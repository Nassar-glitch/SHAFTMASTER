using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Layouts;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Sheet
{
    /// <summary>
    /// Adds the A0 geared sheet of Templates/Layout_Geared_A0.dxf to a generated drawing (L2b): the layout (frame, SM_TITLEBLOCK), the
    /// reference details at their model-space offset, one locked viewport per view at a standard scale with its scale label, the title
    /// block values and the logo. Only a layout, untagged model entities (the details) and symbol table records are added: the four
    /// views and their semantic entities are not touched. Never writes to the template.
    /// </summary>
    public static class SheetAssembler
    {
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

            // 1. The arrangement from the real extents.
            var views = new List<SheetViewRequest>();
            Action<DrawingViews, SheetViewKind, string> add = (v, kind, name) =>
            {
                var b = bounds.FirstOrDefault(x => x.View == v);
                if (b != null)
                    views.Add(new SheetViewRequest
                    {
                        Kind = kind,
                        Name = name,
                        ModelExtents = new SheetRect(b.MinX, b.MinY, b.MaxX - b.MinX, b.MaxY - b.MinY),
                        MarginMm = SheetLayout.ViewMarginMm
                    });
            };
            add(DrawingViews.Plan, SheetViewKind.ShaftPlan, "Shaft plan");
            add(DrawingViews.MachineRoomPlan, SheetViewKind.MachineRoomPlan, "Machine-room plan");
            add(DrawingViews.SectionAA, SheetViewKind.SectionAA, "Section A-A");
            add(DrawingViews.SectionBB, SheetViewKind.SectionBB, "Section B-B");
            foreach (string name in SheetLayout.ReferenceDetails(request.DoorType))
            {
                var d = details.Details.FirstOrDefault(x => x.Name == name);
                if (d == null || d.Window == null || d.Window.Length != 4)
                    continue;
                views.Add(new SheetViewRequest
                {
                    Kind = SheetViewKind.Detail,
                    Name = name,
                    ModelExtents = new SheetRect(d.Window[0], d.Window[1], d.Window[2] - d.Window[0], d.Window[3] - d.Window[1]),
                    MinScaleDenominator = d.TemplateScale
                });
            }
            var area = SheetArrangement.A0DrawingArea();
            var arrangement = SheetArrangement.Arrange(area, views);
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

            // 2. The layout and its content, cloned from the template (the template database is only read).
            var previous = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;
            try
            {
                using (var src = new Database(false, true))
                {
                    src.DxfIn(templateFile, null);
                    ObjectId layoutId = CreateLayout(db, layoutName);
                    CloneTemplate(db, src, layoutName, layoutId);
                    // A viewport can only be switched on while its layout is the current one; Model is current again afterwards.
                    LayoutManager.Current.CurrentLayout = layoutName;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                        var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                        FillTitleBlock(db, tr, paper, attributes.Values);
                        EnsureViewportLayer(db, tr);
                        foreach (var v in arrangement.Viewports)
                        {
                            AddViewport(tr, paper, v);
                            AddLabel(db, tr, paper, v);
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
                                ModelCenterY = v.ModelCenterY
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
            string file = Path.Combine(Path.GetDirectoryName(templateFile) ?? "", Path.GetFileNameWithoutExtension(templateFile) + ".details.json");
            if (!File.Exists(file))
                throw new FileNotFoundException("The reference details file of the template is missing.", file);
            return Elevator.Core.Serialization.CoreJson.Create().Deserialize<SheetDetailsFile>(File.ReadAllText(file));
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

        static void FillTitleBlock(Database db, Transaction tr, BlockTableRecord paper, IEnumerable<TitleBlockAttribute> values)
        {
            var byTag = values.GroupBy(v => v.Tag, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
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
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = (AttributeReference)tr.GetObject(attId, OpenMode.ForWrite);
                    TitleBlockAttribute v;
                    if (!byTag.TryGetValue(att.Tag, out v))
                    {
                        att.TextString = "";
                        continue;
                    }
                    if (styles.Has(v.Style))
                        att.TextStyleId = styles[v.Style];
                    att.TextString = v.DrawingText ?? "";
                    try
                    {
                        att.AdjustAlignment(db);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                    }
                }
            }
            if (!found)
                throw new InvalidDataException("The layout has no " + SheetLayout.TitleBlock + " block.");
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
