using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.SectionAA;
using Elevator.Core.Geometry;
using Elevator.Core.Layouts;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Sheet
{
    /// <summary>
    /// Adds the A0 geared sheet of Templates/Layout_Geared_A0.dxf to a generated drawing (L2b): the layout (frame, SM_TITLEBLOCK), the
    /// reference details at their model-space offset, one locked viewport per view at a standard scale, the title block values and the
    /// logo. Only a layout, untagged model entities (the details), symbol table records and the scale in the views' own titles are
    /// added or changed: the semantic entities of the four views keep their keys, geometry and counts. Never writes to the template.
    /// </summary>
    public static class SheetAssembler
    {
        static readonly Regex ScaleText = new Regex(@"\b1:\d+\b", RegexOptions.Compiled);

        /// <summary>Assembles the sheet. bounds: the extents of each generated view in the drawing (model mm), from the AutoCAD geometric extents of its entities.</summary>
        public static SheetResult Assemble(Database db, SheetRequest request, IList<ViewBounds> bounds, string templateFile)
        {
            if (db == null)
                throw new ArgumentNullException("db");
            if (request == null)
                throw new ArgumentNullException("request");
            if (bounds == null || bounds.Count == 0)
                throw new ArgumentException("The sheet needs the extents of the generated views.");
            if (String.IsNullOrWhiteSpace(templateFile) || !File.Exists(templateFile))
                throw new FileNotFoundException("The layout template is missing.", templateFile);
            string layoutName = String.IsNullOrWhiteSpace(request.LayoutName) ? SheetLayout.Name : request.LayoutName;
            var details = ReadDetails(templateFile);
            var fields = ReadFields(templateFile);

            // 1. The arrangement from the real extents of what each view draws (title and dimension texts at their drawn size, not the
            //    MTEXT reference box: the title of A-A has a box 15 m wide for a 1.5 m text).
            var chosen = new Dictionary<string, SheetDetailSpec>();
            foreach (string name in SheetLayout.ReferenceDetails(request.DoorType))
            {
                var d = details.Details.FirstOrDefault(x => x.Name == name);
                if (d != null && d.Window != null && d.Window.Length == 4)
                    chosen[name] = d;
            }
            var area = SheetArrangement.A0DrawingArea();
            var previous = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;
            ObjectId layoutId;
            try
            {
                // The layout and the reference details come first: their titles take the sheet scale below.
                using (var src = new Database(false, true))
                {
                    src.DxfIn(templateFile, null);
                    layoutId = CreateLayout(db, layoutName);
                    CloneTemplate(db, src, layoutName, layoutId);
                }
            }
            catch
            {
                HostApplicationServices.WorkingDatabase = previous;
                throw;
            }
            SheetArrangementResult arrangement = null;
            Dictionary<string, List<string>> titles = null;
            string last = null;
            var addedTitles = new Dictionary<DrawingViews, ObjectId>();
            for (int pass = 0; pass < 4; pass++)
            {
                var measured = MeasureViews(db, addedTitles);
                var views = new List<SheetViewRequest>();
                Action<DrawingViews, SheetViewKind, string> add = (v, kind, name) =>
                {
                    SheetRect r;
                    if (measured.TryGetValue(v, out r))
                        views.Add(new SheetViewRequest { Kind = kind, Name = name, ModelExtents = r, MarginMm = SheetLayout.ViewMarginMm });
                    else
                    {
                        var bb = bounds.FirstOrDefault(x => x.View == v);
                        if (bb != null)
                            views.Add(new SheetViewRequest
                            {
                                Kind = kind,
                                Name = name,
                                ModelExtents = new SheetRect(bb.MinX, bb.MinY, bb.MaxX - bb.MinX, bb.MaxY - bb.MinY),
                                MarginMm = SheetLayout.ViewMarginMm
                            });
                    }
                };
                add(DrawingViews.Plan, SheetViewKind.ShaftPlan, "Shaft plan");
                add(DrawingViews.MachineRoomPlan, SheetViewKind.MachineRoomPlan, "Machine-room plan");
                add(DrawingViews.SectionAA, SheetViewKind.SectionAA, "Section A-A");
                add(DrawingViews.SectionBB, SheetViewKind.SectionBB, "Section B-B");
                foreach (var kv in chosen)
                {
                    var d = kv.Value;
                    // A reference detail is shown at its template scale or larger, never smaller; the door elevation, whose title in the
                    // drawing says 1:20, is fixed to it. The scale appended to a title may run past the template window: +150 mm.
                    bool fixedScale = d.Kind == "DoorElevationSection";
                    views.Add(new SheetViewRequest
                    {
                        Kind = SheetViewKind.Detail,
                        Name = kv.Key,
                        ModelExtents = new SheetRect(d.Window[0], d.Window[1], d.Window[2] - d.Window[0], d.Window[3] - d.Window[1]),
                        MarginMm = 150,
                        AllowedScales = SheetLayout.DetailScales,
                        MinScaleDenominator = fixedScale ? d.TemplateScale : 0,
                        MaxScaleDenominator = d.TemplateScale
                    });
                }
                arrangement = SheetArrangement.Arrange(area, views);
                if (arrangement.NotPlaced.Count > 0)
                    break;
                // the titles state the sheet scale at 8 mm printed height: apply them, measure again until the scales are stable
                titles = UpdateTitles(db, arrangement.Viewports, chosen, measured, addedTitles);
                string key = String.Join(";", arrangement.Viewports.Select(v => v.Name + "=" + v.ScaleDenominator));
                if (key == last)
                    break;
                last = key;
            }
            var result = new SheetResult
            {
                LayoutName = layoutName,
                DrawingAreaX = area.X,
                DrawingAreaY = area.Y,
                DrawingAreaWidth = area.Width,
                DrawingAreaHeight = area.Height,
                FillRatio = arrangement.FillRatio
            };
            result.NotPlaced.AddRange(arrangement.NotPlaced);
            if (arrangement.NotPlaced.Count > 0)
                throw new InvalidOperationException("The views do not fit the sheet at the smallest standard scale: " + String.Join(", ", arrangement.NotPlaced));

            var attributes = request.Attributes.ToDictionary(a => a.Tag, a => a, StringComparer.OrdinalIgnoreCase);
            var plan = arrangement.Viewports.FirstOrDefault(v => v.Kind == SheetViewKind.ShaftPlan);
            // The scale cell holds the scale of the leading view when the title block data has none.
            TitleBlockAttribute scale;
            if (plan != null && (!attributes.TryGetValue("SCALE", out scale) || String.IsNullOrEmpty(scale.Value)))
                attributes["SCALE"] = TitleBlockAttribute.Of("SCALE", plan.LabelText);

            // 2. The layout (cloned above from the template, which is only read): viewports, title block, logo.
            try
            {
                {
                    // A viewport can only be switched on while its layout is the current one; Model is current again afterwards.
                    LayoutManager.Current.CurrentLayout = layoutName;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                        var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                        FillTitleBlock(db, tr, paper, attributes.Values, fields, result);
                        EnsureViewportLayer(db, tr);
                        foreach (var v in arrangement.Viewports)
                        {
                            AddViewport(tr, paper, v);
                            List<string> own;
                            bool titled = titles.TryGetValue(v.Name, out own) && own.Count > 0;
                            // one title per view, no scale label under a viewport (the fish plate's geometry titles carry none)
                            result.Viewports.Add(new SheetViewportInfo
                            {
                                Name = v.Name,
                                Kind = v.Kind.ToString(),
                                PaperX = v.Paper.X,
                                PaperY = v.Paper.Y,
                                PaperWidth = v.Paper.Width,
                                PaperHeight = v.Paper.Height,
                                ScaleDenominator = v.ScaleDenominator,
                                Label = v.LabelText,
                                ModelCenterX = v.ModelCenterX,
                                ModelCenterY = v.ModelCenterY,
                                HasLabel = false,
                                Titles = titled ? own : new List<string>()
                            });
                        }
                        PlaceLogo(db, tr, paper, request, result);
                        ConfigurePlot(layout);
                        tr.Commit();
                    }
                    LayoutManager.Current.CurrentLayout = "Model";
                }
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = previous;
            }
            return result;
        }

        /// <summary>The reference-detail windows of the template (Layout_Geared_A0.details.json beside the DXF).</summary>
        public static SheetDetailsFile ReadDetails(string templateFile)
        {
            return ReadJson<SheetDetailsFile>(templateFile, ".details.json", "The reference details file of the template is missing.");
        }

        /// <summary>The attribute boxes of the template (Layout_Geared_A0.fields.json beside the DXF).</summary>
        public static SheetFieldsFile ReadFields(string templateFile)
        {
            return ReadJson<SheetFieldsFile>(templateFile, ".fields.json", "The attribute boxes file of the template is missing.");
        }

        static T ReadJson<T>(string templateFile, string suffix, string missing)
        {
            string file = Path.Combine(Path.GetDirectoryName(templateFile) ?? "", Path.GetFileNameWithoutExtension(templateFile) + suffix);
            if (!File.Exists(file))
                throw new FileNotFoundException(missing, file);
            return Elevator.Core.Serialization.CoreJson.Create().Deserialize<T>(File.ReadAllText(file));
        }

        static ObjectId CreateLayout(Database db, string name)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                if (dict.Contains(name))
                    throw new InvalidOperationException("The drawing already has a layout " + name + ".");
                tr.Commit();
            }
            return LayoutManager.Current.CreateLayout(name);
        }

        // Paper-space entities (without the overall viewport) and every model-space entity of the template go into the new layout and
        // the model space; names already in the drawing keep the drawing's definition (DuplicateRecordCloning.Ignore).
        static void CloneTemplate(Database db, Database src, string layoutName, ObjectId targetLayoutId)
        {
            var paperIds = new ObjectIdCollection();
            var modelIds = new ObjectIdCollection();
            using (var tr = src.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(src.LayoutDictionaryId, OpenMode.ForRead);
                if (!dict.Contains(layoutName))
                    throw new InvalidDataException("The template has no layout " + layoutName + ".");
                var layout = (Layout)tr.GetObject(dict.GetAt(layoutName), OpenMode.ForRead);
                foreach (ObjectId id in (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead))
                {
                    if (id.ObjectClass.DxfName == "VIEWPORT")
                        continue;
                    paperIds.Add(id);
                }
                foreach (ObjectId id in (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(src), OpenMode.ForRead))
                    modelIds.Add(id);
                tr.Commit();
            }
            ObjectId targetPaper;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                targetPaper = ((Layout)tr.GetObject(targetLayoutId, OpenMode.ForRead)).BlockTableRecordId;
                tr.Commit();
            }
            if (paperIds.Count > 0)
                src.WblockCloneObjects(paperIds, targetPaper, new IdMapping(), DuplicateRecordCloning.Ignore, false);
            if (modelIds.Count > 0)
                src.WblockCloneObjects(modelIds, SymbolUtilityServices.GetBlockModelSpaceId(db), new IdMapping(), DuplicateRecordCloning.Ignore, false);
        }

        // ---------- the extents of what each view draws ----------

        // The extents of an entity as drawn: an MTEXT takes its actual text size, not its reference width.
        static bool DrawnExtents(Entity e, out Extents3d box)
        {
            box = new Extents3d();
            try
            {
                var mt = e as MText;
                if (mt != null && Math.Abs(mt.Rotation) < 1e-9)
                {
                    double w = mt.ActualWidth, h = mt.ActualHeight;
                    var p = mt.Location;
                    double x0 = p.X, y1 = p.Y;
                    switch (mt.Attachment)
                    {
                        case AttachmentPoint.TopCenter:
                        case AttachmentPoint.MiddleCenter:
                        case AttachmentPoint.BottomCenter:
                            x0 = p.X - w / 2;
                            break;
                        case AttachmentPoint.TopRight:
                        case AttachmentPoint.MiddleRight:
                        case AttachmentPoint.BottomRight:
                            x0 = p.X - w;
                            break;
                    }
                    switch (mt.Attachment)
                    {
                        case AttachmentPoint.MiddleLeft:
                        case AttachmentPoint.MiddleCenter:
                        case AttachmentPoint.MiddleRight:
                            y1 = p.Y + h / 2;
                            break;
                        case AttachmentPoint.BottomLeft:
                        case AttachmentPoint.BottomCenter:
                        case AttachmentPoint.BottomRight:
                            y1 = p.Y + h;
                            break;
                    }
                    box = new Extents3d(new Point3d(x0, y1 - h, p.Z), new Point3d(x0 + w, y1, p.Z));
                    return true;
                }
                box = e.GeometricExtents;
                return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }
        }

        static DrawingViews ViewFlag(string key)
        {
            if (key == null)
                return 0;
            if (key.StartsWith("MachineRoomSection.", StringComparison.Ordinal))
                return DrawingViews.SectionBB;
            if (key.StartsWith("MachineRoom.", StringComparison.Ordinal))
                return DrawingViews.MachineRoomPlan;
            if (key.StartsWith("SectionAA.", StringComparison.Ordinal))
                return DrawingViews.SectionAA;
            if (key.StartsWith("Plan.", StringComparison.Ordinal))
                return DrawingViews.Plan;
            return 0;
        }

        /// <summary>The extents (model mm) of every visible semantic entity of each view, texts at their drawn size.</summary>
        static Dictionary<DrawingViews, SheetRect> MeasureViews(Database db, Dictionary<DrawingViews, ObjectId> addedTitles)
        {
            var acc = new Dictionary<DrawingViews, Extents3d>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                foreach (ObjectId id in model)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null || !e.Visible)
                        continue;
                    var tag = SectionAADrawing.Tag(e);
                    DrawingViews view = 0;
                    if (tag != null && tag.Length > 0)
                        view = ViewFlag(tag[0]);
                    else
                    {
                        // a title the sheet itself added to a view (the shaft plan's own title is hidden in the generated drawing)
                        foreach (var kv in addedTitles)
                            if (kv.Value == id)
                                view = kv.Key;
                    }
                    Extents3d box;
                    if (view == 0 || !DrawnExtents(e, out box))
                        continue;

                    Extents3d cur;
                    if (acc.TryGetValue(view, out cur))
                    {
                        cur.AddExtents(box);
                        acc[view] = cur;
                    }
                    else
                        acc[view] = box;
                }
                tr.Commit();
            }
            return acc.ToDictionary(k => k.Key, k => new SheetRect(k.Value.MinPoint.X, k.Value.MinPoint.Y,
                k.Value.MaxPoint.X - k.Value.MinPoint.X, k.Value.MaxPoint.Y - k.Value.MinPoint.Y));
        }

        // ---------- one title per view, with the sheet scale ----------

        static string ViewOf(string key)
        {
            if (key == null)
                return null;
            if (key.StartsWith("MachineRoomSection.", StringComparison.Ordinal))
                return "Section B-B";
            if (key.StartsWith("MachineRoom.", StringComparison.Ordinal))
                return "Machine-room plan";
            if (key.StartsWith("SectionAA.", StringComparison.Ordinal))
                return "Section A-A";
            if (key.StartsWith("Plan.", StringComparison.Ordinal))
                return "Shaft plan";
            return null;
        }

        // The text of an MTEXT without its formatting codes, for the report.
        static string Plain(string contents)
        {
            string t = Regex.Replace(contents ?? "", @"\\[A-Za-z][^;]*;", "");
            t = t.Replace("{", "").Replace("}", "").Replace("\\P", " ");
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Writes the sheet scale into the titles drawn in model space: the four view titles ("SHAFT AND CAR 1:8" becomes "SHAFT AND CAR
        /// 1:10") and the reference details whose titles are text (the scale is appended to the last title line). Returns the titles
        /// that state a scale per viewport name; a detail whose title is flattened geometry and states the scale itself (the door
        /// elevation, fixed at its template scale) counts as titled with the template text. Semantic entities keep their keys and
        /// count; only the contents of the title text changes.
        /// </summary>
        static Dictionary<string, List<string>> UpdateTitles(
            Database db,
            IList<SheetViewport> viewports,
            Dictionary<string, SheetDetailSpec> details,
            Dictionary<DrawingViews, SheetRect> measured,
            Dictionary<DrawingViews, ObjectId> addedTitles)
        {
            var result = new Dictionary<string, List<string>>();
            var scaleOf = viewports.ToDictionary(v => v.Name, v => v.ScaleDenominator);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                var detailTexts = details.Keys.ToDictionary(k => k, k => new List<MText>());
                foreach (ObjectId id in model)
                {
                    var mt = tr.GetObject(id, OpenMode.ForRead) as MText;
                    if (mt == null)
                        continue;
                    var tag = SectionAADrawing.Tag(mt);
                    string view = tag == null || tag.Length == 0 ? null : ViewOf(tag[0]);
                    if (view != null && !mt.Visible)
                        continue; // a hidden title of the master (the shaft plan's): not a title of the sheet
                    if (view != null)
                    {
                        // a title of a generated view: any text of it that states a scale
                        if (scaleOf.ContainsKey(view) && ScaleText.IsMatch(mt.Contents))
                        {
                            mt.UpgradeOpen();
                            // the section audit reads the centre of these texts: the title keeps its centre when its height changes
                            var before = mt.GeometricExtents;
                            mt.Contents = ScaleText.Replace(mt.Contents, "1:" + scaleOf[view], 1);
                            mt.TextHeight = SheetLayout.TitlePaperHeightMm * scaleOf[view];
                            mt.Width = 0;
                            var after = mt.GeometricExtents;
                            mt.Location = mt.Location + new Vector3d(
                                (before.MinPoint.X + before.MaxPoint.X - after.MinPoint.X - after.MaxPoint.X) / 2,
                                (before.MinPoint.Y + before.MaxPoint.Y - after.MinPoint.Y - after.MaxPoint.Y) / 2, 0);
                            AddTitle(result, view, Plain(mt.Contents));
                        }
                        continue;
                    }
                    // untagged: a title text of a reference detail (underlined, inside its window)
                    if (tag != null && tag.Length > 0)
                        continue;
                    foreach (var d in details)
                    {
                        var w = d.Value.Window;
                        var p = mt.Location;
                        if (p.X >= w[0] && p.X <= w[2] && p.Y >= w[1] && p.Y <= w[3] && mt.Contents.Contains("\\L"))
                            detailTexts[d.Key].Add(mt);
                    }
                }
                // The shaft plan's own title text is hidden in the generated drawing (the master keeps it invisible): the sheet adds
                // an untagged title "SHAFT AND CAR 1:N", centred under the plan, as the user did by hand.
                SheetRect planBox;
                int planScale;
                if (!result.ContainsKey("Shaft plan") && scaleOf.TryGetValue("Shaft plan", out planScale) && measured.TryGetValue(DrawingViews.Plan, out planBox))
                {
                    var bt = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                    ObjectId existing;
                    MText title;
                    bool isNew = false;
                    if (addedTitles.TryGetValue(DrawingViews.Plan, out existing))
                        title = (MText)tr.GetObject(existing, OpenMode.ForWrite);
                    else
                    {
                        isNew = true;
                        title = new MText();
                        title.SetDatabaseDefaults();
                        bt.AppendEntity(title);
                        tr.AddNewlyCreatedDBObject(title, true);
                        addedTitles[DrawingViews.Plan] = title.ObjectId;
                    }
                    title.Contents = "{\\Fcent|c0;\\L\\C6;" + PlanTitleText(db, tr) + " 1:" + planScale + "}";
                    title.TextHeight = SheetLayout.TitlePaperHeightMm * planScale;
                    title.Width = 0;
                    title.Attachment = AttachmentPoint.TopCenter;
                    // under the plan content (measured before the title existed): its top stays where it was put
                    if (isNew)
                        title.Location = new Point3d(planBox.X + planBox.Width / 2, planBox.Y - 0.5 * title.TextHeight, 0);
                    AddTitle(result, "Shaft plan", Plain(title.Contents));
                }
                foreach (var d in details)
                {
                    int s;
                    if (!scaleOf.TryGetValue(d.Key, out s))
                        continue;
                    var texts = detailTexts[d.Key];
                    if (d.Value.Kind == "DoorElevationSection")
                    {
                        // geometry titles "VIEW at D-D 1:20" / "SEC. C-C 1:20": fixed to the template scale, so they state it
                        if (s == d.Value.TemplateScale)
                            AddTitle(result, d.Key, "VIEW at D-D 1:" + s + " / SEC. C-C 1:" + s);
                        continue;
                    }
                    if (texts.Count == 0)
                        continue; // no editable title (fish plate): the label under the viewport stays
                    double lowest = texts.Min(t => t.Location.Y);
                    foreach (var mt in texts.Where(t => t.Location.Y - lowest < 5))
                    {
                        mt.UpgradeOpen();
                        string c = mt.Contents;
                        string updated = ScaleText.IsMatch(c) ? ScaleText.Replace(c, "1:" + s, 1) : Regex.Replace(c, @"\s*\}\s*$", " 1:" + s + "}");
                        if (!ScaleText.IsMatch(updated))
                            updated = c + " 1:" + s;
                        mt.Contents = updated;
                        mt.Width = 0; // no wrapping: the longer text must not lose its tail behind the old reference width
                        AddTitle(result, d.Key, Plain(mt.Contents));
                    }
                }
                tr.Commit();
            }
            return result;
        }

        // The text of the plan title: the project's own (hidden) title without its scale, else "SHAFT AND CAR".
        static string PlanTitleText(Database db, Transaction tr)
        {
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var mt = tr.GetObject(id, OpenMode.ForRead) as MText;
                if (mt == null)
                    continue;
                var tag = SectionAADrawing.Tag(mt);
                if (tag != null && tag.Length > 0 && tag[0].StartsWith("Plan.", StringComparison.Ordinal) && ScaleText.IsMatch(mt.Contents))
                {
                    string t = Plain(ScaleText.Replace(mt.Contents, "", 1));
                    if (t.Length > 0)
                        return t;
                }
            }
            return "SHAFT AND CAR";
        }

        static void AddTitle(Dictionary<string, List<string>> map, string view, string text)
        {
            List<string> l;
            if (!map.TryGetValue(view, out l))
                map[view] = l = new List<string>();
            l.Add(text);
        }

        // ---------- title block ----------

        static void FillTitleBlock(Database db, Transaction tr, BlockTableRecord paper, IEnumerable<TitleBlockAttribute> values, SheetFieldsFile fields, SheetResult result)
        {
            var byTag = values.GroupBy(v => v.Tag, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            var boxes = fields.Fields.ToDictionary(f => f.Tag, f => f, StringComparer.OrdinalIgnoreCase);
            var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            bool found = false;
            foreach (ObjectId id in paper)
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null)
                    continue;
                var def = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                if (!String.Equals(def.Name, SheetLayout.TitleBlock, StringComparison.OrdinalIgnoreCase))
                    continue;
                found = true;
                br.UpgradeOpen();
                var refs = new Dictionary<string, AttributeReference>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = (AttributeReference)tr.GetObject(attId, OpenMode.ForWrite);
                    refs[att.Tag] = att;
                    TitleBlockAttribute v;
                    if (!byTag.TryGetValue(att.Tag, out v))
                    {
                        att.TextString = "";
                        continue;
                    }
                    if (styles.Has(v.Style))
                        att.TextStyleId = styles[v.Style];
                    att.TextString = v.DrawingText ?? "";
                }
                foreach (var pair in refs)
                {
                    TitleBlockAttribute v;
                    if (!byTag.TryGetValue(pair.Key, out v) || pair.Key.EndsWith("_2", StringComparison.Ordinal))
                        continue;
                    SheetFieldSpec box;
                    boxes.TryGetValue(pair.Key, out box);
                    AttributeReference second = null;
                    string secondTag = TitleBlockData.SecondLine(pair.Key);
                    if (secondTag != null)
                        refs.TryGetValue(secondTag, out second);
                    var fit = Fit(db, pair.Value, second, box, v.DrawingText ?? "");
                    if (fit != null)
                        result.Attributes.Add(fit);
                }
            }
            if (!found)
                throw new InvalidDataException("The layout has no " + SheetLayout.TitleBlock + " block.");
        }

        static double WidthOf(AttributeReference att)
        {
            var e = att.GeometricExtents;
            return e.MaxPoint.X - e.MinPoint.X;
        }

        static void Realign(Database db, AttributeReference att)
        {
            try
            {
                att.AdjustAlignment(db);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
            }
        }

        // Width factor down to the minimum, then the height down to its minimum share: returns the width in mm.
        static double Shrink(Database db, AttributeReference att, double avail, double nominal, double minShare)
        {
            double w = WidthOf(att);
            if (w > avail)
            {
                att.WidthFactor = Math.Max(SheetLayout.MinWidthFactor, att.WidthFactor * avail / w);
                Realign(db, att);
                w = WidthOf(att);
            }
            if (w > avail)
            {
                att.Height = Math.Max(nominal * minShare, att.Height * avail / w);
                Realign(db, att);
                w = WidthOf(att);
            }
            return w;
        }

        /// <summary>
        /// Makes a value fit the box of its attribute: the width factor down to 0.7, then the height down to 55 % of the nominal, then
        /// (the big frames, which have a second-line attribute) two lines, last resort a smaller height still. Returns how it was drawn.
        /// </summary>
        static SheetAttributeFit Fit(Database db, AttributeReference att, AttributeReference second, SheetFieldSpec box, string text)
        {
            Realign(db, att);
            double nominal = att.Height;
            var fit = new SheetAttributeFit { Tag = att.Tag, Text = text, NominalHeightMm = nominal, Lines = 1, WidthFactor = att.WidthFactor, HeightMm = nominal };
            if (box == null || text.Length == 0)
            {
                fit.AvailableWidthMm = box == null ? 0 : box.AvailableWidthMm;
                fit.Fits = true;
                return fit;
            }
            // AutoCAD's extents of SHX text run a few percent short of what is plotted: fit to 92 % of the free width
            double avail = box.AvailableWidthMm * 0.92;
            fit.AvailableWidthMm = avail;
            double w = Shrink(db, att, avail, nominal, SheetLayout.MinHeightShare);
            if (w > avail && second != null && box.MaxLines >= 2)
            {
                int cut = SplitPoint(text);
                if (cut > 0)
                {
                    // two lines: both at 80 % of the nominal height, the width factor and height fitted per line, moved apart
                    string line1 = text.Substring(0, cut).TrimEnd(), line2 = text.Substring(cut + 1).TrimStart();
                    double h = nominal * 0.62;
                    att.TextString = line1;
                    second.TextString = line2;
                    second.TextStyleId = att.TextStyleId;
                    foreach (var a in new[] { att, second })
                    {
                        a.WidthFactor = 1.0;
                        a.Height = h;
                    }
                    var anchor = att.AlignmentPoint;
                    double dy = h * 0.95;
                    att.AlignmentPoint = new Point3d(anchor.X, anchor.Y + dy, anchor.Z);
                    second.AlignmentPoint = new Point3d(anchor.X, anchor.Y - dy, anchor.Z);
                    Realign(db, att);
                    Realign(db, second);
                    double w1 = Shrink(db, att, avail, h, 0.6), w2 = Shrink(db, second, avail, h, 0.6);
                    fit.Lines = 2;
                    fit.WidthFactor = Math.Min(att.WidthFactor, second.WidthFactor);
                    fit.HeightMm = Math.Min(att.Height, second.Height);
                    fit.WidthMm = Math.Max(w1, w2);
                    fit.Fits = fit.WidthMm <= avail + 0.05;
                    return fit;
                }
            }
            // last resort: a smaller height than the readable minimum, but never overflowing the box
            int guard = 0;
            while (w > avail && att.Height > 1.0 && guard++ < 20)
            {
                att.Height = Math.Max(1.0, att.Height * Math.Min(0.95, avail / w));
                Realign(db, att);
                w = WidthOf(att);
            }
            fit.WidthFactor = att.WidthFactor;
            fit.HeightMm = att.Height;
            fit.WidthMm = w;
            fit.Fits = w <= avail + 0.05;
            return fit;
        }

        // The index of the space nearest the middle of a text, or -1.
        static int SplitPoint(string text)
        {
            int mid = text.Length / 2;
            for (int d = 0; d < text.Length; d++)
            {
                if (mid - d > 0 && text[mid - d] == ' ')
                    return mid - d;
                if (mid + d < text.Length - 1 && text[mid + d] == ' ')
                    return mid + d;
            }
            return -1;
        }

        static void EnsureViewportLayer(Database db, Transaction tr)
        {
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layers.Has(SheetLayout.ViewportLayer))
                return;
            layers.UpgradeOpen();
            var l = new LayerTableRecord
            {
                Name = SheetLayout.ViewportLayer,
                IsPlottable = false,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 4)
            };
            layers.Add(l);
            tr.AddNewlyCreatedDBObject(l, true);
        }

        static void AddViewport(Transaction tr, BlockTableRecord paper, SheetViewport v)
        {
            var vp = new Autodesk.AutoCAD.DatabaseServices.Viewport();
            vp.SetDatabaseDefaults();
            vp.CenterPoint = new Point3d(v.Paper.X + v.Paper.Width / 2, v.Paper.Y + v.Paper.Height / 2, 0);
            vp.Width = v.Paper.Width;
            vp.Height = v.Paper.Height;
            vp.ViewDirection = Vector3d.ZAxis;
            vp.ViewTarget = new Point3d(v.ModelCenterX, v.ModelCenterY, 0);
            vp.ViewCenter = Point2d.Origin;
            vp.ViewHeight = v.Paper.Height * v.ScaleDenominator;
            vp.Layer = SheetLayout.ViewportLayer;
            paper.AppendEntity(vp);
            tr.AddNewlyCreatedDBObject(vp, true);
            vp.On = true;
            vp.CustomScale = 1.0 / v.ScaleDenominator;
            vp.Locked = true;
        }

        static void AddLabel(Database db, Transaction tr, BlockTableRecord paper, SheetViewport v)
        {
            var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            var t = new DBText();
            t.SetDatabaseDefaults();
            t.TextString = v.LabelText;
            t.Height = SheetLayout.LabelHeight;
            if (styles.Has(XarabText.LatinStyle))
                t.TextStyleId = styles[XarabText.LatinStyle];
            t.HorizontalMode = TextHorizontalMode.TextCenter;
            t.VerticalMode = TextVerticalMode.TextVerticalMid;
            t.AlignmentPoint = new Point3d(v.LabelX, v.LabelY, 0);
            paper.AppendEntity(t);
            tr.AddNewlyCreatedDBObject(t, true);
            t.AdjustAlignment(db);
        }

        // The logo image goes into the rectangle of the layer SM_LOGO_AREA, its aspect ratio kept, centred.
        static void PlaceLogo(Database db, Transaction tr, BlockTableRecord paper, SheetRequest request, SheetResult result)
        {
            if (String.IsNullOrWhiteSpace(request.LogoFile))
                return;
            if (!File.Exists(request.LogoFile) || request.LogoWidthPx <= 0 || request.LogoHeightPx <= 0)
            {
                result.LogoMessage = "The logo file or its size is missing.";
                return;
            }
            Extents3d? box = null;
            foreach (ObjectId id in paper)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null || !String.Equals(e.Layer, SheetLayout.LogoLayer, StringComparison.OrdinalIgnoreCase) || e is RasterImage)
                    continue;
                var b = e.GeometricExtents;
                if (box.HasValue)
                {
                    var t = box.Value;
                    t.AddExtents(b);
                    box = t;
                }
                else
                    box = b;
            }
            if (!box.HasValue)
            {
                result.LogoMessage = "The template has no logo area.";
                return;
            }
            double aw = box.Value.MaxPoint.X - box.Value.MinPoint.X, ah = box.Value.MaxPoint.Y - box.Value.MinPoint.Y;
            double k = Math.Min(aw / request.LogoWidthPx, ah / request.LogoHeightPx);
            double w = request.LogoWidthPx * k, h = request.LogoHeightPx * k;
            var origin = new Point3d(box.Value.MinPoint.X + (aw - w) / 2, box.Value.MinPoint.Y + (ah - h) / 2, 0);
            try
            {
                ObjectId dictId = RasterImageDef.GetImageDictionary(db);
                if (dictId.IsNull)
                    dictId = RasterImageDef.CreateImageDictionary(db);
                var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
                const string key = "SM_COMPANY_LOGO";
                ObjectId defId;
                if (dict.Contains(key))
                    defId = dict.GetAt(key);
                else
                {
                    var def = new RasterImageDef { SourceFileName = request.LogoFile };
                    def.Load();
                    defId = dict.SetAt(key, def);
                    tr.AddNewlyCreatedDBObject(def, true);
                }
                var image = new RasterImage
                {
                    ImageDefId = defId,
                    Orientation = new CoordinateSystem3d(origin, new Vector3d(w, 0, 0), new Vector3d(0, h, 0)),
                    Layer = SheetLayout.LogoLayer
                };
                paper.AppendEntity(image);
                tr.AddNewlyCreatedDBObject(image, true);
                RasterImage.EnableReactors(true);
                image.AssociateRasterDef((RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite));
                result.LogoPlaced = true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                result.LogoPlaced = false;
                result.LogoMessage = "The logo image could not be placed: " + e.Message;
            }
        }

        // Plot settings for the PDF route (device, A0 landscape, monochrome.ctb, layout plot, 1:1). Not needed to draw the sheet: a device
        // or media name that is missing leaves the layout as the template made it.
        static void ConfigurePlot(Layout layout)
        {
            try
            {
                var v = PlotSettingsValidator.Current;
                v.SetPlotConfigurationName(layout, "DWG To PDF.pc3", null);
                v.RefreshLists(layout);
                string media = null;
                foreach (string m in v.GetCanonicalMediaNameList(layout))
                {
                    // The full-bleed A0 keeps the whole 1189 x 841 mm printable (the title-block column is at the sheet edge).
                    if (m.IndexOf("full_bleed_A0", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("841", StringComparison.Ordinal) >= 0)
                    {
                        media = m;
                        break;
                    }
                }
                if (media != null)
                    v.SetCanonicalMediaName(layout, media);
                v.SetPlotType(layout, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                v.SetPlotRotation(layout, PlotRotation.Degrees090);
                v.SetUseStandardScale(layout, false);
                v.SetCustomPrintScale(layout, new CustomScale(1, 1));
                v.SetCurrentStyleSheet(layout, "monochrome.ctb");
                v.SetPlotCentered(layout, true);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
            }
        }
    }
}
