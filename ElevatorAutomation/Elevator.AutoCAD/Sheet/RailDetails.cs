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
            /// <summary>How the rail group is fixed, decided from the plan numbers.</summary>
            public RailFixingDecision Fixing;
            /// <summary>The distance from the support face (wall, beam or head plate of the arm) to the foot back of the rail: the gap of the
            /// plan for a wall bracket; for a cross arm the nominal assembly (the arm legs of the preset length overlapped by the preset
            /// overlap).</summary>
            public double Gap
            {
                get
                {
                    double t = Bracket.AngleThicknessMm, tp = Bracket.PlateThicknessMm;
                    double nominal = 2 * Bracket.AngleLegBMm - Bracket.AssemblyOverlapMm + tp + 2 * t;
                    if (Fixing == null || CrossArm)
                        return nominal;
                    return Math.Max(tp + 2 * t + 4, Fixing.ArmLengthMm);
                }
            }
            /// <summary>The length of the two arm legs: the preset leg, longer when the gap needs it (the legs grow so that they still
            /// overlap by the preset overlap).</summary>
            public double ArmLeg
            {
                get
                {
                    double t = Bracket.AngleThicknessMm, tp = Bracket.PlateThicknessMm;
                    return Math.Max(Bracket.AngleLegBMm, (Gap - tp - 2 * t + Bracket.AssemblyOverlapMm) / 2);
                }
            }
            public bool CrossArm
            {
                get { return Fixing != null && Fixing.Type == RailFixingType.CrossArm; }
            }
            // derived plate span
            public double PlateWidth { get { return Profile.WidthMm + 2 * Bracket.ClipWidthMm; } }
        }

        sealed class Context
        {
            public bool Steel;
            public ChannelProfile Beam;
            public Rail Car, Cw;
            public RailFixingPreset Arm;
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
            bool hidden, planHidden;
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
                    PlanSymbols(db, tr, ctx, !planHidden);
                    planHidden = true;
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
            c.Arm = RailBracketFields.EffectiveFixing(project, lib);
            // the numbers the plan dimensions: Part016 shaft width, Part024 car DBG, Part003 CW DBG, Part017 rear wall to the CW row
            var dims = PlanDimensions(db, tr);
            double sw = dims.ContainsKey("Part016") ? dims["Part016"] : 0;
            foreach (var r in new[] { c.Car, c.Cw })
            {
                bool car = r.Family == RailBracketFields.Car;
                string dbgKey = car ? "Part024" : "Part003";
                if (sw > 0 && dims.ContainsKey(dbgKey))
                {
                    var facts = new RailFixingFacts
                    {
                        Family = r.Family,
                        ShaftWidthMm = sw,
                        DbgMm = dims[dbgKey],
                        RailHeightMm = r.Profile.HeightMm,
                        RailWidthMm = r.Profile.WidthMm,
                        RearWallToRowMm = !car && dims.ContainsKey("Part017") ? dims["Part017"] : 0,
                        Steel = c.Steel
                    };
                    r.Fixing = RailFixing.Decide(facts, RailBracketFields.FixingSetting(project, lib, r.Family), car ? c.Arm.CarReachMm : c.Arm.CwSideReachMm);
                }
            }
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

        // ---------- the main plan follows the detail ----------
        // The template plan draws the car brackets as wall brackets and the counterweight brackets on the rear wall (a cross arm). When a
        // group is fixed otherwise (a chosen or Auto wall bracket for the counterweight rails), the template's bracket block of that
        // group is hidden and the program's own symbol (a named block on S-BRACKET, same type, arm and gap as the detail) is drawn.
        static void PlanSymbols(Database db, Transaction tr, Context c, bool hide)
        {
            if (c.Cw == null || c.Cw.Fixing == null || c.Cw.CrossArm)
                return;
            Polyline shaft = null;
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            var cwEntities = new List<Entity>();
            foreach (ObjectId id in model)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e == null)
                    continue;
                var tag = SectionAADrawing.Tag(e);
                if (tag == null || tag.Length < 2)
                    continue;
                if (tag[0] == "Plan.Shaft.InnerOutline.EntranceDatum")
                    shaft = e as Polyline;
                else if (tag[0].StartsWith("Plan.", StringComparison.Ordinal) && (tag[1] == "cwLeft" || tag[1] == "cwRight"))
                    cwEntities.Add(e);
            }
            var dims = PlanDimensions(db, tr);
            if (shaft == null || shaft.NumberOfVertices != 6 || !dims.ContainsKey("Part003") || !dims.ContainsKey("Part017"))
                return;
            Point3d origin = shaft.GetPoint3dAt(1);
            Vector3d xv = shaft.GetPoint3dAt(4) - origin, yv = shaft.GetPoint3dAt(2) - origin;
            double sw = xv.Length, sd = yv.Length;
            var frame = new Elevator.AutoCAD.Plan.ShaftFrame { Origin = origin, XAxis = xv.GetNormal(), YAxis = yv.GetNormal(), ZAxis = Vector3d.ZAxis };
            double dbg = dims["Part003"], row = sd - dims["Part017"];
            double back = sw / 2 - dbg / 2 - c.Cw.Profile.HeightMm;   // the rail back of the left rail, from the left wall
            if (hide)
            {
                foreach (var e in cwEntities)
                {
                    Extents3d b;
                    try
                    {
                        b = e.GeometricExtents;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        continue;
                    }
                    var lo = frame.Local(b.MinPoint);
                    var hi = frame.Local(b.MaxPoint);
                    double minX = Math.Min(lo.X, hi.X), maxX = Math.Max(lo.X, hi.X);
                    // the bracket block stands on the wall side of the rail back (left rail) or beyond it (right rail)
                    bool left = maxX <= back + 3, right = minX >= sw - back - 3;
                    if (!left && !right)
                        continue;
                    e.UpgradeOpen();
                    e.Visible = false;
                }
            }
            var block = BracketPlanBlock(db, tr, c.Cw, false, "SM_RAIL_FIXING_PLAN_");
            foreach (int side in new[] { -1, 1 })
            {
                Func<double, double, Point3d> world = (u, v) => frame.World(new Point3d(side < 0 ? u : sw - u, row + v, 0));
                var w = new SteelDrawing.Writer(db, tr, Prefix, "railFixing", world);
                w.Layer = LayerBracket;
                w.Insert(block, 0, 0, 1, 0);
            }
        }

        // The measurements of the plan dimensions of the drawing by the last part of their key (Plan.Dimension.PartNNN).
        static Dictionary<string, double> PlanDimensions(Database db, Transaction tr)
        {
            var map = new Dictionary<string, double>();
            var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var d = tr.GetObject(id, OpenMode.ForRead) as Dimension;
                if (d == null)
                    continue;
                var tag = SectionAADrawing.Tag(d);
                if (tag == null || tag.Length == 0 || !tag[0].StartsWith("Plan.Dimension.", StringComparison.Ordinal))
                    continue;
                map[tag[0].Substring("Plan.Dimension.".Length)] = d.Measurement;
            }
            return map;
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

        // The bracket assembly of the user's library (the anchor sheets, "2 brackets 270 x 50 x 40, plate 270 x 50 x 4, 4 assembly bolts
        // M10 x 40"): two right-angle pieces mounted opposite each other. The fixing angle stands on the wall (or the beam, or the head plate
        // of a cross arm) with its wall leg; the rail angle carries the rail on its base leg, the foot on the plate, held by two clips with
        // two bolts each. The two arm legs overlap face to face and are joined by 4 assembly bolts through slotted holes: the overlap sets the
        // distance from the wall to the rail. Plan frame: x from the support face to the rail, y along the support; the arms are on the +y side.
        // The name carries the rail group, the type of fixing and the gap (the plan symbol and the detail are checked by it).
        static string FixingTag(Rail r)
        {
            string type = r.CrossArm ? "ARM" : "WALL";
            double gap = r.CrossArm ? r.Fixing.ArmLengthMm : r.Gap;
            return "_" + (r.Family == RailBracketFields.Car ? "CAR" : "CW") + "_" + type + "_G" + Math.Round(gap).ToString(CultureInfo.InvariantCulture);
        }

        // the x positions of the two assembly bolts of a row, inside the overlap of the arm legs
        static double[] AssemblyBoltX(Rail r)
        {
            double t = r.Bracket.AngleThicknessMm, tp = r.Bracket.PlateThicknessMm, g = r.Gap;
            double from = g - tp - t - r.ArmLeg, to = t + r.ArmLeg;      // the overlap: [from, to]
            return new[] { from + 0.3 * (to - from), from + 0.7 * (to - from) };
        }

        static ObjectId BracketPlanBlock(Database db, Transaction tr, Rail r, bool clips = true, string prefix = "SM_RAIL_BRACKET_PLAN_")
        {
            return SteelDrawing.Block(db, tr, prefix + Key(r) + FixingTag(r) + (clips ? "" : "_SYMBOL"), btr =>
            {
                var b = r.Bracket;
                double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, la = r.ArmLeg, half = r.PlateWidth / 2;
                double d = b.AssemblyBoltDiameterMm;
                // the fixing angle: the wall leg on the support and its arm leg
                RectIn(db, tr, btr, 0, -half, t, half, LayerBracket);
                RectIn(db, tr, btr, t, half - t, t + la, half, LayerBracket);
                // the rail angle: the base leg behind the plate and its arm leg, inside the arm of the fixing angle
                RectIn(db, tr, btr, g - tp - t, -half, g - tp, half, LayerBracket);
                RectIn(db, tr, btr, g - tp - t - la, half - 2 * t, g - tp - t, half - t, LayerBracket);
                // the plate the rail foot sits on (270 x 50 x 4: 50 across)
                RectIn(db, tr, btr, g - tp, -b.AngleLegAMm / 2, g, b.AngleLegAMm / 2, LayerBracket);
                // the assembly bolts through the two arm legs (axis along y): shank, head outside, nut inside
                foreach (double x in AssemblyBoltX(r))
                {
                    RectIn(db, tr, btr, x - d / 2, half - 2 * t - 0.8 * d, x + d / 2, half + 0.65 * d, LayerClip);
                    RectIn(db, tr, btr, x - 0.8 * d, half, x + 0.8 * d, half + 0.65 * d, LayerClip);
                    RectIn(db, tr, btr, x - 0.8 * d, half - 2 * t - 0.8 * d, x + 0.8 * d, half - 2 * t, LayerClip);
                }
                if (!clips)
                    return;
                double bw = r.Profile.WidthMm / 2, f = r.Profile.FootMm, ct = b.ClipThicknessMm, xb = g - tp;
                foreach (int s in new[] { -1, 1 })
                {
                    // the clip: a strap on the base leg, a riser against the foot edge and a lip over its front face
                    double yf = s * bw, yc = s * (bw + ct), yb = s * (bw + b.ClipWidthMm - 8), yl = s * (bw - 8);
                    Poly(db, tr, btr, new[]
                    {
                        new[] { xb, yf }, new[] { xb, yb }, new[] { xb + ct, yb }, new[] { xb + ct, yc }, new[] { g + f + ct, yc },
                        new[] { g + f + ct, yl }, new[] { g + f, yl }, new[] { g + f, yf }
                    }, true, LayerClip);
                    // the clip bolt through the strap and the base leg: head on the strap, nut behind the base leg
                    double ybolt = s * ((bw + ct + (bw + b.ClipWidthMm - 8)) / 2), cd = b.ClipBoltDiameterMm;
                    RectIn(db, tr, btr, xb + ct, ybolt - cd * 0.8, xb + ct + 0.65 * cd, ybolt + cd * 0.8, LayerClip);
                    RectIn(db, tr, btr, xb - t - 0.8 * cd, ybolt - cd * 0.8, xb - t, ybolt + cd * 0.8, LayerClip);
                    RectIn(db, tr, btr, xb - t - 0.8 * cd, ybolt - cd / 2, xb + ct + 0.65 * cd, ybolt + cd / 2, LayerClip);
                }
            });
        }

        // ---------- names of the parts, as the library names them; the same in the plan and in the elevation ----------
        static string AngleName(Rail r)
        {
            return "BRACKET L " + N(r.Bracket.AngleLengthMm) + "x" + N(r.Bracket.AngleLegAMm) + "x" + N(r.Bracket.AngleLegBMm) + " (x2)";
        }

        static string PlateName(Rail r)
        {
            return "PLATE " + N(r.Bracket.AngleLengthMm) + "x" + N(r.Bracket.AngleLegAMm) + "x" + N(r.Bracket.PlateThicknessMm);
        }

        static string AssemblyName(Rail r)
        {
            return "ASSEMBLY BOLTS 4 x M" + N(r.Bracket.AssemblyBoltDiameterMm) + "x" + N(r.Bracket.AssemblyBoltLengthMm);
        }

        static string ClipName(Rail r)
        {
            return "CLIPS 2 x " + N(r.Bracket.ClipLengthMm) + "x" + N(r.Bracket.ClipWidthMm) + "x" + N(r.Bracket.ClipThicknessMm);
        }

        static string BoltsName(Rail r)
        {
            return "CLIP BOLTS " + N(r.Bracket.ClipBoltCount) + " x M" + N(r.Bracket.ClipBoltDiameterMm) + " EACH";
        }

        static string FixName(Context c, Rail r)
        {
            if (r.CrossArm)
                return "BOLTS M" + N(r.Bracket.AnchorDiameterMm);
            return c.Steel ? "BOLTS M" + N(r.Bracket.AnchorDiameterMm) : "ANCHOR BOLTS D" + N(r.Bracket.AnchorDiameterMm) + "x" + N(r.Bracket.AnchorLengthMm);
        }

        static string ArmName(Context c)
        {
            return "CROSS ARM " + c.Arm.ArmProfile;
        }

        static string RailName(Rail r)
        {
            return "RAIL " + r.Profile.Name;
        }

        static string SupportName(Context c)
        {
            return c.Steel ? "BEAM " + c.Beam.Name : "WALL";
        }

        static string SupportName(Context c, bool rear)
        {
            return c.Steel ? "BEAM " + c.Beam.Name : (rear ? "REAR WALL" : "WALL");
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
            return Wall(r) + (r.CrossArm ? r.Fixing.ArmLengthMm : 0) + 3 * Text + 26 * Text;
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

        // The labels of the bracket assembly in plan (right of the rail), in the order of the library notes.
        static List<Call> PlanAssemblyCalls(Rail r)
        {
            var b = r.Bracket;
            double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, la = r.ArmLeg, half = r.PlateWidth / 2;
            double ct = b.ClipThicknessMm, bw = r.Profile.WidthMm / 2, xb = g - tp;
            double[] bx = AssemblyBoltX(r);
            return new List<Call>
            {
                new Call { Ax = t / 2, Ay = half / 2, Text = AngleName(r) },
                new Call { Ax = g - tp / 2, Ay = b.AngleLegAMm / 4, Text = PlateName(r) },
                new Call { Ax = bx[1], Ay = half + 0.3 * b.AssemblyBoltDiameterMm, Text = AssemblyName(r) },
                new Call { Ax = xb + ct / 2, Ay = -(bw + ct + (b.ClipWidthMm - 8 - ct) / 2), Text = ClipName(r) },
                new Call { Ax = xb + ct + 0.3 * b.ClipBoltDiameterMm, Ay = bw + (ct + b.ClipWidthMm - 8) / 2, Text = BoltsName(r) },
                new Call { Ax = g + r.Profile.HeightMm, Ay = 0, Text = RailName(r) }
            };
        }

        // A rail fixed on a cross arm: the surface (rear wall or beam) at the left, the end plate with its anchors, the arm (a section of
        // the library: UPE 120), the head plate, then the two-angle assembly (the fixing angle on the head plate) and the rail.
        static void DrawPlanCrossArm(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var a = c.Arm;
            var w = WriterAt(db, tr, ox, oy);
            double half = r.PlateWidth / 2;
            double g = r.Gap;
            double len = Math.Max(r.Fixing.ArmLengthMm, 2 * a.ArmEndPlateThicknessMm + 20);
            double xw = -len;
            double et = a.ArmEndPlateThicknessMm;
            double span = Math.Max(half, a.ArmEndPlateMm / 2) + 2 * Text;
            double depth = c.Steel ? c.Beam.WidthMm : Wall(r);
            w.Layer = c.Steel ? SteelDrawing.LayerBeam : LayerWall;
            var box = w.Rect(xw - depth, -span, xw, span);
            if (c.Steel)
                w.Fill(box, "ANSI32", 1.2 * Text);
            w.Layer = LayerBracket;
            w.Rect(xw, -a.ArmEndPlateMm / 2, xw + et, a.ArmEndPlateMm / 2);
            w.Rect(-et, -half, 0, half);
            w.Layer = SteelDrawing.LayerBeam;
            w.Rect(xw + et, -a.ArmWidthMm / 2, -et, a.ArmWidthMm / 2);
            w.Layer = LayerAnchor;
            double yAnchor = a.ArmEndPlateMm / 2 - 20, d = a.ArmAnchorDiameterMm;
            double far = c.Steel ? xw - depth : xw - Math.Min(depth - 10, r.Bracket.AnchorLengthMm);
            foreach (int s in new[] { -1, 1 })
                w.Rect(far, s * yAnchor - d / 2, xw + et, s * yAnchor + d / 2);
            // the assembly and the rail
            w.Layer = LayerBracket;
            w.Insert(BracketPlanBlock(db, tr, r), 0, 0, 1, 0);
            w.Layer = LayerRail;
            w.Insert(RailBlock(db, tr, r.Profile), g, 0, 1, 0);
            // the fixing angle is bolted to the head plate
            w.Layer = LayerAnchor;
            double fixA = b.AnchorDiameterMm, yfix = half - b.AngleLegAMm / 2;
            foreach (int s in new[] { -1, 1 })
                w.Rect(-et, s * yfix - fixA / 2, b.AngleThicknessMm, s * yfix + fixA / 2);
            var ob = new SteelDrawing.Obstacles();
            Chain(w, ob, new[] { xw, 0.0 }, new[] { N(len) }, span + 2 * Text, 0, span, true);
            Chain(w, ob, new[] { 0.0, g }, new[] { N(g) }, span + 2 * Text, 0, span, true);
            Chain(w, ob, new[] { g, g + r.Profile.HeightMm }, new[] { N(r.Profile.HeightMm) }, span + 2 * Text, 0, span, true);
            double xDim = g + r.Profile.HeightMm + 1.5 * Text;
            Single(w, ob, -half, half, xDim, N(r.PlateWidth), false, 0);
            Column(w, PlanAssemblyCalls(r), xDim + 3.5 * Text, false);
            var left = new List<Call>
            {
                new Call { Ax = xw - depth / 2, Ay = -span, Text = SupportName(c, true) },
                new Call { Ax = xw - depth / 2, Ay = -yAnchor, Text = c.Steel ? "BOLTS M" + N(d) : "ANCHOR BOLTS D" + N(d) + "x" + N(r.Bracket.AnchorLengthMm) },
                new Call { Ax = xw / 2, Ay = a.ArmWidthMm / 2, Text = ArmName(c) }
            };
            Column(w, left, xw - depth - 3 * Text, true);
            w.Layer = SteelDrawing.LayerAnno;
        }

        static void DrawPlanOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            if (r.CrossArm)
            {
                DrawPlanCrossArm(db, tr, c, r, ox, oy);
                return;
            }
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            double half = r.PlateWidth / 2;
            double wall = Wall(r);
            double g = r.Gap;
            double span = half + 2 * Text;
            double depth = c.Steel ? c.Beam.WidthMm : wall;
            // the support: the concrete wall, or the UPN beam in plan (outline with the steel hatch of the legend)
            w.Layer = c.Steel ? SteelDrawing.LayerBeam : LayerWall;
            var box = w.Rect(-depth, -span, 0, span);
            if (c.Steel)
                w.Fill(box, "ANSI32", 1.2 * Text);
            // the assembly and the rail as blocks
            w.Layer = LayerBracket;
            w.Insert(BracketPlanBlock(db, tr, r), 0, 0, 1, 0);
            w.Layer = LayerRail;
            w.Insert(RailBlock(db, tr, r.Profile), g, 0, 1, 0);
            // the fixing of the wall leg: anchors into the wall, or bolts through the beam
            double fixA = b.AnchorDiameterMm;
            w.Layer = LayerAnchor;
            double yfix = half - b.AngleLegAMm / 2;
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
            // dimensions: the gap (wall to rail back, the plan's number), the rail height, the width of the bracket
            var ob = new SteelDrawing.Obstacles();
            Chain(w, ob, new[] { 0.0, g }, new[] { N(g) }, span + 2 * Text, 0, span, true);
            Chain(w, ob, new[] { g, g + r.Profile.HeightMm }, new[] { N(r.Profile.HeightMm) }, span + 2 * Text, 0, span, true);
            double xDim = g + r.Profile.HeightMm + 1.5 * Text;
            Single(w, ob, -half, half, xDim, N(r.PlateWidth), false, 0);
            Column(w, PlanAssemblyCalls(r), xDim + 3.5 * Text, false);
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

        // ---------- the elevations (RailBrackets): the side view of the assembly, as the library draws it (x from the support, z up) ----------
        static void SlotIn(Database db, Transaction tr, BlockTableRecord btr, double x0, double x1, double zc, double h, string layer)
        {
            double r = h / 2;
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(x0 + r, zc - r), 1, 0, 0);
            pl.AddVertexAt(1, new Point2d(x1 - r, zc - r), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x1 - r, zc + r), 1, 0, 0);
            pl.AddVertexAt(3, new Point2d(x0 + r, zc + r), 0, 0, 0);
            pl.Closed = true;
            SteelDrawing.Put(btr, tr, pl, layer);
        }

        static ObjectId BracketElevationBlock(Database db, Transaction tr, Context c, Rail r)
        {
            return SteelDrawing.Block(db, tr, "SM_RAIL_BRACKET_ELEV_" + Key(r) + FixingTag(r) + (c.Steel ? "_BOLT" : "_ANCHOR"), btr =>
            {
                var b = r.Bracket;
                double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, la = r.ArmLeg, h = b.AngleLengthMm / 2;
                double d = b.AssemblyBoltDiameterMm, f = r.Profile.FootMm, ct = b.ClipThicknessMm, cd = b.ClipBoltDiameterMm;
                double[] bx = AssemblyBoltX(r);
                double zb = h - 45;
                // the fixing angle: its wall leg (edge on) and its arm leg (face on, with the slots of the assembly bolts)
                RectIn(db, tr, btr, 0, -h, t, h, LayerBracket);
                RectIn(db, tr, btr, t, -h, t + la, h, LayerBracket);
                // the rail angle: the part of its arm leg beyond the fixing angle, the base leg and the plate
                if (g - tp - t > t + la)
                    RectIn(db, tr, btr, t + la, -h, g - tp - t, h, LayerBracket);
                RectIn(db, tr, btr, g - tp - t, -h, g - tp, h, LayerBracket);
                RectIn(db, tr, btr, g - tp, -h, g, h, LayerBracket);
                foreach (int k in new[] { -1, 1 })
                {
                    SlotIn(db, tr, btr, bx[0] - 0.9 * d, bx[1] + 0.9 * d, k * zb, d + 3, LayerBracket);
                    foreach (double x in bx)
                    {
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(x, k * zb, 0), Vector3d.ZAxis, 0.85 * d), LayerClip);
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(x, k * zb, 0), Vector3d.ZAxis, d / 2), LayerClip);
                    }
                    // the clips (seen from the side) and the heads of their bolts
                    double zc = k * b.ClipLengthMm / 4;
                    RectIn(db, tr, btr, g - tp, k * b.ClipLengthMm / 4 - cd * 0.8, g + f + ct + 0.65 * cd, k * b.ClipLengthMm / 4 + cd * 0.8, LayerClip);
                    // the fixing of the wall leg
                    double fz = FixZ(c, r);
                    double fd = b.AnchorDiameterMm;
                    double far = r.CrossArm ? -c.Arm.ArmEndPlateThicknessMm : (c.Steel ? -(c.Beam.WidthMm) : -b.AnchorLengthMm);
                    RectIn(db, tr, btr, far, k * fz - fd / 2, t, k * fz + fd / 2, LayerAnchor);
                    RectIn(db, tr, btr, t, k * fz - fd * 0.8, t + 0.8 * fd, k * fz + fd * 0.8, LayerAnchor);
                }
                RectIn(db, tr, btr, g, -b.ClipLengthMm / 2, g + f + ct, b.ClipLengthMm / 2, LayerClip);
            });
        }

        static double FixZ(Context c, Rail r)
        {
            double len = r.Bracket.AngleLengthMm / 2;
            return c.Steel ? Math.Min(len - 30, c.Beam.HeightMm / 2 - 25) : len - 35;
        }

        static double ElevationLeftRoom(Context c, Rail r)
        {
            return (r.CrossArm ? r.Fixing.ArmLengthMm : 0) + (c.Steel ? c.Beam.WidthMm : Wall(r)) + 3 * Text + 26 * Text;
        }

        static double ElevationRightRoom(Rail r)
        {
            return r.Gap + r.Profile.HeightMm + 2.5 * Text + 3.5 * Text + 1.5 * Text + 27 * Text;
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
                double ox = x + ElevationLeftRoom(c, r);
                centres[i] = ox + (ElevationRightRoom(r) - ElevationLeftRoom(c, r)) / 2;
                DrawElevationOf(db, tr, c, r, ox, y0);
                x = ox + ElevationRightRoom(r) + 40;
            }
            var titles = FindTitles(db, tr, window, "Anchor Details");
            foreach (var t in titles)
            {
                t.UpgradeOpen();
                t.Location = new Point3d((centres[0] + centres[1]) / 2, t.Location.Y, 0);
            }
            return Extents(db, tr, before);
        }

        // a UPN beam end on, web at the support face (x = 0 side), at the centre z
        static void BeamSection(SteelDrawing.Writer w, ChannelProfile beam, double xFace, double z)
        {
            double b = beam.WidthMm, h = beam.HeightMm / 2, tw = beam.WebMm, tf = beam.FlangeMm;
            var pl = w.Poly(new[]
            {
                new[] { xFace, z + h }, new[] { xFace - b, z + h }, new[] { xFace - b, z + h - tf }, new[] { xFace - tw, z + h - tf },
                new[] { xFace - tw, z - h + tf }, new[] { xFace - b, z - h + tf }, new[] { xFace - b, z - h }, new[] { xFace, z - h }
            }, true);
            w.Fill(pl, "ANSI32", 1.2 * Text);
        }

        static void DrawElevationOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            double len = b.AngleLengthMm, h = len / 2;
            double g = r.Gap, t = b.AngleThicknessMm, h1 = r.Profile.HeightMm, f = r.Profile.FootMm;
            // the two brackets are drawn close together: the rail is broken between them and the dimension carries the real spacing
            double zLow = 0, zHigh = 3 * len;
            double over = 0.7 * len;
            double bottom = zLow - h - over, top = zHigh + h + over;
            double xs = r.CrossArm ? -Math.Max(r.Fixing.ArmLengthMm, 2 * c.Arm.ArmEndPlateThicknessMm + 20) : 0;   // the support face of the wall / beam
            double depth = c.Steel ? c.Beam.WidthMm : Wall(r);
            double et = c.Arm.ArmEndPlateThicknessMm;
            // the support behind the assembly: the wall, or the UPN beam end on at each bracket
            if (c.Steel)
            {
                w.Layer = SteelDrawing.LayerBeam;
                foreach (double z in new[] { zLow, zHigh })
                    BeamSection(w, c.Beam, xs, z);
            }
            else
            {
                w.Layer = LayerWall;
                w.Rect(xs - depth, bottom, xs, top);
            }
            // a cross arm: end plate on the support, the arm (side view), the head plate the fixing angle is bolted to
            if (r.CrossArm)
            {
                foreach (double z in new[] { zLow, zHigh })
                {
                    w.Layer = LayerBracket;
                    w.Rect(xs, z - c.Arm.ArmEndPlateMm / 2, xs + et, z + c.Arm.ArmEndPlateMm / 2);
                    w.Rect(-et, z - h, 0, z + h);
                    w.Layer = SteelDrawing.LayerBeam;
                    w.Rect(xs + et, z - c.Arm.ArmHeightMm / 2, -et, z + c.Arm.ArmHeightMm / 2);
                    w.Layer = LayerAnchor;
                    double ad = c.Arm.ArmAnchorDiameterMm, farA = c.Steel ? xs - depth : xs - Math.Min(depth - 10, b.AnchorLengthMm);
                    foreach (int k in new[] { -1, 1 })
                        w.Rect(farA, z + k * (c.Arm.ArmEndPlateMm / 2 - 20) - ad / 2, xs + et, z + k * (c.Arm.ArmEndPlateMm / 2 - 20) + ad / 2);
                }
            }
            // the rail in side view (its foot back at x = g, blade tip at g + h1) with its break marks
            w.Layer = LayerRail;
            double zm = (zLow + zHigh) / 2, gap = 0.9 * Text;
            foreach (double xx in new[] { g, g + f, g + h1 })
            {
                w.Seg(xx, bottom, xx, zm - gap / 2);
                w.Seg(xx, zm + gap / 2, xx, top);
            }
            foreach (double z in new[] { zm - gap / 2, zm + gap / 2 })
                w.Poly(new[] { new[] { g - 0.5 * Text, z }, new[] { g + h1 / 3, z + 0.35 * gap }, new[] { g + 2 * h1 / 3, z - 0.35 * gap }, new[] { g + h1 + 0.5 * Text, z } }, false);
            // the brackets: one named block at each level
            var block = BracketElevationBlock(db, tr, c, r);
            foreach (double z in new[] { zLow, zHigh })
            {
                w.Layer = LayerBracket;
                w.Insert(block, 0, z, 1, 0);
            }
            // dimensions: the real spacing (text = the spacing of the project), the bracket length
            double x1 = g + h1 + 2.5 * Text, x2 = x1 + 3.5 * Text;
            Dim(w, g + h1 + 0.3 * Text, zLow, g + h1 + 0.3 * Text, zHigh, x1, 90, N(r.SpacingMm));
            Dim(w, g + h1 + 0.3 * Text, zHigh - h, g + h1 + 0.3 * Text, zHigh + h, x2, 90, "");
            w.Layer = SteelDrawing.LayerAnno;
            w.Text(xs - depth, top + 3 * Text, Text * 1.2, r.Title + " RAIL " + r.Profile.Name);
            // labels of the lower assembly in one column right of the dimensions; the support, the fixing and the arm left
            double fz = FixZ(c, r), ct = b.ClipThicknessMm, d = b.AssemblyBoltDiameterMm;
            double[] bx = AssemblyBoltX(r);
            var right = new List<Call>
            {
                new Call { Ax = t + r.ArmLeg / 2, Ay = zLow + h - 4, Text = AngleName(r) },
                new Call { Ax = g - b.PlateThicknessMm / 2, Ay = zLow - h / 2, Text = PlateName(r) },
                new Call { Ax = bx[1], Ay = zLow - (h - 45), Text = AssemblyName(r) },
                new Call { Ax = g + f / 2, Ay = zLow + b.ClipLengthMm / 2 - 2, Text = ClipName(r) },
                new Call { Ax = g + f + ct + 0.3 * b.ClipBoltDiameterMm, Ay = zLow - b.ClipLengthMm / 4, Text = BoltsName(r) },
                new Call { Ax = g + h1, Ay = zLow + h + over / 2, Text = RailName(r) }
            };
            Column(w, right, x2 + 1.5 * Text, false);
            var left = new List<Call>
            {
                new Call { Ax = xs - depth / 2, Ay = zLow - h - over / 2, Text = SupportName(c, r.CrossArm) },
                new Call { Ax = (r.CrossArm ? -c.Arm.ArmEndPlateThicknessMm : (c.Steel ? -c.Beam.WidthMm : -b.AnchorLengthMm)) / 2, Ay = zLow - fz, Text = FixName(c, r) }
            };
            if (r.CrossArm)
                left.Add(new Call { Ax = xs / 2, Ay = zLow + c.Arm.ArmHeightMm / 2, Text = ArmName(c) });
            Column(w, left, xs - depth - 3 * Text, true);
            w.Layer = SteelDrawing.LayerAnno;
        }
    }
}
