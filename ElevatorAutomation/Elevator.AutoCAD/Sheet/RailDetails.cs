using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionAA;
using Elevator.AutoCAD.Steel;
using Elevator.Core.Geometry;
using Elevator.Core.GuideRails;
using Elevator.Core.Layouts;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Steel;

namespace Elevator.AutoCAD.Sheet
{
    /// <summary>
    /// The two rail details of the A0 sheet (stage G1): the plan of each rail with its bracket (RailsPlan, "CAR GUIDES AND BRACKETS" and
    /// "C.W. GUIDES AND BRACKETS") and the front elevation of each rail with its brackets at the real spacing (RailBrackets). They are
    /// drawn at full size from the project (profiles, brackets of the RAILS page, spacings) in place of the static template details:
    /// the template entities of the two windows are hidden and their titles kept. The drawing follows principle 11: named blocks for
    /// the brackets, S- and A- layers, multileaders and real dimensions. The new entities carry no key (they are details of the sheet).
    /// A concrete shaft shows the anchors in the concrete wall; a steel shaft shows the bracket bolted to the UPN beam.
    /// </summary>
    static class RailDetails
    {
        public const string PlanDetail = "RAILS_PLAN_CW5_CAR9";
        public const string BracketsDetail = "RAIL_BRACKETS_CW5_CAR9";
        public const string LayerRail = "S-RAIL";
        public const string LayerBracket = "S-BRACKET";
        public const string LayerClip = "S-CLIP";
        public const string LayerAnchor = "S-ANCHOR";
        public const string LayerWall = "A-WALL";
        const string Prefix = "Details.Rail.";
        // model height of the labels and dimension texts (3.5 mm printed at 1:10)
        // text height of the labels and dimensions: 3.5 mm printed at the scale the detail takes on the sheet (set by Draw)
        static double Text = 35;
        static double K
        {
            get { return Text / 35.0; }
        }
        const double Margin = 120;
        // the elevation is drawn well left of the template strip: its title is as wide as the viewport at 1:50 and would take in the door detail
        const double ElevationShift = 60000;

        sealed class Rail
        {
            public string Family;       // Car, CW
            public string Title;        // CAR, C.W.
            public RailProfile Profile;
            public RailBracketPreset Bracket;
            public double SpacingMm;
            // derived plate span
            public double PlateWidth { get { return Profile.WidthMm + 2 * Bracket.ClipWidthMm; } }
        }

        sealed class Context
        {
            public bool Steel;
            public ChannelProfile Beam;
            public Rail Car, Cw;
        }

        /// <summary>
        /// Draws the two details at the text height of the scale each takes on the sheet, in place of the template's; Draw again when the
        /// arrangement gave a detail another scale (the previous drawing is erased). Active is false when the drawing holds no project or
        /// its rails are unknown: the template details stay.
        /// </summary>
        public sealed class Drawer
        {
            readonly Database db;
            readonly SheetDetailSpec plan, brackets;
            readonly double[] oldPlan, oldBrackets;
            readonly List<ObjectId> made = new List<ObjectId>();
            bool hidden;
            int planScale, bracketsScale;

            public Drawer(Database db, Dictionary<string, SheetDetailSpec> chosen)
            {
                this.db = db;
                SheetDetailSpec p, b;
                if (chosen.TryGetValue(PlanDetail, out p) && chosen.TryGetValue(BracketsDetail, out b) && p.Window != null && b.Window != null)
                {
                    plan = p;
                    brackets = b;
                    oldPlan = (double[])p.Window.Clone();
                    oldBrackets = (double[])b.Window.Clone();
                }
            }

            public bool Active { get; private set; }

            /// <summary>The scales the two details were last drawn for (plan, brackets).</summary>
            public string Key
            {
                get { return planScale + "/" + bracketsScale; }
            }

            public bool Draw(int planScale, int bracketsScale)
            {
                if (plan == null)
                    return false;
                if (Active && planScale == this.planScale && bracketsScale == this.bracketsScale)
                    return true;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ctx = Read(db, tr);
                    if (ctx == null)
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
                        // the strip of the two details up to where the door detail starts: the unused rail details of the template
                        // stand there and would show in the larger viewports
                        HideTemplate(db, tr, oldPlan, oldBrackets[0] - 1);
                        HideTemplate(db, tr, oldBrackets, oldBrackets[0] + 8000);
                        hidden = true;
                    }
                    var before = Existing(db, tr);
                    Text = 3.0 * Math.Max(planScale, 1);
                    TitleWindow = (double[])plan.Window.Clone();
                    var box = DrawPlan(db, tr, ctx, oldPlan);
                    plan.Window = new[] { oldPlan[0], oldPlan[1], Math.Max(oldPlan[2], box[2] + Margin), Math.Max(oldPlan[3], box[3] + Margin) };
                    Text = 3.0 * Math.Max(bracketsScale, 1);
                    TitleWindow = (double[])brackets.Window.Clone();
                    box = DrawElevations(db, tr, ctx, oldBrackets);
                    brackets.Window = new[] { oldBrackets[0] - ElevationShift, oldBrackets[1], box[2] + Margin, Math.Max(oldBrackets[3], box[3] + Margin) };
                    Untag(db, tr);
                    var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                    foreach (ObjectId id in model)
                    {
                        if (!before.Contains(id))
                            made.Add(id);
                    }
                    tr.Commit();
                }
                this.planScale = planScale;
                this.bracketsScale = bracketsScale;
                Active = true;
                return true;
            }
        }

        static Context Read(Database db, Transaction tr)
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
            var pc = new PresetContext { Project = project, Library = lib };
            var catalog = lib.GuideRailsOrDefault();
            Func<string, string, string, Rail> rail = (family, title, profileKey) =>
            {
                var name = ProjectPresets.Find(profileKey).Effective(pc) as string;
                var profile = name == null ? null : catalog.Profile(name);
                var bracket = profile == null ? null : RailBracketFields.Effective(project, lib, family);
                if (profile == null || bracket == null)
                    return null;
                var spacingKey = family == RailBracketFields.Car ? ProjectPresets.CarSpacingKey : ProjectPresets.CwSpacingKey;
                object s = ProjectPresets.Find(spacingKey).Effective(pc);
                double spacing = s == null ? 0 : Convert.ToDouble(s, CultureInfo.InvariantCulture);
                if (spacing <= 0)
                    return null;
                return new Rail { Family = family, Title = title, Profile = profile, Bracket = bracket, SpacingMm = spacing };
            };
            var c = new Context
            {
                Car = rail(RailBracketFields.Car, "CAR", ProjectPresets.CarProfileKey),
                Cw = rail(RailBracketFields.Cw, "C.W.", ProjectPresets.CwProfileKey)
            };
            if (c.Car == null || c.Cw == null)
                return null;
            c.Steel = SteelFields.IsSteel(project, lib);
            if (c.Steel)
            {
                var steel = SteelStructure.Build(project, lib);
                if (steel == null || !steel.Valid || steel.Inputs == null || steel.Inputs.Beam == null)
                    c.Steel = false;
                else
                    c.Beam = steel.Inputs.Beam;
            }
            return c;
        }

        // The template entities of a window go off; its titles (underlined texts) stay.
        static void HideTemplate(Database db, Transaction tr, double[] w, double xTo)
        {
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null || !e.Visible || SectionAADrawing.Tag(e) != null)
                    continue;
                var mt = e as MText;
                if (mt != null && mt.Contents.Contains("\\L") && mt.Location.X >= w[0] - 1 && mt.Location.X <= w[2] + 1)
                    continue;
                Extents3d box;
                try
                {
                    box = e.GeometricExtents;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }
                double ax = mt != null ? mt.Location.X : box.MinPoint.X, ay = mt != null ? mt.Location.Y : box.MaxPoint.Y;
                if (ax < w[0] - 1 || ax > xTo || ay < w[1] - 1500 || ay > w[3] + 4000)
                    continue;
                e.UpgradeOpen();
                e.Visible = false;
            }
        }

        // The entities of this file carry a key only to be written by the steel writer; the sheet details are untagged like the template's.
        static void Untag(Database db, Transaction tr)
        {
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null)
                    continue;
                var tag = SectionAADrawing.Tag(e);
                if (tag == null || tag.Length == 0 || !tag[0].StartsWith(Prefix, StringComparison.Ordinal))
                    continue;
                e.UpgradeOpen();
                e.XData = new ResultBuffer(new TypedValue(1001, DrawingReader.RegApp));
            }
        }

        static double[] Union(double[] a, double[] b)
        {
            return a == null ? b : new[] { Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Max(a[2], b[2]), Math.Max(a[3], b[3]) };
        }

        // The extents of the entities written with the prefix since the count before.
        static double[] Extents(Database db, Transaction tr, HashSet<ObjectId> before)
        {
            double[] box = null;
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                if (before.Contains(id))
                    continue;
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null || !e.Visible)
                    continue;
                try
                {
                    var b = e.GeometricExtents;
                    box = Union(box, new[] { b.MinPoint.X, b.MinPoint.Y, b.MaxPoint.X, b.MaxPoint.Y });
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                }
            }
            return box ?? new double[4];
        }

        static HashSet<ObjectId> Existing(Database db, Transaction tr)
        {
            var set = new HashSet<ObjectId>();
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
                set.Add(id);
            return set;
        }

        static double curOx, curOy;
        // the window of the detail as last drawn (its titles may stand further right than the template's window)
        static double[] TitleWindow;

        static SteelDrawing.Writer WriterAt(Database db, Transaction tr, double ox, double oy)
        {
            curOx = ox;
            curOy = oy;
            return new SteelDrawing.Writer(db, tr, Prefix, "railDetail", (u, v) => new Point3d(ox + u, oy + v, 0));
        }

        static string N(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // ---------- the rail section and the bracket as named blocks ----------
        static string Key(Rail r)
        {
            var p = r.Profile;
            var b = r.Bracket;
            return SteelDrawing.Clean(p.Name) + "_" + N(b.AngleLegAMm) + "x" + N(b.AngleLegBMm) + "x" + N(b.AngleThicknessMm) + "_P" + N(b.PlateThicknessMm)
                + "_C" + N(b.ClipLengthMm) + "x" + N(b.ClipWidthMm) + "x" + N(b.ClipThicknessMm) + "_B" + N(b.ClipBoltDiameterMm);
        }

        static void Poly(Database db, Transaction tr, BlockTableRecord btr, IEnumerable<double[]> pts, bool closed, string layer)
        {
            var pl = new Polyline();
            int i = 0;
            foreach (var p in pts)
                pl.AddVertexAt(i++, new Point2d(p[0], p[1]), 0, 0, 0);
            pl.Closed = closed;
            SteelDrawing.Put(btr, tr, pl, layer);
        }

        static void RectIn(Database db, Transaction tr, BlockTableRecord btr, double x0, double y0, double x1, double y1, string layer)
        {
            Poly(db, tr, btr, new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } }, true, layer);
        }

        static double HeadHeight(RailProfile p)
        {
            return p.BladeMm > p.NeckMm + 1e-9 ? Math.Min(34, Math.Max(p.HeightMm - p.FootMm - 10, 5)) : 0;
        }

        // The rail section in plan: back (the flange) at x = 0, blade tip at x = h1, centred on y = 0. A T-profile: flange b1 x f, neck c,
        // and the head k wide for the machined rails (a flat T for the 5 mm rail).
        static ObjectId RailBlock(Database db, Transaction tr, RailProfile p)
        {
            string name = "SM_RAIL_" + SteelDrawing.Clean(p.Name);
            return SteelDrawing.Block(db, tr, name, btr =>
            {
                double b = p.WidthMm / 2, f = p.FootMm, c = p.NeckMm / 2, k = p.BladeMm / 2, h = p.HeightMm, n = HeadHeight(p);
                var pts = new List<double[]> { new[] { 0.0, -b }, new[] { f, -b }, new[] { f, -c } };
                if (n > 0)
                {
                    pts.Add(new[] { h - n, -c });
                    pts.Add(new[] { h - n, -k });
                    pts.Add(new[] { h, -k });
                    pts.Add(new[] { h, k });
                    pts.Add(new[] { h - n, k });
                    pts.Add(new[] { h - n, c });
                }
                else
                {
                    pts.Add(new[] { h, -k });
                    pts.Add(new[] { h, k });
                }
                pts.Add(new[] { f, c });
                pts.Add(new[] { f, b });
                pts.Add(new[] { 0.0, b });
                Poly(db, tr, btr, pts, true, LayerRail);
            });
        }

        // The bracket in plan: wall face at x = 0, rail axis at y = 0. Two angles (leg A on the wall, leg B towards the rail), the plate across
        // the ends of the legs B, the two clips with their bolts. The rail foot sits on the plate: its back at x = legB + plate.
        static ObjectId BracketPlanBlock(Database db, Transaction tr, Rail r)
        {
            return SteelDrawing.Block(db, tr, "SM_RAIL_BRACKET_PLAN_" + Key(r), btr =>
            {
                var b = r.Bracket;
                double t = b.AngleThicknessMm, a = b.AngleLegAMm, lb = b.AngleLegBMm, pt = b.PlateThicknessMm, w = r.PlateWidth / 2;
                double xr = lb + pt, bw = r.Profile.WidthMm / 2, f = r.Profile.FootMm;
                foreach (int s in new[] { -1, 1 })
                {
                    // the angle: the L of leg A (on the wall) and leg B (towards the rail), corner at (0, s*w)
                    double y0 = s * w, yo = s * (w + a - t), yi = s * (w - t);
                    Poly(db, tr, btr, new[]
                    {
                        new[] { 0.0, yo }, new[] { t, yo }, new[] { t, yi }, new[] { lb, yi }, new[] { lb, y0 }, new[] { 0.0, y0 }
                    }, true, LayerBracket);
                    // the clip: a strap on the plate, a riser against the foot edge and a lip over its front face
                    double yf = s * bw, yc = s * (bw + b.ClipThicknessMm), yb = s * (bw + b.ClipWidthMm - 8), yl = s * (bw - 8);
                    double ct = b.ClipThicknessMm;
                    Poly(db, tr, btr, new[]
                    {
                        new[] { xr, yf }, new[] { xr, yb }, new[] { xr + ct, yb }, new[] { xr + ct, yc }, new[] { xr + f + ct, yc },
                        new[] { xr + f + ct, yl }, new[] { xr + f, yl }, new[] { xr + f, yf }
                    }, true, LayerClip);
                    // the bolt through the strap and the plate: head on the strap, nut behind the plate
                    double ybolt = s * ((bw + ct + (bw + b.ClipWidthMm - 8)) / 2), d = b.ClipBoltDiameterMm;
                    double head = 0.65 * d, nut = 0.8 * d;
                    RectIn(db, tr, btr, xr + ct, ybolt - d * 0.8, xr + ct + head, ybolt + d * 0.8, LayerClip);
                    RectIn(db, tr, btr, xr - pt - nut, ybolt - d * 0.8, xr - pt, ybolt + d * 0.8, LayerClip);
                    RectIn(db, tr, btr, xr - pt - nut, ybolt - d / 2, xr + ct + head, ybolt + d / 2, LayerClip);
                }
                RectIn(db, tr, btr, lb, -w, xr, w, LayerBracket);
            });
        }

        // ---------- the plan (RailsPlan) ----------
        static double[] DrawPlan(Database db, Transaction tr, Context c, double[] window)
        {
            var before = Existing(db, tr);
            // the car plan at the left, the counterweight plan at the right, as the template's titles stand
            var titles = FindTitles(db, tr, window, "GUIDES AND BRACKETS");
            double x0 = window[0] + 160 * K;
            double y0 = window[1] + 1000 * K;
            double step = 0;
            var ob = new SteelDrawing.Obstacles();
            double[] xs = new double[2];
            int i = 0;
            foreach (var r in new[] { c.Car, c.Cw })
            {
                double span = PlanWidth(c, r);
                xs[i] = x0 + step;
                DrawPlanOf(db, tr, c, r, xs[i], y0);
                // the two titles stand side by side under the plans: about 140 model mm per scale unit each
                step += Math.Max(span + 60 * K, 140 * Text / 3.0 + 150);
                i++;
            }
            // each title under its plan, left aligned with the wall
            foreach (var t in titles)
            {
                string text = t.Contents ?? "";
                bool car = text.IndexOf("CAR", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("C.W.", StringComparison.OrdinalIgnoreCase) < 0;
                t.UpgradeOpen();
                t.Location = new Point3d(xs[car ? 0 : 1] - 120, t.Location.Y, 0);
            }
            return Extents(db, tr, before);
        }

        static List<MText> FindTitles(Database db, Transaction tr, double[] w, string contains)
        {
            var list = new List<MText>();
            if (TitleWindow != null)
                w = new[] { Math.Min(w[0], TitleWindow[0]), Math.Min(w[1], TitleWindow[1]), Math.Max(w[2], TitleWindow[2]), Math.Max(w[3], TitleWindow[3]) };
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var mt = tr.GetObject(id, OpenMode.ForRead) as MText;
                if (mt == null || !mt.Visible || SectionAADrawing.Tag(mt) != null || !mt.Contents.Contains("\\L") || !mt.Contents.Contains(contains))
                    continue;
                var p = mt.Location;
                if (p.X >= w[0] && p.X <= w[2] && p.Y >= w[1] && p.Y <= w[3])
                    list.Add(mt);
            }
            return list;
        }

        // width of one plan: wall, bracket, rail, and room for the labels to its right
        static double PlanWidth(Context c, Rail r)
        {
            double wall = 160;
            double depth = r.Bracket.AngleLegBMm + r.Bracket.PlateThicknessMm + r.Profile.HeightMm;
            return wall + depth + 430 * K;
        }

        static void DrawPlanOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            var ob = new SteelDrawing.Obstacles();
            double half = r.PlateWidth / 2 + b.AngleLegAMm;
            double wall = 140;
            // the support: the concrete wall, or the UPN beam seen from above
            double beamB = c.Steel ? c.Beam.WidthMm : 0;
            if (c.Steel)
            {
                w.Layer = SteelDrawing.LayerBeam;
                var beam = w.Rect(-beamB, -half - 60, 0, half + 60);
                ob.AddOutline(-beamB, -half - 60, 0, half + 60);
                w.Layer = SteelDrawing.LayerBeam;
                w.Fill(beam, "SOLID", 1);
            }
            else
            {
                w.Layer = LayerWall;
                var wallBox = w.Rect(-wall, -half - 60, 0, half + 60);
                ob.AddOutline(-wall, -half - 60, 0, half + 60);
                w.Fill(wallBox, "ANSI31", 150);
            }
            // the bracket and the rail as blocks
            double xr = b.AngleLegBMm + b.PlateThicknessMm;
            w.Layer = LayerBracket;
            w.Insert(BracketPlanBlock(db, tr, r), 0, 0, 1, 0);
            w.Layer = LayerRail;
            w.Insert(RailBlock(db, tr, r.Profile), xr, 0, 1, 0);
            // the fixing: anchors in the wall (dashed, shown as their axes) or bolts through the web of the beam
            double fixA = r.Bracket.AnchorDiameterMm;
            w.Layer = LayerAnchor;
            foreach (int s in new[] { -1, 1 })
            {
                double y = s * (r.PlateWidth / 2 + b.AngleLegAMm / 2 - b.AngleThicknessMm / 2);
                if (c.Steel)
                {
                    // bolt through the web of the beam and the leg A, head on the beam inside face
                    w.Rect(-beamB, y - fixA / 2, b.AngleThicknessMm, y + fixA / 2);
                }
                else
                {
                    w.Rect(-b.AnchorLengthMm * 0 - wall + 10, y - fixA / 2, b.AngleThicknessMm, y + fixA / 2);
                }
            }
            // dimension chain along x above the bracket: leg B, plate, rail height
            double top = half + 60;
            var dimRow = top + 80 * K;
            var xs = new[] { 0.0, b.AngleLegBMm, xr, xr + r.Profile.HeightMm };
            var texts = new[] { N(b.AngleLegBMm), N(b.PlateThicknessMm), N(r.Profile.HeightMm) };
            Chain(w, ob, xs, texts, dimRow, 0, top, true);
            // the width of the plate at the foot of the plan
            double bottom = -half - 60;
            Single(w, ob, -r.PlateWidth / 2, r.PlateWidth / 2, xr + r.Profile.HeightMm + 90 * K, N(r.PlateWidth), false, 0);
            // labels: plain names with their sizes
            string angle = "ANGLE L" + N(b.AngleLegAMm) + "x" + N(b.AngleLegBMm) + "x" + N(b.AngleThicknessMm);
            string plate = "PLATE " + N(b.PlateThicknessMm);
            string clip = "CLIP " + N(b.ClipLengthMm) + "x" + N(b.ClipWidthMm) + "x" + N(b.ClipThicknessMm);
            string bolt = N(b.ClipBoltCount) + " BOLTS M" + N(b.ClipBoltDiameterMm);
            string rail = "RAIL " + r.Profile.Name;
            string fix = c.Steel ? "BOLT M" + N(fixA) : "ANCHOR D" + N(fixA) + "x" + N(b.AnchorLengthMm);
            string support = c.Steel ? "BEAM " + c.Beam.Name : "WALL";
            double y1 = r.PlateWidth / 2;
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { b.AngleLegBMm / 2, half - b.AngleThicknessMm / 2, 100.0 } }, false, Text, angle, 150 * K, 150 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { b.AngleLegBMm + b.PlateThicknessMm / 2, y1 / 2 } }, false, Text, plate, 250 * K, 250 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { xr + r.Profile.FootMm + b.ClipThicknessMm / 2, -(r.Profile.WidthMm / 2 + b.ClipWidthMm / 2) } }, false, Text, clip, 250 * K, -250 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { xr + r.Profile.HeightMm, 0.0 } }, false, Text, rail, 120 * K, 0);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { xr, -(r.Profile.WidthMm / 2 + b.ClipWidthMm - 14) } }, false, Text, bolt, 300 * K, -420 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { -wall / 2, -half - 30 } }, false, Text, support, 120 * K, -120 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { b.AngleThicknessMm / 2, -half + 20, 100.0 } }, false, Text, fix, 200 * K, -250 * K);
            w.Layer = SteelDrawing.LayerAnno;
        }

        // A chain of horizontal dimensions along local x on the row y; the text of an interval that does not fit goes outside the chain or
        // beside it (DimensionLayout). The rail is not lying on the row.
        static void Chain(SteelDrawing.Writer w, SteelDrawing.Obstacles ob, double[] xs, string[] texts, double row, double yFrom, double yTop, bool above)
        {
            var placement = DimensionLayout.PlaceChainTexts(xs, texts, Text, Text * 0.8, new List<double[]>());
            for (int i = 0; i < texts.Length; i++)
            {
                var p = placement[i];
                double mid = (xs[i] + xs[i + 1]) / 2;
                double lat = p.Slot == DimensionTextSlot.Beside ? p.LateralMm : 0;
                var d = Dim(w, xs[i], yTop, xs[i + 1], yTop, row, 0);
                if (p.Slot != DimensionTextSlot.Centred)
                {
                    d.TextPosition = WorldOf(w, mid + p.AlongMm, row + (above ? 1 : -1) * (Text * 0.9 + lat));
                    d.UsingDefaultTextPosition = false;
                }
                ob.AddText(new[] { mid + p.AlongMm - texts[i].Length * 0.4 * Text, row + Text * 0.2, mid + p.AlongMm + texts[i].Length * 0.4 * Text, row + Text * 1.6 });
                ob.AddSegment(xs[i], yTop, xs[i], row);
                ob.AddSegment(xs[i + 1], yTop, xs[i + 1], row);
            }
        }

        static Point3d WorldOf(SteelDrawing.Writer w, double u, double v)
        {
            return new Point3d(curOx + u, curOy + v, 0);
        }

        // One dimension between two local x (horizontal=false: along y between two y at column x).
        static void Single(SteelDrawing.Writer w, SteelDrawing.Obstacles ob, double a, double b, double at, string text, bool horizontal, double dummy)
        {
            w.Layer = SteelDrawing.LayerDims;
            var d = Dim(w, at - 20, a, at - 20, b, at, 90);
            ob.AddText(new[] { at, Math.Min(a, b), at + Text * 2.2, Math.Max(a, b) });
            ob.AddSegment(at - 20, a, at, a);
            ob.AddSegment(at - 20, b, at, b);
            ob.AddSegment(at, a, at, b);
            w.Layer = SteelDrawing.LayerAnno;
        }

        // A rotated dimension through the writer: (u1, v1) and (u2, v2) the extension points, the dimension line through (lineU or
        // lineV); rotation 0 = horizontal measure along x, 90 = along y.
        static RotatedDimension Dim(SteelDrawing.Writer w, double u1, double v1, double u2, double v2, double line, double rotationDeg)
        {
            w.Layer = SteelDrawing.LayerDims;
            Point3d p1 = WorldOf(w, u1, v1), p2 = WorldOf(w, u2, v2);
            Point3d dl = rotationDeg == 0 ? WorldOf(w, (u1 + u2) / 2, line) : WorldOf(w, line, (v1 + v2) / 2);
            var d = new RotatedDimension(rotationDeg * Math.PI / 180, p1, p2, dl, "", w.Database.Dimstyle);
            d.SetDatabaseDefaults(w.Database);
            d.Dimtxt = Text;
            d.Dimasz = Text * 0.7;
            d.Dimexo = 15;
            d.Dimexe = 15;
            d.Dimgap = Text * 0.25;
            d.Dimdec = 0;
            d.Dimtad = 1;
            d.Dimscale = 1;
            d.UsingDefaultTextPosition = true;
            w.Adopt(d);
            return d;
        }

        // ---------- the elevations (RailBrackets) ----------
        static ObjectId BracketElevationBlock(Database db, Transaction tr, Context c, Rail r)
        {
            return SteelDrawing.Block(db, tr, "SM_RAIL_BRACKET_ELEV_" + Key(r) + (c.Steel ? "_BOLT" : "_ANCHOR"), btr =>
            {
                var b = r.Bracket;
                double w = r.PlateWidth / 2, len = b.AngleLengthMm / 2, a = b.AngleLegAMm, t = b.AngleThicknessMm;
                double bw = r.Profile.WidthMm / 2, f = r.Profile.FootMm;
                // plate and the two angles behind it (leg A outside the plate)
                RectIn(db, tr, btr, -w, -len, w, len, LayerBracket);
                foreach (int s in new[] { -1, 1 })
                {
                    double y0 = s * (w - t), y1 = s * (w + a - t);
                    RectIn(db, tr, btr, Math.Min(y0, y1), -len, Math.Max(y0, y1), len, LayerBracket);
                    // the clip on the foot edge with its two bolts
                    double cy0 = s * (bw - 8), cy1 = s * (bw + b.ClipWidthMm - 8);
                    RectIn(db, tr, btr, Math.Min(cy0, cy1), -b.ClipLengthMm / 2, Math.Max(cy0, cy1), b.ClipLengthMm / 2, LayerClip);
                    double bx = s * ((bw + b.ClipThicknessMm + bw + b.ClipWidthMm - 8) / 2);
                    foreach (int k in new[] { -1, 1 })
                    {
                        var circle = new Circle(new Point3d(bx, k * b.ClipLengthMm / 4, 0), Vector3d.ZAxis, b.ClipBoltDiameterMm / 2);
                        SteelDrawing.Put(btr, tr, circle, LayerClip);
                    }
                    // the fixing on the leg A: two anchors (concrete) or two bolts (beam)
                    double fx = s * (w + a / 2 - t / 2);
                    double fz = c.Steel ? Math.Min(len - 30, c.Beam.HeightMm / 2 - 25) : len - 35;
                    foreach (int k in new[] { -1, 1 })
                    {
                        var circle = new Circle(new Point3d(fx, k * fz, 0), Vector3d.ZAxis, b.AnchorDiameterMm / 2);
                        SteelDrawing.Put(btr, tr, circle, LayerAnchor);
                    }
                }
                // the foot of the rail seen through the plate is part of the rail, not of the bracket
            });
        }

        static double[] DrawElevations(Database db, Transaction tr, Context c, double[] window)
        {
            var before = Existing(db, tr);
            double x0 = window[0] + 560 * K - ElevationShift;
            double y0 = window[1] + 1000 * K;
            double span = 0;
            var rails = new[] { c.Cw, c.Car };
            double[] centres = new double[2];
            for (int i = 0; i < 2; i++)
            {
                var r = rails[i];
                double width = r.PlateWidth + 2 * r.Bracket.AngleLegAMm;
                double ox = x0 + span + width / 2 + 420 * K;
                centres[i] = ox;
                DrawElevationOf(db, tr, c, r, ox, y0);
                span += width + 1100 * K;
            }
            var titles = FindTitles(db, tr, window, "Anchor Details");
            foreach (var t in titles)
            {
                t.UpgradeOpen();
                t.Location = new Point3d((centres[0] + centres[1]) / 2, t.Location.Y, 0);
            }
            return Extents(db, tr, before);
        }

        static void DrawElevationOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            var ob = new SteelDrawing.Obstacles();
            double s = r.SpacingMm, len = b.AngleLengthMm;
            double half = r.PlateWidth / 2 + b.AngleLegAMm;
            double bw = r.Profile.WidthMm / 2, k = r.Profile.BladeMm / 2;
            double zLow = 0, zHigh = s;
            double overrun = 200 * Math.Max(K, 1);
            // the beams of a steel shaft behind the brackets (UPN, the section height shown)
            if (c.Steel)
            {
                w.Layer = SteelDrawing.LayerBeam;
                foreach (double z in new[] { zLow, zHigh })
                {
                    w.Rect(-half - 60, z - c.Beam.HeightMm / 2, half + 60, z + c.Beam.HeightMm / 2);
                    ob.AddOutline(-half - 60, z - c.Beam.HeightMm / 2, half + 60, z + c.Beam.HeightMm / 2);
                }
            }
            // the rail: its foot and its blade, open ends
            w.Layer = LayerRail;
            double bottom = zLow - len / 2 - overrun, top = zHigh + len / 2 + overrun;
            w.Seg(-bw, bottom, -bw, top);
            w.Seg(bw, bottom, bw, top);
            w.Seg(-k, bottom, -k, top);
            w.Seg(k, bottom, k, top);
            ob.AddSegment(-bw, bottom, -bw, top);
            ob.AddSegment(bw, bottom, bw, top);
            // the brackets: one named block at each level
            var block = BracketElevationBlock(db, tr, c, r);
            foreach (double z in new[] { zLow, zHigh })
            {
                w.Layer = LayerBracket;
                w.Insert(block, 0, z, 1, 0);
                ob.AddOutline(-r.PlateWidth / 2, z - len / 2, r.PlateWidth / 2, z + len / 2);
                ob.AddOutline(-half, z - len / 2, half, z + len / 2);
            }
            // the spacing, real value: one vertical dimension between the two bracket centres
            double dimX = -half - 150 * K;
            w.Layer = SteelDrawing.LayerDims;
            var d = Dim(w, -half - 30, zLow, -half - 30, zHigh, dimX, 90);
            ob.AddSegment(dimX, zLow, dimX, zHigh);
            ob.AddText(new[] { dimX - Text * 1.6, (zLow + zHigh) / 2 - Text * 2.5, dimX, (zLow + zHigh) / 2 + Text * 2.5 });
            // the length of one bracket
            double dimR = half + 150 * K;
            Dim(w, half + 30, zHigh - len / 2, half + 30, zHigh + len / 2, dimR, 90);
            ob.AddSegment(dimR, zHigh - len / 2, dimR, zHigh + len / 2);
            ob.AddText(new[] { dimR - Text * 1.6, zHigh - Text * 2.5, dimR, zHigh + Text * 2.5 });
            // the width chain above the upper bracket: leg A, plate, leg A
            double row = zHigh + len / 2 + overrun + 100 * K;
            var xs = new[] { -half, -r.PlateWidth / 2, r.PlateWidth / 2, half };
            Chain(w, ob, xs, new[] { N(b.AngleLegAMm - b.AngleThicknessMm), N(r.PlateWidth), N(b.AngleLegAMm - b.AngleThicknessMm) }, row, zHigh + len / 2, true ? zHigh + len / 2 : 0, true);
            // title of the rail and the labels
            w.Layer = SteelDrawing.LayerAnno;
            string head = r.Title + " RAIL " + r.Profile.Name;
            double ty = row + 220 * K;
            w.Text(-half, ty + 2 * Text, Text * 1.2, head);
            ob.AddText(new[] { -half, ty, -half + head.Length * Text * 1.2, ty + 2 * Text });
            string angle = "ANGLE L" + N(b.AngleLegAMm) + "x" + N(b.AngleLegBMm) + "x" + N(b.AngleThicknessMm) + "";
            string clip = "CLIP " + N(b.ClipLengthMm) + "x" + N(b.ClipWidthMm) + "x" + N(b.ClipThicknessMm);
            string bolt = N(b.ClipBoltCount) + " BOLTS M" + N(b.ClipBoltDiameterMm);
            string plate = "PLATE " + N(r.PlateWidth) + "x" + N(len) + "x" + N(b.PlateThicknessMm);
            string fix = c.Steel ? "BOLT M" + N(b.AnchorDiameterMm) : "ANCHOR D" + N(b.AnchorDiameterMm) + "x" + N(b.AnchorLengthMm);
            string rail = "RAIL " + r.Profile.Name;
            double fz = c.Steel ? Math.Min(len / 2 - 30, c.Beam.HeightMm / 2 - 25) : len / 2 - 35;
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { half - b.AngleLegAMm / 2, zHigh + len / 2 - 5 } }, false, Text, angle, 200 * K, 130 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { r.PlateWidth / 2 - 4, zLow + len / 2 - 10 } }, false, Text, plate, 380 * K, 200 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { bw + b.ClipWidthMm / 2, zLow + b.ClipLengthMm / 2 - 3 } }, false, Text, clip, 300 * K, 250 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { bw + b.ClipThicknessMm + 6.0, zLow - b.ClipLengthMm / 4 } }, false, Text, bolt, 350 * K, -250 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { half - b.AngleLegAMm / 2, zLow - fz } }, false, Text, fix, 250 * K, -120 * K);
            SteelDrawing.PlaceLabel(w, ob, new[] { new[] { k, (zLow + zHigh) / 2 } }, false, Text, rail, 450 * K, 0);
            w.Layer = SteelDrawing.LayerAnno;
        }
    }
}
