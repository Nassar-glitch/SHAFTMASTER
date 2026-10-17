using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Steel;
using Elevator.Core.Doors;
using Elevator.Core.Layouts;
using Elevator.Core.Project;
using Elevator.Core.Steel;

namespace Elevator.AutoCAD.Sheet
{
    /// <summary>
    /// The landing door detail of the A0 sheet (stage G2): "VIEW at D-D" and "SEC. C-C" from the user's four master doors
    /// (Templates/DoorMasters.json, extracted from docs/design/details/g2/source by tools/door_masters.py), stretched to the project's
    /// opening and entrance height and mirrored for the side by the rules of docs/design/details/DOORS-G2.md. The door is a named block
    /// SM_DOOR_&lt;type&gt;_&lt;panel&gt;_W_H_S_&lt;side&gt;; dimensions, linetypes, glass hatch, texts and marks are native.
    /// </summary>
    static partial class RailDetails
    {
        const double DoorShift = 110000;   // the details strip starts at x 900000 (MeasureDetails)
        public const string LayerDoor = "A-DOOR";
        const string LayerGlass = "A-GLAZ";

        // ---------- the masters ----------
        public sealed class DoorMasterFile
        {
            public double OpeningMm;
            public double HeightMm;
            public Dictionary<string, DoorMaster> Doors;
        }

        public sealed class DoorMaster
        {
            public MasterView Elevation;
            public MasterView Section;
        }

        public sealed class MasterView
        {
            public List<MasterLine> Lines;
            public List<double[]> Circles;
            public List<List<double[]>> Glass;
            public List<List<double[]>> Solids;
        }

        public sealed class MasterLine
        {
            public string Lt;
            public bool Closed;
            public List<double[]> Pts;
        }

        static DoorMasterFile masters;

        static DoorMasterFile Masters()
        {
            if (masters == null)
            {
                string file = Path.Combine(Configuration.Root, "Templates", "DoorMasters.json");
                if (!File.Exists(file))
                    return null;
                masters = Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<DoorMasterFile>(File.ReadAllText(file));
            }
            return masters;
        }

        // ---------- the project's door ----------
        sealed class DoorData
        {
            public string Type;          // Manual, Automatic2S, Automatic2C
            public bool StackRight;
            public double OpeningMm, OffsetMm, ClearHeightMm, StructuralMm;
            public DoorDetailSizes Sizes;
            public bool Steel;
            public ChannelProfile Beam;
            public double HeadBeamBottomMm;
            public bool Automatic { get { return Type != DoorTypes.Manual; } }
            public string TypeName { get { return Type == DoorTypes.Automatic2S ? "2S" : Type == DoorTypes.Automatic2C ? "2C" : "SWING"; } }
            public string MasterName { get { return (Automatic ? "AUTO_" : "SWING_") + (Sizes.Panel == DoorDetailFields.Glass ? "GLASS" : Automatic ? "SOLID" : "WINDOW"); } }
            /// <summary>The master is mirrored: a swing door hinged at the left, a 2S door stacking to the right.</summary>
            public bool Mirror { get { return Automatic ? Type == DoorTypes.Automatic2S && StackRight : !Sizes.HingeRight; } }
            public string Side { get { return Type == DoorTypes.Automatic2C ? "" : Automatic ? (StackRight ? "_R" : "_L") : (Sizes.HingeRight ? "_HR" : "_HL"); } }
        }

        public sealed class DoorDrawer
        {
            readonly Database db;
            readonly SheetDetailSpec spec;
            readonly double[] old;
            readonly List<ObjectId> made = new List<ObjectId>();
            bool hidden;
            int scale;

            public DoorDrawer(Database db, Dictionary<string, SheetDetailSpec> chosen)
            {
                this.db = db;
                spec = chosen.Values.FirstOrDefault(d => d.Kind == "DoorElevationSection");
                if (spec != null && spec.Window != null)
                    old = (double[])spec.Window.Clone();
                else
                    spec = null;
            }

            public bool Active { get; private set; }

            public string Name
            {
                get { return spec == null ? null : spec.Name; }
            }

            public bool Draw(int scale)
            {
                if (spec == null || Masters() == null)
                    return false;
                if (Active && scale == this.scale)
                    return true;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var d = ReadDoor(db, tr);
                    if (d == null || !Masters().Doors.ContainsKey(d.MasterName))
                    {
                        tr.Commit();
                        return false;
                    }
                    foreach (var id in made)
                    {
                        var e = tr.GetObject(id, OpenMode.ForWrite, true) as Entity;
                        if (e != null && !e.IsErased)
                            e.Erase();
                    }
                    made.Clear();
                    if (!hidden)
                    {
                        HideTemplate(db, tr, old, old[2] + 1);
                        hidden = true;
                    }
                    var before = Existing(db, tr);
                    Text = 2.5 * Math.Max(scale, 1);
                    TitleWindow = (double[])spec.Window.Clone();
                    var box = DrawDoor(db, tr, d, old, scale);
                    spec.Window = new[] { old[0] - DoorShift, old[1], box[2] + Margin, Math.Max(old[3], box[3] + Margin) };
                    Untag(db, tr);
                    var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                    foreach (ObjectId id in model)
                    {
                        if (!before.Contains(id))
                            made.Add(id);
                    }
                    tr.Commit();
                }
                this.scale = scale;
                Active = true;
                return true;
            }
        }

        static DoorData ReadDoor(Database db, Transaction tr)
        {
            ElevatorProject project;
            try
            {
                project = UnifiedProjectStorage.Read(db, tr);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            var lib = SteelDrawing.Library();
            if (project == null || lib == null)
                return null;
            var dims = PlanDimensions(db, tr);
            if (!dims.ContainsKey("Part010"))
                return null;
            var d = new DoorData
            {
                Type = DoorTypes.Normalize(project.Doors == null ? null : project.Doors.Type),
                OpeningMm = dims["Part010"],
                Sizes = DoorDetailFields.Effective(project, lib)
            };
            d.ClearHeightMm = d.Sizes.ClearHeightMm;
            // the structural opening: the plan's wall opening (the jambs either side of OPENING) when the plan has it, else the preset rule
            d.StructuralMm = dims.ContainsKey("Part009") && dims.ContainsKey("Part011")
                ? dims["Part009"] + d.OpeningMm + dims["Part011"]
                : d.Sizes.StructuralWidth(d.OpeningMm);
            // the opening centre against the shaft centre, as the plan dimensions it (Part012 left, Part013 right of the door)
            if (dims.ContainsKey("Part012") && dims.ContainsKey("Part013"))
                d.OffsetMm = (dims["Part012"] - dims["Part013"]) / 2;
            var input = DoorInput.From(project, Configuration.DoorCatalog());
            if (input != null && input.Settings != null && input.Catalog != null)
                d.StackRight = input.Settings.Handing == DoorTypes.Right || (input.Settings.Handing != DoorTypes.Left && input.Catalog.GovernorSide == DoorTypes.Left);
            if (SteelFields.IsSteel(project, lib))
            {
                var st = SteelStructure.Build(project, lib);
                if (st != null && st.Valid && st.Inputs != null && st.Inputs.Beam != null)
                {
                    d.Steel = true;
                    d.Beam = st.Inputs.Beam;
                    d.HeadBeamBottomMm = st.Inputs.DoorHeightMm;
                }
            }
            return d;
        }

        static string DoorTag(DoorData d)
        {
            string panel = d.Sizes.Panel == DoorDetailFields.Glass ? "GLASS" : d.Automatic ? "SOLID" : "WINDOW";
            return d.TypeName + "_" + panel + "_W" + N(d.OpeningMm) + "_H" + N(d.ClearHeightMm) + "_S" + N(d.StructuralMm) + d.Side;
        }

        static ObjectId Linetype(Database db, Transaction tr, string name)
        {
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(name))
                return lt[name];
            try
            {
                db.LoadLineTypeFile(name, "acad.lin");
                lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(name))
                    return lt[name];
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
            }
            return ObjectId.Null;
        }

        // ---------- stretch and mirror (DOORS-G2.md section 4) ----------
        const double FixedAbove = 1500;   // parts below keep their height above the floor (handle, vision window, indicator, sill)

        sealed class Stretch
        {
            public double DW, DH, FixedX;
            public bool Mirror, Elevation;

            public Point2d Map(double x, double y)
            {
                if (Elevation && Math.Abs(x) > FixedX)
                    x += Math.Sign(x) * DW / 2;
                if (y > FixedAbove)
                    y += DH;
                return new Point2d(Mirror ? -x : x, y);
            }
        }

        // The header above the opening in the section: the bottom of the wall above (auto 200, swing 160).
        static double WallBottom(MasterView s)
        {
            double best = double.MaxValue;
            foreach (var l in s.Lines)
            {
                for (int i = 0; i + 1 < l.Pts.Count; i++)
                {
                    var a = l.Pts[i];
                    var b = l.Pts[i + 1];
                    if (Math.Abs(a[1] - b[1]) < 0.6 && a[1] > 2050 && a[1] < 2400 && Math.Abs(a[0] - b[0]) > 200)
                        best = Math.Min(best, a[1]);
                }
            }
            return best == double.MaxValue ? 2200 : best;
        }

        // Steel: the wall above the header is the door-head beam (drawn separately), so its outline and the break line above are left out.
        static bool AboveWall(MasterLine l, double wallBottom)
        {
            return l.Pts.All(p => p[1] >= wallBottom - 0.6 && p[0] <= 495) || l.Pts.All(p => p[1] >= 2600);
        }

        static void PutView(Database db, Transaction tr, BlockTableRecord btr, MasterView v, Stretch s, Func<MasterLine, bool> skip, int scale)
        {
            var center = Linetype(db, tr, "CENTER");
            var hiddenLt = Linetype(db, tr, "HIDDEN");
            foreach (var l in v.Lines)
            {
                if (skip != null && skip(l))
                    continue;
                var pts = l.Pts.Select(p => s.Map(p[0], p[1])).ToList();
                if (pts.Count < 2 || (pts.Count == 2 && pts[0].GetDistanceTo(pts[1]) < 3))
                    continue;
                var pl = new Polyline();
                for (int i = 0; i < pts.Count; i++)
                    pl.AddVertexAt(i, pts[i], 0, 0, 0);
                pl.Closed = l.Closed;
                SteelDrawing.Put(btr, tr, pl, LayerDoor);
                if (l.Lt == "CENTER" || (s.Elevation && l.Lt == "HIDDEN"))
                {
                    if (!center.IsNull)
                        pl.LinetypeId = center;
                    pl.Layer = SteelDrawing.LayerAnno;
                }
                else if (l.Lt == "HIDDEN" && !hiddenLt.IsNull)
                    pl.LinetypeId = hiddenLt;
                if (l.Lt != "CONTINUOUS")
                    pl.LinetypeScale = scale * 0.5;
            }
            foreach (var c in v.Circles ?? new List<double[]>())
            {
                var p = s.Map(c[0], c[1]);
                SteelDrawing.Put(btr, tr, new Circle(new Point3d(p.X, p.Y, 0), Vector3d.ZAxis, c[2]), LayerDoor);
            }
            foreach (var g in v.Glass ?? new List<List<double[]>>())
                HatchIn(db, tr, btr, g.Select(p => s.Map(p[0], p[1])).ToList(), "GOST_GLASS", 30, LayerGlass);
            foreach (var g in v.Solids ?? new List<List<double[]>>())
                HatchIn(db, tr, btr, g.Select(p => s.Map(p[0], p[1])).ToList(), "SOLID", 1, LayerDoor);
        }

        static void HatchIn(Database db, Transaction tr, BlockTableRecord btr, List<Point2d> pts, string pattern, double patternScale, string layer)
        {
            if (pts.Count < 3)
                return;
            var h = new Hatch();
            h.SetDatabaseDefaults(db);
            SteelDrawing.Put(btr, tr, h, layer);
            h.Normal = Vector3d.ZAxis;
            try
            {
                h.SetHatchPattern(HatchPatternType.PreDefined, pattern);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI32");
            }
            if (pattern != "SOLID")
                h.PatternScale = patternScale;
            var v = new Point2dCollection(pts.ToArray());
            v.Add(pts[0]);
            h.AppendLoop(HatchLoopTypes.Polyline, v, new DoubleCollection(Enumerable.Repeat(0.0, v.Count).ToArray()));
            h.EvaluateHatch(true);
        }

        // The landing elevation of the door as one named block: origin at the opening centre on the floor.
        static ObjectId DoorElevationBlock(Database db, Transaction tr, DoorData d, int scale)
        {
            var m = Masters();
            var master = m.Doors[d.MasterName];
            return SteelDrawing.Block(db, tr, "SM_DOOR_" + DoorTag(d), btr =>
            {
                var s = new Stretch
                {
                    DW = d.OpeningMm - m.OpeningMm,
                    DH = d.ClearHeightMm - m.HeightMm,
                    FixedX = d.Automatic ? 45 : 150,
                    Mirror = d.Mirror,
                    Elevation = true
                };
                PutView(db, tr, btr, master.Elevation, s, null, scale);
            });
        }

        static ObjectId DoorSectionBlock(Database db, Transaction tr, DoorData d, int scale)
        {
            var m = Masters();
            var master = m.Doors[d.MasterName];
            double wb = WallBottom(master.Section);
            string name = "SM_DOOR_SEC_" + d.MasterName + "_H" + N(d.ClearHeightMm) + (d.Steel ? "_STEEL" : "");
            return SteelDrawing.Block(db, tr, name, btr =>
            {
                var s = new Stretch { DH = d.ClearHeightMm - m.HeightMm };
                PutView(db, tr, btr, master.Section, s, d.Steel ? (Func<MasterLine, bool>)(l => AboveWall(l, wb)) : null, scale);
            });
        }

        static void Arrow(SteelDrawing.Writer w, double x0, double x1, double z, double size)
        {
            // the masters' opening arrow: an open head and a double shaft
            double s = Math.Sign(x1 - x0);
            w.Poly(new[] { new[] { x1, z }, new[] { x1 - s * size, z + size / 3 }, new[] { x1 - s * size, z - size / 3 } }, true);
            w.Seg(x0, z + size / 10, x1 - s * size, z + size / 10);
            w.Seg(x0, z - size / 10, x1 - s * size, z - size / 10);
            w.Seg(x0, z + size / 10, x0, z - size / 10);
        }

        static void SectionMark(SteelDrawing.Writer w, double y0, double y1)
        {
            w.Layer = SteelDrawing.LayerAnno;
            foreach (var y in new[] { y0, y1 })
            {
                double up = y1 > y0 && y == y1 ? 1 : -1;
                w.Seg(0, y, 0, y + up * 6 * Text);
                Arrow(w, 0, 3 * Text, y + up * 6 * Text, 1.4 * Text);
                w.Text(4.5 * Text, y + up * 6 * Text + 0.7 * Text, Text * 1.4, "C");
            }
        }

        static double[] DrawDoor(Database db, Transaction tr, DoorData d, double[] window, int scale)
        {
            var before = Existing(db, tr);
            var m = Masters();
            var master = m.Doors[d.MasterName];
            const double jamb = DoorDetailFields.FrameJambMm, head = 170;
            double W = d.OpeningMm, H = d.ClearHeightMm, S = d.StructuralMm;
            double frameHalf = W / 2 + jamb, frameTop = H + head;
            double half = Math.Max(S / 2, frameHalf + (d.Steel ? d.Beam.WidthMm : 0));
            double x0 = window[0] - DoorShift + 100 + 30 * Text + half;
            double y0 = window[1] + 14 * Text + 600;
            var w = WriterAt(db, tr, x0, y0);

            // ---- VIEW at D-D ----
            double strTop = frameTop + 50;
            if (d.Steel)
            {
                // the frame on the door-head beam and the jamb posts
                w.Layer = SteelDrawing.LayerBeam;
                double bb = Math.Max(d.HeadBeamBottomMm, frameTop), bh = d.Beam.HeightMm, bw = d.Beam.WidthMm;
                w.Rect(-frameHalf - bw - 150, bb, frameHalf + bw + 150, bb + bh);
                w.Rect(-frameHalf - bw, 0, -frameHalf, bb);
                w.Rect(frameHalf, 0, frameHalf + bw, bb);
                strTop = bb + bh;
                S = 2 * (frameHalf + bw);
            }
            else
            {
                // the structural opening behind the frame, dashed
                w.Layer = LayerWall;
                var str = w.Poly(new[] { new[] { -S / 2, 0.0 }, new[] { -S / 2, strTop }, new[] { S / 2, strTop }, new[] { S / 2, 0.0 } }, false);
                var dashed = Linetype(db, tr, "DASHED");
                if (!dashed.IsNull)
                {
                    str.LinetypeId = dashed;
                    str.LinetypeScale = scale * 0.5;
                }
            }
            w.Layer = LayerDoor;
            w.Insert(DoorElevationBlock(db, tr, d, scale), 0, 0, 1, 0);
            // the axis (CENTER) and the opening arrows of the automatic doors at the masters' height
            w.Layer = SteelDrawing.LayerAnno;
            var axis = w.Seg(0, -3 * Text, 0, strTop + Text);
            var center = Linetype(db, tr, "CENTER");
            if (!center.IsNull)
            {
                axis.LinetypeId = center;
                axis.LinetypeScale = scale * 0.5;
            }
            w.Layer = LayerDoor;
            double za = 860, aw = Math.Min(160, W / 5), ah = Math.Min(60, aw / 2.5);
            if (d.Type == DoorTypes.Automatic2C)
            {
                Arrow(w, -W / 4 + aw / 2, -W / 4 - aw / 2, za, ah);
                Arrow(w, W / 4 - aw / 2, W / 4 + aw / 2, za, ah);
            }
            else if (d.Type == DoorTypes.Automatic2S)
            {
                double s = d.StackRight ? 1 : -1;
                Arrow(w, -W / 4 - s * aw / 2, -W / 4 + s * aw / 2, za, ah);
                Arrow(w, W / 4 - s * aw / 2, W / 4 + s * aw / 2, za, ah);
            }
            // floor line, section mark
            w.Layer = SteelDrawing.LayerAnno;
            double floorL = -half - 300, floorR = half + 12 * Text;
            w.Seg(floorL, 0, floorR, 0);
            SectionMark(w, -2 * Text, strTop + 13 * Text);
            // dimensions: the opening, the structural opening (the masters' wording), the offset from the shaft centre
            double top = strTop + 2 * Text;
            Chain(w, null, new[] { -W / 2, W / 2 }, new[] { N(W) }, top + 3 * Text, top, top, true);
            Dim(w, -S / 2, strTop, S / 2, strTop, top + 7 * Text, 0, d.Automatic ? "STR. " + N(S) : N(S));
            if (Math.Abs(d.OffsetMm) > 0.5)
            {
                w.Layer = SteelDrawing.LayerAnno;
                w.Seg(-d.OffsetMm, -2 * Text, -d.OffsetMm, H / 3);
                Chain(w, null, new[] { Math.Min(0, -d.OffsetMm), Math.Max(0, -d.OffsetMm) }, new[] { N(Math.Abs(d.OffsetMm)) }, -6 * Text, 0, 0, false);
            }
            // labels: INDICATOR on its jamb (the masters: 69 x 300 at 907, 30 outside the opening), FINISH FLOOR LEVEL
            w.Layer = SteelDrawing.LayerAnno;
            w.Label(half + 3 * Text, 0, half + 9 * Text, 5 * Text, Text, "FINISH FLOOR LEVEL");
            var right = new List<Call>();
            var left = new List<Call>();
            if (d.Automatic)
            {
                double ix = W / 2 + 30 + 34.5;
                var ind = new Call { Ax = (d.Mirror ? -1 : 1) * ix, Ay = 1000, Text = "INDICATOR" };
                (d.Mirror ? left : right).Insert(0, ind);
            }
            if (d.Steel)
                left.Add(new Call { Ax = -frameHalf - d.Beam.WidthMm - 75, Ay = Math.Max(d.HeadBeamBottomMm, frameTop) + d.Beam.HeightMm / 2, Text = "DOOR HEAD BEAM " + d.Beam.Name });
            Column(w, right, half + 6 * Text, false);
            if (left.Count > 0)
                Column(w, left, -half - 6 * Text, true);

            // ---- SEC. C-C (landing at the left, shaft at the right; the masters' section) ----
            double xs = half + 45 * Text + 300;
            var v = WriterAt(db, tr, x0 + xs, y0);
            double wb = WallBottom(master.Section), hdr = Math.Round(wb - m.HeightMm), dH = H - m.HeightMm;
            v.Layer = LayerDoor;
            v.Insert(DoorSectionBlock(db, tr, d, scale), 0, 0, 1, 0);
            if (d.Steel)
            {
                // the door-head beam over the header, its web at the shaft face of the master wall
                v.Layer = SteelDrawing.LayerBeam;
                double bb = Math.Max(d.HeadBeamBottomMm, H + hdr);
                v.Rect(488 - d.Beam.WidthMm, bb, 488, bb + d.Beam.HeightMm);
                Column(v, new List<Call> { new Call { Ax = 488 - d.Beam.WidthMm / 2, Ay = bb + d.Beam.HeightMm / 2, Text = "DOOR HEAD BEAM " + d.Beam.Name } }, -8 * Text, true);
            }
            v.Layer = SteelDrawing.LayerAnno;
            v.Seg(-60, 0, 0, 0);
            // dimensions: ENTRANCE HEIGHT, the header, the sill (the masters' positions)
            double sill = -Math.Round(-SillDepth(master.Section) / 10) * 10, right0 = SectionRight(master.Section);
            Dim(v, 0, 0, 0, H, -3 * Text, 90, "ENTRANCE HEIGHT " + N(H));
            Dim(v, 0, H, 0, H + hdr, -3 * Text, 90, "");
            Dim(v, right0, sill, right0, 0, right0 + 4 * Text, 90, "");
            // the two titles under their views
            foreach (var t in FindTitles(db, tr, window, "D-D"))
            {
                t.UpgradeOpen();
                t.Location = new Point3d(x0 - half, t.Location.Y, 0);
            }
            foreach (var t in FindTitles(db, tr, window, "C-C"))
            {
                t.UpgradeOpen();
                t.Location = new Point3d(x0 + xs - 100, t.Location.Y, 0);
            }
            return Extents(db, tr, before);
        }

        // The bottom of the sill recess (the masters' 60 dimension): the lowest short horizontal line just below the floor.
        static double SillDepth(MasterView s)
        {
            double best = 0;
            foreach (var l in s.Lines)
            {
                for (int i = 0; i + 1 < l.Pts.Count; i++)
                {
                    var a = l.Pts[i];
                    var b = l.Pts[i + 1];
                    if (Math.Abs(a[1] - b[1]) < 0.6 && a[1] < -40 && a[1] > -80 && Math.Max(a[0], b[0]) > 500)
                        best = Math.Min(best, a[1]);
                }
            }
            return best == 0 ? -60 : best;
        }

        static double SectionRight(MasterView s)
        {
            return s.Lines.SelectMany(l => l.Pts).Where(p => p[1] > -80 && p[1] < 100).Select(p => p[0]).DefaultIfEmpty(600).Max();
        }
    }
}
