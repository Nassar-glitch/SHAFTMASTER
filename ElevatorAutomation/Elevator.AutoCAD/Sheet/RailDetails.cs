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
                    Text = 2.5 * Math.Max(planScale, 1);
                    TitleWindow = (double[])plan.Window.Clone();
                    var box = DrawPlan(db, tr, ctx, oldPlan);
                    plan.Window = new[] { oldPlan[0], oldPlan[1], Math.Max(oldPlan[2], box[2] + Margin), Math.Max(oldPlan[3], box[3] + Margin) };
                    Text = 2.5 * Math.Max(bracketsScale, 1);
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

        // ---------- names of the parts: one full name per part, the same in the plan and in the elevation ----------
        static string AngleName(Rail r)
        {
            return "ANGLE L" + N(r.Bracket.AngleLegAMm) + "x" + N(r.Bracket.AngleLegBMm) + "x" + N(r.Bracket.AngleThicknessMm);
        }

        static string PlateName(Rail r)
        {
            return "PLATE " + N(r.PlateWidth) + "x" + N(r.Bracket.AngleLengthMm) + "x" + N(r.Bracket.PlateThicknessMm);
        }

        static string ClipName(Rail r)
        {
            return "CLIP " + N(r.Bracket.ClipLengthMm) + "x" + N(r.Bracket.ClipWidthMm) + "x" + N(r.Bracket.ClipThicknessMm);
        }

        static string BoltsName(Rail r)
        {
            return N(r.Bracket.ClipBoltCount) + " BOLTS M" + N(r.Bracket.ClipBoltDiameterMm);
        }

        static string FixName(Context c, Rail r)
        {
            return c.Steel ? "BOLT M" + N(r.Bracket.AnchorDiameterMm) : "ANCHOR D" + N(r.Bracket.AnchorDiameterMm) + "x" + N(r.Bracket.AnchorLengthMm);
        }

        static string RailName(Rail r)
        {
            return "RAIL " + r.Profile.Name;
        }

        static string SupportName(Context c)
        {
            return c.Steel ? "BEAM " + c.Beam.Name : "WALL";
        }

        // ---------- one tidy column of labels ----------
        sealed class Call
        {
            public double Ax, Ay;
            public string Text;
        }

        static bool Cross(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            double d1 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax), d2 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
            double d3 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx), d4 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
            return d1 * d2 < 0 && d3 * d4 < 0;
        }

        // The labels in one column at x, two text heights apart, centred on the anchors. The order of the column is the one whose
        // leaders cross no other (the shortest of those); the leaders are short straight lines from the member to the column.
        static void Column(SteelDrawing.Writer w, List<Call> items, double xCol, bool left)
        {
            int n = items.Count;
            if (n == 0)
                return;
            double step = Text * 2.0, cy = items.Average(i => i.Ay);
            var slots = new double[n];
            for (int k = 0; k < n; k++)
                slots[k] = cy + ((n - 1) / 2.0 - k) * step;
            var perm = Enumerable.Range(0, n).ToArray();
            int[] best = null;
            double bestScore = double.MaxValue;
            Action<int> rec = null;
            rec = pos =>
            {
                if (pos == n)
                {
                    int cross = 0;
                    double len = 0;
                    for (int i = 0; i < n; i++)
                    {
                        len += Math.Sqrt(Math.Pow(xCol - items[perm[i]].Ax, 2) + Math.Pow(slots[i] - items[perm[i]].Ay, 2));
                        for (int j = i + 1; j < n; j++)
                        {
                            if (Cross(items[perm[i]].Ax, items[perm[i]].Ay, xCol, slots[i], items[perm[j]].Ax, items[perm[j]].Ay, xCol, slots[j]))
                                cross++;
                        }
                    }
                    double score = cross * 1e9 + len;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = (int[])perm.Clone();
                    }
                    return;
                }
                for (int i = pos; i < n; i++)
                {
                    var t = perm[pos];
                    perm[pos] = perm[i];
                    perm[i] = t;
                    rec(pos + 1);
                    t = perm[pos];
                    perm[pos] = perm[i];
                    perm[i] = t;
                }
            };
            rec(0);
            w.Layer = SteelDrawing.LayerAnno;
            for (int i = 0; i < n; i++)
            {
                var it = items[best[i]];
                if (left)
                    w.LabelLeft(it.Ax, it.Ay, xCol, slots[i], Text, it.Text);
                else
                    w.Label(it.Ax, it.Ay, xCol, slots[i], Text, it.Text);
            }
        }

        // ---------- the plan (RailsPlan) ----------
        static double Wall(Rail r)
        {
            return Math.Max(140, r.Bracket.AnchorLengthMm + 30);
        }

        // the room a plan takes left of its wall face (labels of the support and the fixing) and right of the rail tip
        static double LeftRoom(Rail r)
        {
            return Wall(r) + 3 * Text + 13 * Text;
        }

        static double RightRoom(Rail r)
        {
            return r.Bracket.AngleLegBMm + r.Bracket.PlateThicknessMm + r.Profile.HeightMm + 5 * Text + 17 * Text;
        }

        static double[] DrawPlan(Database db, Transaction tr, Context c, double[] window)
        {
            var before = Existing(db, tr);
            var titles = FindTitles(db, tr, window, "GUIDES AND BRACKETS");
            double x = window[0] + 100;
            double y0 = window[1] + 11 * Text + 200;
            double[] xs = new double[2];
            int i = 0;
            foreach (var r in new[] { c.Car, c.Cw })
            {
                double originX = x + LeftRoom(r);
                xs[i] = originX - Wall(r);
                DrawPlanOf(db, tr, c, r, originX, y0);
                // the two titles stand side by side under the plans: about 56 text heights each
                x = Math.Max(originX + RightRoom(r), xs[i] + 56 * Text) + 60;
                i++;
            }
            foreach (var t in titles)
            {
                string text = t.Contents ?? "";
                bool car = text.IndexOf("CAR", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("C.W.", StringComparison.OrdinalIgnoreCase) < 0;
                t.UpgradeOpen();
                t.Location = new Point3d(xs[car ? 0 : 1], t.Location.Y, 0);
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

        static void DrawPlanOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            double half = r.PlateWidth / 2 + b.AngleLegAMm;
            double wall = Wall(r);
            double xr = b.AngleLegBMm + b.PlateThicknessMm;
            double span = half + 2 * Text;
            double depth = c.Steel ? c.Beam.WidthMm : wall;
            // the support: the concrete wall, or the UPN beam in plan (outline with the steel hatch of the legend)
            w.Layer = c.Steel ? SteelDrawing.LayerBeam : LayerWall;
            var box = w.Rect(-depth, -span, 0, span);
            if (c.Steel)
                w.Fill(box, "ANSI32", 1.2 * Text);
            // the bracket and the rail as blocks
            w.Layer = LayerBracket;
            w.Insert(BracketPlanBlock(db, tr, r), 0, 0, 1, 0);
            w.Layer = LayerRail;
            w.Insert(RailBlock(db, tr, r.Profile), xr, 0, 1, 0);
            // the fixing of each leg A: an anchor into the wall, or a bolt through the flange of the beam
            double fixA = b.AnchorDiameterMm;
            w.Layer = LayerAnchor;
            double yfix = r.PlateWidth / 2 + b.AngleLegAMm / 2 - b.AngleThicknessMm / 2;
            foreach (int s in new[] { -1, 1 })
            {
                double xFar = c.Steel ? -depth : -b.AnchorLengthMm;
                w.Rect(xFar, s * yfix - fixA / 2, b.AngleThicknessMm, s * yfix + fixA / 2);
                if (c.Steel)
                {
                    double head = 0.65 * fixA;
                    w.Rect(-depth - head, s * yfix - fixA * 0.8, -depth, s * yfix + fixA * 0.8);
                    w.Rect(b.AngleThicknessMm, s * yfix - fixA * 0.8, b.AngleThicknessMm + head, s * yfix + fixA * 0.8);
                }
            }
            // dimensions: the chain along x above the bracket (leg B, plate, rail height), the width of the plate at the right
            var ob = new SteelDrawing.Obstacles();
            Chain(w, ob, new[] { 0.0, b.AngleLegBMm }, new[] { N(b.AngleLegBMm) }, span + 2 * Text, 0, span, true);
            Chain(w, ob, new[] { xr, xr + r.Profile.HeightMm }, new[] { N(r.Profile.HeightMm) }, span + 2 * Text, 0, span, true);
            double xDim = xr + r.Profile.HeightMm + 1.5 * Text;
            Single(w, ob, -r.PlateWidth / 2, r.PlateWidth / 2, xDim, N(r.PlateWidth), false, 0);
            // the labels: the support and the fixing left of the wall, the bracket parts right of the rail, in one column each
            double ct = b.ClipThicknessMm, bw = r.Profile.WidthMm / 2;
            double ybolt = bw + (ct + b.ClipWidthMm - 8) / 2;
            var right = new List<Call>
            {
                new Call { Ax = b.AngleThicknessMm / 2, Ay = r.PlateWidth / 2 + b.AngleLegAMm / 2 - b.AngleThicknessMm, Text = AngleName(r) },
                new Call { Ax = b.AngleLegBMm + b.PlateThicknessMm / 2, Ay = r.PlateWidth / 4, Text = PlateName(r) },
                new Call { Ax = xr + ct / 2, Ay = -(bw + ct + (b.ClipWidthMm - 8 - ct) / 2), Text = ClipName(r) },
                new Call { Ax = xr + ct + 0.3 * b.ClipBoltDiameterMm, Ay = ybolt, Text = BoltsName(r) },
                new Call { Ax = xr + r.Profile.HeightMm, Ay = 0, Text = RailName(r) }
            };
            Column(w, right, xDim + 3.5 * Text, false);
            var left = new List<Call>
            {
                new Call { Ax = -depth / 2, Ay = -span, Text = SupportName(c) },
                new Call { Ax = c.Steel ? -depth / 2 : -b.AnchorLengthMm / 2, Ay = -yfix, Text = FixName(c, r) }
            };
            Column(w, left, -depth - 3 * Text, true);
            w.Layer = SteelDrawing.LayerAnno;
        }

        // A chain of horizontal dimensions along local x on the row y; the text of an interval that does not fit goes outside the chain or
        // beside it (DimensionLayout).
        static void Chain(SteelDrawing.Writer w, SteelDrawing.Obstacles ob, double[] xs, string[] texts, double row, double yFrom, double yTop, bool above)
        {
            var placement = DimensionLayout.PlaceChainTexts(xs, texts, Text, Text * 0.8, new List<double[]>());
            for (int i = 0; i < texts.Length; i++)
            {
                var p = placement[i];
                double mid = (xs[i] + xs[i + 1]) / 2;
                double lat = p.Slot == DimensionTextSlot.Beside ? p.LateralMm : 0;
                var d = Dim(w, xs[i], yTop, xs[i + 1], yTop, row, 0, "");
                if (p.Slot != DimensionTextSlot.Centred)
                {
                    d.TextPosition = WorldOf(w, mid + p.AlongMm, row + (above ? 1 : -1) * (Text * 0.9 + lat));
                    d.UsingDefaultTextPosition = false;
                }
            }
        }

        static Point3d WorldOf(SteelDrawing.Writer w, double u, double v)
        {
            return new Point3d(curOx + u, curOy + v, 0);
        }

        // One vertical dimension between two local y at the column x (text left of the line).
        static void Single(SteelDrawing.Writer w, SteelDrawing.Obstacles ob, double a, double b, double at, string text, bool horizontal, double dummy)
        {
            w.Layer = SteelDrawing.LayerDims;
            var d = Dim(w, at - 0.3 * Text, a, at - 0.3 * Text, b, at, 90, "");
            d.TextPosition = WorldOf(w, at - 0.5 * Text, Math.Min(a, b) + 2.2 * Text);
            d.UsingDefaultTextPosition = false;
            w.Layer = SteelDrawing.LayerAnno;
        }

        // A rotated dimension through the writer: (u1, v1) and (u2, v2) the extension points, the dimension line through (lineU or
        // lineV); rotation 0 = horizontal measure along x, 90 = along y. A given text replaces the measured value.
        static RotatedDimension Dim(SteelDrawing.Writer w, double u1, double v1, double u2, double v2, double line, double rotationDeg, string text)
        {
            w.Layer = SteelDrawing.LayerDims;
            Point3d p1 = WorldOf(w, u1, v1), p2 = WorldOf(w, u2, v2);
            Point3d dl = rotationDeg == 0 ? WorldOf(w, (u1 + u2) / 2, line) : WorldOf(w, line, (v1 + v2) / 2);
            var d = new RotatedDimension(rotationDeg * Math.PI / 180, p1, p2, dl, text ?? "", w.Database.Dimstyle);
            d.SetDatabaseDefaults(w.Database);
            d.Dimtxt = Text;
            d.Dimasz = Text * 0.7;
            d.Dimexo = Text * 0.3;
            d.Dimexe = Text * 0.4;
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
                double bw = r.Profile.WidthMm / 2;
                RectIn(db, tr, btr, -w, -len, w, len, LayerBracket);
                foreach (int s in new[] { -1, 1 })
                {
                    double y0 = s * (w - t), y1 = s * (w + a - t);
                    RectIn(db, tr, btr, Math.Min(y0, y1), -len, Math.Max(y0, y1), len, LayerBracket);
                    double cy0 = s * (bw - 8), cy1 = s * (bw + b.ClipWidthMm - 8);
                    RectIn(db, tr, btr, Math.Min(cy0, cy1), -b.ClipLengthMm / 2, Math.Max(cy0, cy1), b.ClipLengthMm / 2, LayerClip);
                    double bx = s * ((bw + b.ClipThicknessMm + bw + b.ClipWidthMm - 8) / 2);
                    foreach (int k in new[] { -1, 1 })
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(bx, k * b.ClipLengthMm / 4, 0), Vector3d.ZAxis, b.ClipBoltDiameterMm / 2), LayerClip);
                    double fx = s * (w + a / 2 - t / 2);
                    double fz = FixZ(c, r);
                    foreach (int k in new[] { -1, 1 })
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(fx, k * fz, 0), Vector3d.ZAxis, b.AnchorDiameterMm / 2), LayerAnchor);
                }
            });
        }

        static double FixZ(Context c, Rail r)
        {
            double len = r.Bracket.AngleLengthMm / 2;
            return c.Steel ? Math.Min(len - 30, c.Beam.HeightMm / 2 - 25) : len - 35;
        }

        static double[] DrawElevations(Database db, Transaction tr, Context c, double[] window)
        {
            var before = Existing(db, tr);
            double x = window[0] - ElevationShift + 100;
            double y0 = window[1] + 12 * Text + 300;
            var rails = new[] { c.Cw, c.Car };
            double[] centres = new double[2];
            for (int i = 0; i < 2; i++)
            {
                var r = rails[i];
                double half = r.PlateWidth / 2 + r.Bracket.AngleLegAMm;
                double ox = x + 3 * Text + 4 * Text + half + 12 * Text;   // the support label and the spacing dimension on the left
                centres[i] = ox;
                DrawElevationOf(db, tr, c, r, ox, y0);
                x = ox + half + 4 * Text + 18 * Text + 40;
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
            double len = b.AngleLengthMm;
            double half = r.PlateWidth / 2 + b.AngleLegAMm;
            double bw = r.Profile.WidthMm / 2, k = r.Profile.BladeMm / 2;
            // the two brackets are drawn close together: the rail is broken between them and the dimension carries the real spacing
            double zLow = 0, zHigh = 3 * len;
            double over = 0.7 * len;
            double bottom = zLow - len / 2 - over, top = zHigh + len / 2 + over;
            // the support behind the brackets: the wall, or the UPN beam at each bracket
            if (c.Steel)
            {
                w.Layer = SteelDrawing.LayerBeam;
                foreach (double z in new[] { zLow, zHigh })
                {
                    var band = w.Rect(-half - Text, z - c.Beam.HeightMm / 2, half + Text, z + c.Beam.HeightMm / 2);
                    w.Fill(band, "ANSI32", 1.2 * Text);
                }
            }
            else
            {
                w.Layer = LayerWall;
                var wallBox = w.Rect(-half - 2 * Text, bottom, half + 2 * Text, top);
            }
            // the rail with its break marks
            w.Layer = LayerRail;
            double zm = (zLow + zHigh) / 2, gap = 0.9 * Text;
            foreach (double xx in new[] { -bw, bw, -k, k })
            {
                w.Seg(xx, bottom, xx, zm - gap / 2);
                w.Seg(xx, zm + gap / 2, xx, top);
            }
            foreach (double z in new[] { zm - gap / 2, zm + gap / 2 })
            {
                double wd = bw + 0.5 * Text;
                w.Poly(new[] { new[] { -wd, z }, new[] { -wd / 3, z + 0.35 * gap }, new[] { wd / 3, z - 0.35 * gap }, new[] { wd, z } }, false);
            }
            // the brackets: one named block at each level
            var block = BracketElevationBlock(db, tr, c, r);
            foreach (double z in new[] { zLow, zHigh })
            {
                w.Layer = LayerBracket;
                w.Insert(block, 0, z, 1, 0);
            }
            // dimensions: the real spacing (left), the bracket length (right), the width chain (above)
            double dimX = -half - 4 * Text;
            Dim(w, -half - 0.3 * Text, zLow, -half - 0.3 * Text, zHigh, dimX, 90, N(r.SpacingMm));
            double dimR = half + 4 * Text;
            Dim(w, half + 0.3 * Text, zHigh - len / 2, half + 0.3 * Text, zHigh + len / 2, dimR, 90, "");
            double row = top + 2.5 * Text;
            Chain(w, null, new[] { -r.PlateWidth / 2, r.PlateWidth / 2 }, new[] { N(r.PlateWidth) }, row, top, top, true);
            // the heading over the rail
            w.Layer = SteelDrawing.LayerAnno;
            w.Text(-half, row + 6.5 * Text, Text * 1.2, r.Title + " RAIL " + r.Profile.Name);
            // labels: the bracket parts right of the lower bracket in one column, the support left
            double fz = FixZ(c, r);
            var right = new List<Call>
            {
                new Call { Ax = half - b.AngleThicknessMm / 2 - b.AngleLegAMm / 4, Ay = zLow + len / 2 - 4, Text = AngleName(r) },
                new Call { Ax = r.PlateWidth / 2 - 3, Ay = zLow - len / 4, Text = PlateName(r) },
                new Call { Ax = bw + b.ClipWidthMm / 2, Ay = zLow + b.ClipLengthMm / 2 - 2, Text = ClipName(r) },
                new Call { Ax = (bw + b.ClipThicknessMm + bw + b.ClipWidthMm - 8) / 2, Ay = zLow - b.ClipLengthMm / 4, Text = BoltsName(r) },
                new Call { Ax = r.PlateWidth / 2 + b.AngleLegAMm / 2 - b.AngleThicknessMm / 2, Ay = zLow - fz, Text = FixName(c, r) },
                new Call { Ax = k, Ay = zLow + len / 2 + over / 2, Text = RailName(r) }
            };
            Column(w, right, dimR + 1.2 * Text, false);
            var left = new List<Call>
            {
                new Call { Ax = c.Steel ? -half - Text : -half - 2 * Text, Ay = c.Steel ? zLow : zLow - len / 2, Text = SupportName(c) }
            };
            Column(w, left, dimX - 3 * Text, true);
            w.Layer = SteelDrawing.LayerAnno;
        }
    }
}
