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
                    double t = Bracket.AngleThicknessMm;
                    // the two horizontal legs (leg B each) overlapped by the preset overlap, plus the vertical leg of the rail angle
                    double nominal = 2 * Bracket.AngleLegBMm - Bracket.AssemblyOverlapMm + t;
                    if (Fixing == null || CrossArm)
                        return nominal;
                    return Math.Max(2 * t + 10, Fixing.ArmLengthMm);
                }
            }
            /// <summary>The length of the two arm legs: the preset leg, longer when the gap needs it (the legs grow so that they still
            /// overlap by the preset overlap).</summary>
            public double ArmLeg
            {
                get
                {
                    double t = Bracket.AngleThicknessMm;
                    return Math.Max(Bracket.AngleLegBMm, (Gap - t + Bracket.AssemblyOverlapMm) / 2);
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
            ArmBack = 0;
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

        // The bracket assembly as the user drew it (round 5, g1/user-edit) and as the photos show it: two L angles 270 long, lying
        // horizontally along the wall. The wall angle has its vertical leg (50) flat on the support (anchors, beam bolts or arm bolts) and
        // its horizontal leg (40) out from the wall; the rail angle has its horizontal leg (40) lying on the wall angle's leg, joined by the
        // 4 assembly bolts through crossed slots (the overlap sets the gap), and its vertical leg (50) parallel to the wall, carrying the
        // rail foot with the 2 clips; the plate 270x50x4 backs the vertical leg on the wall side, the clip bolts go through clip, leg and
        // plate. Plan frame: x from the support face to the rail, y along the wall (the 270), the rail axis at y = 0.
        static string FixingTag(Rail r)
        {
            string type = r.CrossArm ? "ARM" : "WALL";
            double gap = r.CrossArm ? r.Fixing.ArmLengthMm : r.Gap;
            return "_" + (r.Family == RailBracketFields.Car ? "CAR" : "CW") + "_" + type + "_G" + Math.Round(gap).ToString(CultureInfo.InvariantCulture)
                + "_L" + Math.Round(r.Bracket.AngleLengthMm).ToString(CultureInfo.InvariantCulture);
        }

        // The anchor bolt of the user's drawing (SM_ANCHOR_BOLT), scaled so that its thread is the anchor diameter, its washer and nut on
        // the leg at xFace (the leg outside face), pointing into the support (-x).
        static BlockReference AnchorRef(Database db, Transaction tr, Point3d at, double dia, string layer)
        {
            double k = dia / (2 * AnchorBoltGeometry.ThreadRadius);
            var br = new BlockReference(at, AnchorBoltGeometry.Block(db, tr, layer));
            br.ScaleFactors = new Scale3d(k, k, 1);
            return br;
        }

        static double AnchorShift(double dia)
        {
            return AnchorBoltGeometry.WallFaceX * dia / (2 * AnchorBoltGeometry.ThreadRadius);
        }

        static void AnchorIn(Database db, Transaction tr, BlockTableRecord btr, double xFace, double y, double dia)
        {
            var br = AnchorRef(db, tr, new Point3d(xFace - AnchorShift(dia), y, 0), dia, LayerAnchor);
            SteelDrawing.Put(btr, tr, br, LayerAnchor);
        }

        static void Anchor(SteelDrawing.Writer w, Database db, Transaction tr, double xFace, double y, double dia)
        {
            w.Layer = LayerAnchor;
            w.Adopt(AnchorRef(db, tr, WorldOf(w, xFace - AnchorShift(dia), y), dia, LayerAnchor));
        }

        // the x of the assembly bolts: the middle of the overlap of the two horizontal legs
        static double AssemblyBoltX(Rail r)
        {
            double t = r.Bracket.AngleThicknessMm, g = r.Gap, la = r.ArmLeg;
            return ((g - t - la) + la) / 2;
        }

        // the y of the 4 assembly bolts along the 270
        static double[] AssemblyBoltY(Rail r)
        {
            double e = r.Bracket.AngleLengthMm / 2 - 31;
            return new[] { -e, -e + 40, e - 40, e };
        }

        static double ArmBack = 0;   // the arm width in a cross-arm plan block (set by the caller)

        static ObjectId BracketPlanBlock(Database db, Transaction tr, Rail r, bool clips = true, string prefix = "SM_RAIL_BRACKET_PLAN_")
        {
            double armBack = r.CrossArm ? ArmBack : 0;
            return SteelDrawing.Block(db, tr, prefix + Key(r) + FixingTag(r) + (clips ? "" : "_SYMBOL"), btr =>
            {
                var b = r.Bracket;
                double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, la = r.ArmLeg, half = b.AngleLengthMm / 2;
                double d = b.AssemblyBoltDiameterMm;
                if (armBack > 0)
                {
                    // on a cross arm: the wall angle's horizontal leg lies on the arm top and reaches back over it; 2 assembly bolts
                    // through it and the top flange of the arm, near the two ends
                    RectIn(db, tr, btr, -armBack, -half, la, half, LayerBracket);
                    foreach (int k in new[] { -1, 1 })
                    {
                        double yb = k * (half - 20);
                        SlotIn(db, tr, btr, -armBack / 2 - 12, -armBack / 2 + 12, yb, d + 2, LayerBracket);
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(-armBack / 2, yb, 0), Vector3d.ZAxis, 0.85 * d), LayerClip);
                        SteelDrawing.Put(btr, tr, new Circle(new Point3d(-armBack / 2, yb, 0), Vector3d.ZAxis, d / 2), LayerClip);
                    }
                }
                else
                {
                    // the wall angle: its vertical leg on the support (edge on) and its horizontal leg seen from above
                    RectIn(db, tr, btr, 0, -half, t, half, LayerBracket);
                    RectIn(db, tr, btr, t, -half, la, half, LayerBracket);
                }
                // the rail angle: its horizontal leg on top (seen from above) and its vertical leg (edge on); the plate behind that leg
                RectIn(db, tr, btr, g - t - la, -half, g - t, half, LayerBracket);
                RectIn(db, tr, btr, g - t, -half, g, half, LayerBracket);
                RectIn(db, tr, btr, g - t - tp, -half, g - t, half, LayerBracket);
                // the assembly bolts in crossed slots: a slot along x in the rail angle, a slot along y in the wall angle
                double xb = AssemblyBoltX(r);
                foreach (double y in AssemblyBoltY(r))
                {
                    SlotIn(db, tr, btr, xb - 15, xb + 15, y, d + 2, LayerBracket);
                    SlotYIn(db, tr, btr, xb, y - 15, y + 15, d + 2, LayerBracket);
                    SteelDrawing.Put(btr, tr, new Circle(new Point3d(xb, y, 0), Vector3d.ZAxis, 0.85 * d), LayerClip);
                    SteelDrawing.Put(btr, tr, new Circle(new Point3d(xb, y, 0), Vector3d.ZAxis, d / 2), LayerClip);
                }
                if (!clips)
                    return;
                double bw = r.Profile.WidthMm / 2, f = r.Profile.FootMm, ct = b.ClipThicknessMm, cd = b.ClipBoltDiameterMm;
                foreach (int s in new[] { -1, 1 })
                {
                    // the clip: a strap on the vertical leg, a riser against the foot edge and a lip over its front face
                    double yf = s * bw, yc = s * (bw + ct), yb = s * (bw + b.ClipWidthMm - 8), yl = s * (bw - 8);
                    Poly(db, tr, btr, new[]
                    {
                        new[] { g, yf }, new[] { g, yb }, new[] { g + ct, yb }, new[] { g + ct, yc }, new[] { g + f + ct, yc },
                        new[] { g + f + ct, yl }, new[] { g + f, yl }, new[] { g + f, yf }
                    }, true, LayerClip);
                    // the clip bolt through clip, leg and plate: head on the clip, nut behind the plate
                    double ybolt = s * ((bw + ct + (bw + b.ClipWidthMm - 8)) / 2);
                    RectIn(db, tr, btr, g + ct, ybolt - cd * 0.8, g + ct + 0.65 * cd, ybolt + cd * 0.8, LayerClip);
                    RectIn(db, tr, btr, g - t - tp - 0.8 * cd, ybolt - cd * 0.8, g - t - tp, ybolt + cd * 0.8, LayerClip);
                    RectIn(db, tr, btr, g - t - tp - 0.8 * cd, ybolt - cd / 2, g + ct + 0.65 * cd, ybolt + cd / 2, LayerClip);
                }
            });
        }

        // a slot along y (the slot of the wall angle)
        static void SlotYIn(Database db, Transaction tr, BlockTableRecord btr, double xc, double y0, double y1, double w, string layer)
        {
            double r = w / 2;
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(xc + r, y0 + r), 1, 0, 0);
            pl.AddVertexAt(1, new Point2d(xc - r, y0 + r), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(xc - r, y1 - r), 1, 0, 0);
            pl.AddVertexAt(3, new Point2d(xc + r, y1 - r), 0, 0, 0);
            pl.Closed = true;
            SteelDrawing.Put(btr, tr, pl, layer);
        }

        static void SlotIn(Database db, Transaction tr, BlockTableRecord btr, double x0, double x1, double zc, double h, string layer)
        {
            double r = h / 2;
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(x0 + r, zc - r), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x1 - r, zc - r), 1, 0, 0);
            pl.AddVertexAt(2, new Point2d(x1 - r, zc + r), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x0 + r, zc + r), 1, 0, 0);
            pl.Closed = true;
            SteelDrawing.Put(btr, tr, pl, layer);
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
                return "ASSEMBLY BOLTS 2 X M" + N(r.Bracket.AssemblyBoltDiameterMm) + "X" + N(r.Bracket.AssemblyBoltLengthMm);
            return c.Steel ? "BOLTS M" + N(r.Bracket.AnchorDiameterMm) : "ANCHOR BOLTS D" + N(r.Bracket.AnchorDiameterMm) + "x" + N(r.Bracket.AnchorLengthMm);
        }

        static string ArmName(Context c)
        {
            return "ST CROSS ARM";
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
                    int cross = 0, order = 0;
                    double len = 0;
                    for (int i = 0; i < n; i++)
                    {
                        len += Math.Sqrt(Math.Pow(xCol - items[perm[i]].Ax, 2) + Math.Pow(slots[i] - items[perm[i]].Ay, 2));
                        for (int j = i + 1; j < n; j++)
                        {
                            if (Cross(items[perm[i]].Ax, items[perm[i]].Ay, xCol, slots[i], items[perm[j]].Ax, items[perm[j]].Ay, xCol, slots[j]))
                                cross++;
                            // the column keeps the vertical order of what the labels point to (slots go down with i)
                            if (items[perm[i]].Ay < items[perm[j]].Ay - 1e-6)
                                order++;
                        }
                    }
                    double score = cross * 1e9 + order * 1e5 + len;
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
            double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, half = b.AngleLengthMm / 2;
            double ct = b.ClipThicknessMm, bw = r.Profile.WidthMm / 2;
            var by = AssemblyBoltY(r);
            return new List<Call>
            {
                new Call { Ax = r.ArmLeg / 2, Ay = half - 6, Text = AngleName(r) },
                new Call { Ax = g - t - tp / 2, Ay = half - 10, Text = PlateName(r) },
                new Call { Ax = AssemblyBoltX(r), Ay = by[3], Text = AssemblyName(r) },
                new Call { Ax = g + ct / 2, Ay = -(bw + ct + (b.ClipWidthMm - 8 - ct) / 2), Text = ClipName(r) },
                new Call { Ax = g + ct + 0.3 * b.ClipBoltDiameterMm, Ay = bw + (ct + b.ClipWidthMm - 8) / 2, Text = BoltsName(r) },
                new Call { Ax = g + r.Profile.HeightMm, Ay = 0, Text = RailName(r) }
            };
        }

        // the support face drawn in plan: the wall, the UPN beam, or the cross arm (seen from above, running along the wall leg)
        static void PlanSupport(SteelDrawing.Writer w, Context c, Rail r, double span, out double depth)
        {
            if (r.CrossArm)
            {
                depth = c.Arm.ArmWidthMm;
                w.Layer = SteelDrawing.LayerBeam;
                var arm = w.Rect(-depth, -span, 0, span);
                w.Fill(arm, "ANSI32", 1.2 * Text);
                return;
            }
            depth = c.Steel ? c.Beam.WidthMm : Wall(r);
            w.Layer = c.Steel ? SteelDrawing.LayerBeam : LayerWall;
            var box = w.Rect(-depth, -span, 0, span);
            if (c.Steel)
                w.Fill(box, "ANSI32", 1.2 * Text);
        }

        static void DrawPlanCrossArm(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            DrawPlanOf(db, tr, c, r, ox, oy);
        }

        static void DrawPlanOf(Database db, Transaction tr, Context c, Rail r, double ox, double oy)
        {
            var b = r.Bracket;
            var w = WriterAt(db, tr, ox, oy);
            double half = b.AngleLengthMm / 2;
            double g = r.Gap;
            double span = half + 2 * Text;
            double depth;
            PlanSupport(w, c, r, span, out depth);
            ArmBack = r.CrossArm ? depth : 0;
            // the assembly and the rail as blocks
            w.Layer = LayerBracket;
            w.Insert(BracketPlanBlock(db, tr, r), 0, 0, 1, 0);
            w.Layer = LayerRail;
            w.Insert(RailBlock(db, tr, r.Profile), g, 0, 1, 0);
            // the fixing of the wall leg near its two ends: anchors into the wall, bolts through the beam or the arm
            double fixA = b.AnchorDiameterMm;
            w.Layer = LayerAnchor;
            double yfix = half - 20;
            bool anchors = !c.Steel && !r.CrossArm;
            foreach (int s in new[] { -1, 1 })
            {
                if (r.CrossArm)
                    break;
                if (anchors)
                {
                    Anchor(w, db, tr, 0, s * yfix, fixA);
                    continue;
                }
                w.Rect(-depth, s * yfix - fixA / 2, b.AngleThicknessMm, s * yfix + fixA / 2);
                w.Rect(b.AngleThicknessMm, s * yfix - fixA * 0.8, b.AngleThicknessMm + 0.8 * fixA, s * yfix + fixA * 0.8);
                w.Rect(-depth - 0.65 * fixA, s * yfix - fixA * 0.8, -depth, s * yfix + fixA * 0.8);
            }
            // dimensions: the gap (support to rail back, the plan's number), the rail height, the bracket length
            var ob = new SteelDrawing.Obstacles();
            Chain(w, ob, new[] { 0.0, g }, new[] { N(g) }, span + 2 * Text, 0, span, true);
            double xDim = g + r.Profile.HeightMm + 1.5 * Text;
            Single(w, ob, -half, half, xDim, N(b.AngleLengthMm), false, 0);
            Column(w, PlanAssemblyCalls(r), xDim + 3.5 * Text, false);
            var left = new List<Call>
            {
                new Call { Ax = -depth / 2, Ay = -span, Text = r.CrossArm ? ArmName(c) : SupportName(c) },
                new Call { Ax = anchors ? -40 : -depth / 2, Ay = -yfix, Text = FixName(c, r) }
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

        // ---------- the elevations (RailBrackets): the section perpendicular to the wall, looking along it (x from the support, z up) ----------
        // The same assembly as VP5: every x equals the plan (gap, legs, overlap, rail). At a bracket level zc the horizontal legs meet at zc:
        // the wall angle's leg below (its vertical leg down the support), the rail angle's leg above (its vertical leg up, carrying the rail).
        static double LegUp(Rail r)
        {
            return r.Bracket.AngleLegAMm;
        }

        static ObjectId BracketElevationBlock(Database db, Transaction tr, Context c, Rail r)
        {
            return SteelDrawing.Block(db, tr, "SM_RAIL_BRACKET_SECTION_" + Key(r) + FixingTag(r) + (r.CrossArm ? "_ARMBOLT" : c.Steel ? "_BOLT" : "_ANCHOR"), btr =>
            {
                var b = r.Bracket;
                double t = b.AngleThicknessMm, tp = b.PlateThicknessMm, g = r.Gap, la = r.ArmLeg, up = LegUp(r);
                double d = b.AssemblyBoltDiameterMm, f = r.Profile.FootMm, ct = b.ClipThicknessMm, cd = b.ClipBoltDiameterMm;
                // the wall angle (L section): vertical leg on the support, horizontal leg out (top at z = 0)
                Poly(db, tr, btr, new[] { new[] { 0.0, -up }, new[] { t, -up }, new[] { t, -t }, new[] { la, -t }, new[] { la, 0.0 }, new[] { 0.0, 0.0 } }, true, LayerBracket);
                // the rail angle (L section): horizontal leg on top of the other, vertical leg up at the rail
                Poly(db, tr, btr, new[] { new[] { g - t - la, 0.0 }, new[] { g, 0.0 }, new[] { g, up }, new[] { g - t, up }, new[] { g - t, t }, new[] { g - t - la, t } }, true, LayerBracket);
                // the plate behind the vertical leg
                RectIn(db, tr, btr, g - t - tp, t, g - t, up, LayerBracket);
                // an assembly bolt (cut): vertical through the two horizontal legs, head on top, nut below
                double xb = AssemblyBoltX(r);
                RectIn(db, tr, btr, xb - d / 2, -t - 0.8 * d, xb + d / 2, t + 0.65 * d, LayerClip);
                RectIn(db, tr, btr, xb - 0.85 * d, t, xb + 0.85 * d, t + 0.65 * d, LayerClip);
                RectIn(db, tr, btr, xb - 0.85 * d, -t - 0.8 * d, xb + 0.85 * d, -t, LayerClip);
                // the clip on the foot and its bolt through clip, leg and plate
                double zc = (t + up) / 2;
                RectIn(db, tr, btr, g + f, zc - b.ClipLengthMm / 2 * 0.5, g + f + ct, zc + b.ClipLengthMm / 2 * 0.5, LayerClip);
                RectIn(db, tr, btr, g - t - tp - 0.8 * cd, zc - cd / 2, g + f + ct + 0.65 * cd, zc + cd / 2, LayerClip);
                RectIn(db, tr, btr, g + f + ct, zc - 0.8 * cd, g + f + ct + 0.65 * cd, zc + 0.8 * cd, LayerClip);
                RectIn(db, tr, btr, g - t - tp - 0.8 * cd, zc - 0.8 * cd, g - t - tp, zc + 0.8 * cd, LayerClip);
                // the fixing: anchors through the vertical leg (concrete), bolts through it and the beam web (steel), or on a cross arm
                // the horizontal leg lies on the arm top (it reaches back over the arm), bolted vertically through the top flange
                double fd = b.AnchorDiameterMm, fz = -up / 2;
                if (r.CrossArm)
                {
                    double aw = c.Arm.ArmWidthMm, xa = -aw / 2;
                    RectIn(db, tr, btr, -aw, -t, 0, 0, LayerBracket);
                    RectIn(db, tr, btr, xa - d / 2, -t - c.Arm.ArmFlangeMm - 0.8 * d, xa + d / 2, 0.65 * d, LayerClip);
                    RectIn(db, tr, btr, xa - 0.85 * d, 0, xa + 0.85 * d, 0.65 * d, LayerClip);
                    RectIn(db, tr, btr, xa - 0.85 * d, -t - c.Arm.ArmFlangeMm - 0.8 * d, xa + 0.85 * d, -t - c.Arm.ArmFlangeMm, LayerClip);
                }
                else if (c.Steel)
                {
                    RectIn(db, tr, btr, -c.Beam.WebMm - 2, fz - fd / 2, t + 0.8 * fd, fz + fd / 2, LayerAnchor);
                    RectIn(db, tr, btr, t, fz - fd * 0.8, t + 0.8 * fd, fz + fd * 0.8, LayerAnchor);
                }
                else
                    AnchorIn(db, tr, btr, 0, fz, fd);
            });
        }

        static double FixZ(Context c, Rail r)
        {
            return LegUp(r) / 2;
        }

        static double ElevationLeftRoom(Context c, Rail r)
        {
            return (r.CrossArm ? c.Arm.ArmWidthMm : (c.Steel ? c.Beam.WidthMm : Wall(r))) + 3 * Text + 26 * Text;
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
            double up = LegUp(r), t = b.AngleThicknessMm;
            double g = r.Gap, h1 = r.Profile.HeightMm, f = r.Profile.FootMm;
            // two levels drawn close together: the rail is broken between them and the dimension carries the real spacing
            double zLow = 0, zHigh = 12 * up;
            double over = 3 * up;
            double bottom = zLow - up - over, top = zHigh + up + over;
            double depth = r.CrossArm ? c.Arm.ArmWidthMm : (c.Steel ? c.Beam.WidthMm : Wall(r));
            // the support: the wall band, the UPN beam end on, or the cross arm end on (it runs along the wall)
            if (r.CrossArm)
            {
                w.Layer = SteelDrawing.LayerBeam;
                foreach (double z in new[] { zLow, zHigh })
                {
                    var arm = w.Rect(-depth, z - t - c.Arm.ArmHeightMm, 0, z - t);
                    w.Fill(arm, "ANSI32", 1.2 * Text);
                }
            }
            else if (c.Steel)
            {
                w.Layer = SteelDrawing.LayerBeam;
                foreach (double z in new[] { zLow, zHigh })
                    BeamSection(w, c.Beam, 0, z - up / 2);
            }
            else
            {
                w.Layer = LayerWall;
                w.Rect(-depth, bottom, 0, top);
            }
            // the rail in side view (foot back at x = g, blade tip at g + h1) with its break marks
            w.Layer = LayerRail;
            double zm = (zLow + zHigh) / 2, gap = 0.9 * Text;
            foreach (double xx in new[] { g, g + f, g + h1 })
            {
                w.Seg(xx, bottom, xx, zm - gap / 2);
                w.Seg(xx, zm + gap / 2, xx, top);
            }
            foreach (double z in new[] { zm - gap / 2, zm + gap / 2 })
                w.Poly(new[] { new[] { g - 0.5 * Text, z }, new[] { g + h1 / 3, z + 0.35 * gap }, new[] { g + 2 * h1 / 3, z - 0.35 * gap }, new[] { g + h1 + 0.5 * Text, z } }, false);
            var block = BracketElevationBlock(db, tr, c, r);
            foreach (double z in new[] { zLow, zHigh })
            {
                w.Layer = LayerBracket;
                w.Insert(block, 0, z, 1, 0);
            }
            // dimensions: the real spacing, the gap and the rail height at the lower level (the same numbers as VP5)
            double x1 = g + h1 + 3.5 * Text;
            Dim(w, g + h1 + 0.3 * Text, zLow, g + h1 + 0.3 * Text, zHigh, x1, 90, N(r.SpacingMm));
            Chain(w, null, new[] { 0.0, g }, new[] { N(g) }, zLow - up - 2.5 * Text, 0, zLow - up, false);
            w.Layer = SteelDrawing.LayerAnno;
            w.Text(-depth, top + 3 * Text, Text * 1.2, r.Title + " RAIL " + r.Profile.Name);
            double ct = b.ClipThicknessMm, d = b.AssemblyBoltDiameterMm;
            var right = new List<Call>
            {
                new Call { Ax = g - t / 2, Ay = zLow + up - 3, Text = AngleName(r) },
                new Call { Ax = g - t - b.PlateThicknessMm / 2, Ay = zLow + up * 0.75, Text = PlateName(r) },
                new Call { Ax = AssemblyBoltX(r), Ay = zLow + t + 0.65 * d, Text = AssemblyName(r) },
                new Call { Ax = g + f + ct / 2, Ay = zLow + (t + up) / 2 + b.ClipLengthMm / 4 - 1, Text = ClipName(r) },
                new Call { Ax = g + f + ct + 0.3 * b.ClipBoltDiameterMm, Ay = zLow + (t + up) / 2, Text = BoltsName(r) },
                new Call { Ax = g + h1, Ay = zLow + up + over / 2, Text = RailName(r) }
            };
            Column(w, right, x1 + 3 * Text, false);
            bool anchors = !c.Steel && !r.CrossArm;
            var left = new List<Call>
            {
                new Call { Ax = -depth / 2, Ay = r.CrossArm ? zLow - c.Arm.ArmHeightMm / 2 : (c.Steel ? zLow - up / 2 - 30 : zLow - up - over / 2), Text = r.CrossArm ? ArmName(c) : SupportName(c) },
                new Call { Ax = r.CrossArm ? -depth / 2 : (anchors ? -40 : -depth / 2), Ay = r.CrossArm ? zLow + 2 : zLow - up / 2, Text = FixName(c, r) }
            };
            Column(w, left, -depth - 3 * Text, true);
            w.Layer = SteelDrawing.LayerAnno;
        }
    }
}
