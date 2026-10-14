using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Steel
{
    /// <summary>The effective steel-shaft inputs of a project (the project value when edited, otherwise the preset) and the shaft quantities they need.</summary>
    public sealed class SteelInputs
    {
        public SteelShaftPreset Preset;
        public string Construction;
        public ChannelProfile Column;
        public ChannelProfile Beam;
        public AngleProfile Brace;
        /// <summary>s: the effective car rail bracket spacing of the project (the rails drive the beam levels).</summary>
        public double SpacingMm;
        public double PlateWidthMm;
        public double PlateDepthMm;
        public double PlateThicknessMm;
        public int BoltCount;
        public double BoltDiameterMm;
        public double BoltPitchMm;
        public double GussetLengthMm;
        public double GussetWidthMm;
        public double GussetThicknessMm;
        public string SteelGrade;
        public double BaseMarginMm;
        public double BaseThicknessMm;
        public double BarDiameterMm;
        public int BarCountAlongX;
        public int BarCountAlongY;
        public double SlabEdgeDepthMm;
        public double SlabEdgeWidthMm;
        public double HousingHeightMm;
        public double HousingOverhangMm;
        public double HousingWallMm;
        public double HousingRoofMm;
        /// <summary>Clear shaft width W and depth D: between the inner faces of the steel.</summary>
        public double WidthMm;
        public double DepthMm;
        public double PitMm;
        public double OverheadMm;
        /// <summary>Door construction height above the floor (the door-head beam), from the shared design result.</summary>
        public double DoorHeightMm;
        /// <summary>The floors with their elevations (all of them; the rule uses the served ones).</summary>
        public List<FloorElevation> Floors = new List<FloorElevation>();
        /// <summary>The level edits of the user per face (Front, Back, Left, Right).</summary>
        public Dictionary<string, List<SteelLevelEdit>> LevelEdits = new Dictionary<string, List<SteelLevelEdit>>();
        /// <summary>What is missing or invalid; empty when the structure can be built.</summary>
        public List<string> Problems = new List<string>();

        public bool Valid
        {
            get { return Problems.Count == 0; }
        }

        static double D(PresetField f, PresetContext c)
        {
            var v = f.Effective(c);
            return v == null ? Double.NaN : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        static string S(PresetField f, PresetContext c)
        {
            return f.Effective(c) as string;
        }

        static double? Field(PresetContext c, string key)
        {
            var f = ProjectPresets.Find(key);
            var v = f == null ? null : f.Effective(c);
            return v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        /// <summary>Everything except the base bar counts (which follow the footprint these values give).</summary>
        public static SteelInputs ResolveBasic(PresetContext c)
        {
            var i = new SteelInputs { Preset = c.Library.SteelShaftOrDefault() };
            i.Construction = S(SteelFields.Construction, c);
            i.Column = i.Preset.Channel(S(SteelFields.ColumnProfile, c));
            i.Beam = i.Preset.Channel(S(SteelFields.BeamProfile, c));
            i.Brace = i.Preset.Angle(S(SteelFields.BracingProfile, c));
            if (i.Column == null)
                i.Problems.Add("The column profile is not in the channel catalogue.");
            if (i.Beam == null)
                i.Problems.Add("The beam profile is not in the channel catalogue.");
            if (i.Brace == null)
                i.Problems.Add("The bracing profile is not in the angle catalogue.");
            var car = Field(c, ProjectPresets.CarSpacingKey);
            i.SpacingMm = car.HasValue ? car.Value : Double.NaN;
            i.PlateWidthMm = D(SteelFields.PlateWidth, c);
            i.PlateDepthMm = D(SteelFields.PlateDepth, c);
            i.PlateThicknessMm = D(SteelFields.PlateThickness, c);
            double bolts = D(SteelFields.BoltCount, c);
            i.BoltCount = Double.IsNaN(bolts) ? 0 : (int)Math.Round(bolts);
            i.BoltDiameterMm = D(SteelFields.BoltDiameter, c);
            i.BoltPitchMm = i.Preset.BoltPitchMm;
            i.GussetLengthMm = D(SteelFields.GussetLength, c);
            i.GussetWidthMm = D(SteelFields.GussetWidth, c);
            i.GussetThicknessMm = D(SteelFields.GussetThickness, c);
            i.SteelGrade = S(SteelFields.SteelGrade, c);
            i.BaseMarginMm = D(SteelFields.BaseMargin, c);
            i.BaseThicknessMm = D(SteelFields.BaseThickness, c);
            i.BarDiameterMm = D(SteelFields.BarDiameter, c);
            i.SlabEdgeDepthMm = D(SteelFields.SlabEdgeDepth, c);
            i.SlabEdgeWidthMm = D(SteelFields.SlabEdgeWidth, c);
            i.HousingHeightMm = D(SteelFields.HousingHeight, c);
            i.HousingOverhangMm = D(SteelFields.HousingOverhang, c);
            i.HousingWallMm = i.Preset.HousingWallMm;
            i.HousingRoofMm = i.Preset.HousingRoofMm;
            foreach (var pair in new[]
            {
                new KeyValuePair<string, double>("The car rail bracket spacing", i.SpacingMm),
                new KeyValuePair<string, double>("The base plate width", i.PlateWidthMm),
                new KeyValuePair<string, double>("The base plate depth", i.PlateDepthMm),
                new KeyValuePair<string, double>("The base plate thickness", i.PlateThicknessMm),
                new KeyValuePair<string, double>("The bolt diameter", i.BoltDiameterMm),
                new KeyValuePair<string, double>("The gusset plate length", i.GussetLengthMm),
                new KeyValuePair<string, double>("The gusset plate width", i.GussetWidthMm),
                new KeyValuePair<string, double>("The gusset plate thickness", i.GussetThicknessMm),
                new KeyValuePair<string, double>("The concrete base margin", i.BaseMarginMm),
                new KeyValuePair<string, double>("The concrete base thickness", i.BaseThicknessMm),
                new KeyValuePair<string, double>("The base bar diameter", i.BarDiameterMm),
                new KeyValuePair<string, double>("The building slab edge depth", i.SlabEdgeDepthMm),
                new KeyValuePair<string, double>("The building slab edge width", i.SlabEdgeWidthMm),
                new KeyValuePair<string, double>("The machine housing height", i.HousingHeightMm),
                new KeyValuePair<string, double>("The housing roof overhang", i.HousingOverhangMm)
            })
            {
                if (!ProjectFloors.FinitePositive(pair.Value))
                    i.Problems.Add(pair.Key + " must be positive.");
            }
            if (i.BoltCount != 4 && i.BoltCount != 6 && i.BoltCount != 8)
                i.Problems.Add("The bolts per base plate must be 4, 6 or 8.");
            var shaft = c.Project.Shaft;
            i.WidthMm = shaft == null ? 0 : shaft.WidthMm;
            i.DepthMm = shaft == null ? 0 : shaft.DepthMm;
            if (!ProjectFloors.FinitePositive(i.WidthMm) || !ProjectFloors.FinitePositive(i.DepthMm))
                i.Problems.Add("The shaft width and depth are required.");
            double? pit = Field(c, "Shaft.PitDepthMm"), overhead = Field(c, "Shaft.OverheadMm");
            if (!pit.HasValue || !(pit.Value > 0))
                i.Problems.Add("The pit depth is required.");
            else
                i.PitMm = pit.Value;
            if (!overhead.HasValue || !(overhead.Value > 0))
                i.Problems.Add("The overhead is required.");
            else
                i.OverheadMm = overhead.Value;
            i.DoorHeightMm = new ElevatorDesignResult().DoorConstructionHeightMm;
            try
            {
                i.Floors = ProjectFloors.Elevations(c.Project.Building);
            }
            catch (ArgumentException e)
            {
                i.Problems.Add(e.Message);
            }
            if (i.Floors.Count(f => f.IsServed) < 1)
                i.Problems.Add("At least one served floor is required.");
            foreach (string face in SteelFields.Faces)
                i.LevelEdits[face] = SteelFields.LevelEdits(c.Project, face);
            return i;
        }

        /// <summary>The effective inputs, including the base bar counts.</summary>
        public static SteelInputs Resolve(PresetContext c)
        {
            var i = ResolveBasic(c);
            i.BarCountAlongX = (int)Math.Round(D(SteelFields.BarCountAlongX, c));
            i.BarCountAlongY = (int)Math.Round(D(SteelFields.BarCountAlongY, c));
            if (i.BarCountAlongX < 1 || i.BarCountAlongY < 1)
                i.Problems.Add("The base bar counts must be at least 1.");
            return i;
        }
    }

    /// <summary>
    /// The steel shaft of a project as data: the beam levels, the derived rail bracket spacing, the plan, the face elevations, the base
    /// block, the tower top and the member list (DESIGN-FLOW "Steel shaft structure"). Pure: no drawing, no structural check.
    /// </summary>
    public sealed class SteelStructure
    {
        const double SteelDensityKgPerM3 = 7850;

        public SteelInputs Inputs;
        public bool Valid;
        /// <summary>Elevation of the column feet (the pit floor): lowest floor minus the pit depth.</summary>
        public double BaseMm;
        /// <summary>Elevation of the tower top (the machine platform): top floor plus the overhead.</summary>
        public double TopMm;
        public double LowestFloorMm;
        public double HighestFloorMm;
        /// <summary>The levels the rule gives per face (Front, Back, Left, Right), before the edits of the user.</summary>
        public Dictionary<string, List<SteelLevel>> RuleLevels = new Dictionary<string, List<SteelLevel>>();
        /// <summary>The levels in force per face: the rule with the edits applied, ordered by elevation.</summary>
        public Dictionary<string, List<SteelLevel>> Levels = new Dictionary<string, List<SteelLevel>>();
        /// <summary>The largest gap between consecutive levels from the lowest to the highest floor over the side and back faces: the
        /// spacing the rail check uses (not more than s by construction); null with fewer than two served floors.</summary>
        public double? LargestGapMm;
        /// <summary>Rules the levels cannot meet (for the Design Notes).</summary>
        public List<string> Issues = new List<string>();
        public SteelPlan Plan;
        public List<SteelFace> Faces = new List<SteelFace>();
        public SteelBaseBlock Base;
        public SteelTowerTop TowerTop;
        /// <summary>The building slab edge in front of the entrance face (plan), directly outside the front beam.</summary>
        public SteelRect SlabEdge;
        /// <summary>The machine housing on top of the tower (walls on the column envelope, roof plate with an overhang).</summary>
        public SteelHousing Housing;
        public List<SteelMember> Members = new List<SteelMember>();

        public double TotalMassKg
        {
            get { return Members.Sum(m => m.TotalMassKg); }
        }

        public double ColumnLengthMm
        {
            get { return TopMm - BaseMm; }
        }

        // ---------- entry points ----------
        /// <summary>True when the project is a steel structure.</summary>
        public static bool IsSteel(ElevatorProject p, PresetLibrary lib)
        {
            return SteelFields.IsSteel(p, lib);
        }

        /// <summary>Builds the steel shaft of a project (whatever its construction field says).</summary>
        public static SteelStructure Build(ElevatorProject p, PresetLibrary lib)
        {
            return Build(new PresetContext { Project = p, Library = lib });
        }

        public static SteelStructure Build(PresetContext c)
        {
            var s = new SteelStructure { Inputs = SteelInputs.Resolve(c) };
            if (!s.Inputs.Valid)
                return s;
            s.Valid = true;
            s.BuildLevels();
            s.BuildPlan();
            s.BuildBase();
            s.BuildFaces();
            s.BuildTowerTop();
            s.BuildSlabAndHousing();
            s.BuildMembers();
            return s;
        }

        /// <summary>The spacing the rail check uses on a steel project: the largest gap of the side and back faces; null when the project is not steel or the levels cannot be built.</summary>
        public static double? CheckSpacing(PresetContext c)
        {
            if (c == null || c.Library == null || c.Project == null || !SteelFields.IsSteel(c.Project, c.Library))
                return null;
            var s = Build(new PresetContext { Project = c.Project, Library = c.Library, Design = c.Design });
            return s.Valid ? s.LargestGapMm : null;
        }

        /// <summary>The faces whose beams carry the rail brackets (side and back).</summary>
        public static readonly string[] BracketFaces = { "Back", "Left", "Right" };

        /// <summary>The preset of a base bar count: bars at no more than the preset spacing across the block, from the cover to the cover.</summary>
        public static object PresetBarCount(PresetContext c, bool alongX)
        {
            var i = SteelInputs.ResolveBasic(c);
            if (i.Column == null || !ProjectFloors.FinitePositive(i.WidthMm) || !ProjectFloors.FinitePositive(i.DepthMm)
                || !ProjectFloors.FinitePositive(i.PlateWidthMm) || !ProjectFloors.FinitePositive(i.PlateDepthMm)
                || !ProjectFloors.FinitePositive(i.BaseMarginMm))
                return null;
            var block = BlockRectangle(i);
            var b = i.Preset.Base;
            double across = alongX ? block.Height : block.Width;
            return (double)Math.Max(2, (int)Math.Ceiling((across - 2 * b.CoverMm - 1e-9) / b.BarSpacingMm) + 1);
        }

        // ---------- levels ----------
        void BuildLevels()
        {
            var i = Inputs;
            var served = i.Floors.Where(f => f.IsServed).OrderBy(f => f.ElevationMm).ToList();
            LowestFloorMm = served[0].ElevationMm;
            HighestFloorMm = served[served.Count - 1].ElevationMm;
            BaseMm = LowestFloorMm - i.PitMm;
            TopMm = HighestFloorMm + i.OverheadMm;
            foreach (string face in SteelFields.Faces)
            {
                var rule = SteelBeamLevels.Rule(served, i.DoorHeightMm, i.SpacingMm, BaseMm, TopMm, face == "Front");
                RuleLevels[face] = rule;
                Levels[face] = SteelBeamLevels.Apply(rule, i.LevelEdits[face], BaseMm, TopMm);
            }
            Issues = SteelBeamLevels.Issues(served, i.DoorHeightMm, i.SpacingMm, BaseMm, TopMm);
            if (served.Count >= 2)
                LargestGapMm = BracketFaces.Max(f => SteelBeamLevels.LargestGap(Levels[f], LowestFloorMm, HighestFloorMm));
        }

        // ---------- plan ----------
        static SteelRect FootprintOf(SteelInputs i, ChannelProfile col)
        {
            double b = col.WidthMm, h = col.HeightMm, t = i.Beam.WidthMm;
            // The column box centres: x = -t + b/2 (left) and W + t - b/2 (right); y = -t + h/2 (front) and D + t - h/2 (back).
            double x0 = -t + b / 2 - i.PlateWidthMm / 2, x1 = i.WidthMm + t - b / 2 + i.PlateWidthMm / 2;
            double y0 = -t + h / 2 - i.PlateDepthMm / 2, y1 = i.DepthMm + t - h / 2 + i.PlateDepthMm / 2;
            return new SteelRect(x0, y0, x1, y1);
        }

        static SteelRect BlockRectangle(SteelInputs i)
        {
            return FootprintOf(i, i.Column).Grow(i.BaseMarginMm);
        }

        SteelColumnPlan MakeColumn(string corner, bool left, bool front)
        {
            var i = Inputs;
            var col = i.Column;
            double b = col.WidthMm, h = col.HeightMm, tw = col.WebMm, tf = col.FlangeMm, t = i.Beam.WidthMm;
            // The outer faces of the columns are the outer faces of the ring beams, which stand against the clear faces (user's edit of the
            // S3 sheet): the box is the beam flange width (t) outside the clear rectangle and reaches b / h into it.
            double xa = left ? -t : i.WidthMm + t - b, xb = left ? -t + b : i.WidthMm + t;
            double ya = front ? -t : i.DepthMm + t - h, yb = front ? -t + h : i.DepthMm + t;
            var c = new SteelColumnPlan { Corner = corner, Profile = col.Name, WebSide = left ? "Left" : "Right", Box = new SteelRect(xa, ya, xb, yb) };
            // Left outline: web at the outer side (xa), flanges to xb. The right column is its mirror image about the column's own box.
            var pts = new List<SteelPoint>
            {
                new SteelPoint(xa, ya), new SteelPoint(xb, ya), new SteelPoint(xb, ya + tf), new SteelPoint(xa + tw, ya + tf),
                new SteelPoint(xa + tw, yb - tf), new SteelPoint(xb, yb - tf), new SteelPoint(xb, yb), new SteelPoint(xa, yb)
            };
            if (left)
                c.Outline = pts;
            else
            {
                c.Outline = pts.Select(p => new SteelPoint(xa + xb - p.X, p.Y)).Reverse().ToList();
            }
            double cx = c.Box.CenterX, cy = c.Box.CenterY;
            c.Plate = new SteelRect(cx - i.PlateWidthMm / 2, cy - i.PlateDepthMm / 2, cx + i.PlateWidthMm / 2, cy + i.PlateDepthMm / 2);
            double k = i.BoltPitchMm / 2;
            var offsets = new List<double[]> { new[] { -k, -k }, new[] { k, -k }, new[] { -k, k }, new[] { k, k } };
            if (i.BoltCount >= 6)
            {
                offsets.Add(new[] { -k, 0.0 });
                offsets.Add(new[] { k, 0.0 });
            }
            if (i.BoltCount == 8)
            {
                offsets.Add(new[] { 0.0, -k });
                offsets.Add(new[] { 0.0, k });
            }
            foreach (var o in offsets)
                c.Bolts.Add(new SteelCircle { X = cx + o[0], Y = cy + o[1], DiameterMm = i.BoltDiameterMm });
            return c;
        }

        List<SteelBeamPlan> MakeRing()
        {
            var i = Inputs;
            // The beams stand against the clear faces, outside the clear rectangle, and run between the columns; the columns are flush with
            // their outer faces: one flat outer face per side (column and beam), fixed directly to the facade of the building.
            double bb = i.Beam.WidthMm, w = i.WidthMm, d = i.DepthMm, cw = i.Column.WidthMm, ch = i.Column.HeightMm;
            double runX0 = -bb + cw, runX1 = w + bb - cw, runY0 = -bb + ch, runY1 = d + bb - ch;
            var ring = new List<SteelBeamPlan>();
            ring.Add(new SteelBeamPlan { Face = "Front", Profile = i.Beam.Name, Box = new SteelRect(runX0, -bb, runX1, 0), CenterLine = new SteelSegment(runX0, -bb / 2, runX1, -bb / 2) });
            ring.Add(new SteelBeamPlan { Face = "Back", Profile = i.Beam.Name, Box = new SteelRect(runX0, d, runX1, d + bb), CenterLine = new SteelSegment(runX0, d + bb / 2, runX1, d + bb / 2) });
            ring.Add(new SteelBeamPlan { Face = "Left", Profile = i.Beam.Name, Box = new SteelRect(-bb, runY0, 0, runY1), CenterLine = new SteelSegment(-bb / 2, runY0, -bb / 2, runY1) });
            ring.Add(new SteelBeamPlan { Face = "Right", Profile = i.Beam.Name, Box = new SteelRect(w, runY0, w + bb, runY1), CenterLine = new SteelSegment(w + bb / 2, runY0, w + bb / 2, runY1) });
            return ring;
        }

        List<SteelColumnPlan> MakeColumns()
        {
            return new List<SteelColumnPlan>
            {
                MakeColumn("FrontLeft", true, true),
                MakeColumn("FrontRight", false, true),
                MakeColumn("BackLeft", true, false),
                MakeColumn("BackRight", false, false)
            };
        }

        void BuildPlan()
        {
            var i = Inputs;
            var plan = new SteelPlan { Clear = new SteelRect(0, 0, i.WidthMm, i.DepthMm), Columns = MakeColumns(), Beams = MakeRing() };
            plan.PlateFootprint = new SteelRect(
                plan.Columns.Min(c => c.Plate.X0), plan.Columns.Min(c => c.Plate.Y0), plan.Columns.Max(c => c.Plate.X1), plan.Columns.Max(c => c.Plate.Y1));
            double intrusion = 0;
            foreach (var c in plan.Columns)
            {
                if (c.Plate.Overlaps(plan.Clear))
                {
                    double ox = Math.Min(c.Plate.X1, plan.Clear.X1) - Math.Max(c.Plate.X0, plan.Clear.X0);
                    double oy = Math.Min(c.Plate.Y1, plan.Clear.Y1) - Math.Max(c.Plate.Y0, plan.Clear.Y0);
                    intrusion = Math.Max(intrusion, Math.Max(ox, oy));
                }
            }
            plan.PlateIntrusionMm = intrusion;
            Plan = plan;
        }

        // ---------- concrete base ----------
        void BuildBase()
        {
            var i = Inputs;
            var block = BlockRectangle(i);
            var b = new SteelBaseBlock
            {
                Plan = block,
                TopMm = BaseMm,
                BottomMm = BaseMm - i.BaseThicknessMm,
                ThicknessMm = i.BaseThicknessMm,
                BarDiameterMm = i.BarDiameterMm,
                CoverMm = i.Preset.Base.CoverMm,
                VolumeM3 = block.Width * block.Height * i.BaseThicknessMm * 1e-9
            };
            double cover = b.CoverMm;
            foreach (double y in Spread(block.Y0 + cover, block.Y1 - cover, i.BarCountAlongX))
                b.BarsAlongX.Add(new SteelSegment(block.X0 + cover, y, block.X1 - cover, y));
            foreach (double x in Spread(block.X0 + cover, block.X1 - cover, i.BarCountAlongY))
                b.BarsAlongY.Add(new SteelSegment(x, block.Y0 + cover, x, block.Y1 - cover));
            Base = b;
        }

        static IEnumerable<double> Spread(double from, double to, int count)
        {
            if (count <= 1)
            {
                yield return (from + to) / 2;
                yield break;
            }
            for (int k = 0; k < count; k++)
                yield return from + (to - from) * k / (count - 1);
        }

        // ---------- faces ----------
        void BuildFaces()
        {
            var i = Inputs;
            double halfBand = i.Beam.HeightMm / 2;
            foreach (string name in SteelFields.Faces)
            {
                var panels = new List<double> { BaseMm };
                panels.AddRange(Levels[name].Select(l => l.ElevationMm));
                bool alongX = name == "Front" || name == "Back";
                var face = new SteelFace { Name = name, IsEntrance = name == Plan.EntranceFace, LengthMm = alongX ? i.WidthMm : i.DepthMm };
                foreach (var c in Plan.Columns.Where(c => FaceHasColumn(name, c)))
                {
                    face.Columns.Add(alongX ? new SteelRect(c.Box.X0, BaseMm, c.Box.X1, TopMm) : new SteelRect(c.Box.Y0, BaseMm, c.Box.Y1, TopMm));
                    face.Plates.Add(alongX
                        ? new SteelRect(c.Plate.X0, BaseMm, c.Plate.X1, BaseMm + i.PlateThicknessMm)
                        : new SteelRect(c.Plate.Y0, BaseMm, c.Plate.Y1, BaseMm + i.PlateThicknessMm));
                }
                foreach (var l in Levels[name])
                {
                    face.Beams.Add(new SteelBeamLine
                    {
                        Key = l.Key,
                        Kind = l.Kind,
                        ElevationMm = l.ElevationMm,
                        U0 = 0,
                        U1 = face.LengthMm,
                        Z0 = l.ElevationMm - halfBand,
                        Z1 = l.ElevationMm + halfBand
                    });
                }
                face.BaseBlock = alongX
                    ? new SteelRect(Base.Plan.X0, Base.BottomMm, Base.Plan.X1, Base.TopMm)
                    : new SteelRect(Base.Plan.Y0, Base.BottomMm, Base.Plan.Y1, Base.TopMm);
                if (!face.IsEntrance)
                {
                    for (int p = 0; p + 1 < panels.Count; p++)
                    {
                        double z0 = panels[p], z1 = panels[p + 1];
                        var brace = new SteelBrace
                        {
                            Panel = p,
                            Z0 = z0,
                            Z1 = z1,
                            U0 = 0,
                            U1 = face.LengthMm,
                            DiagonalUp = new SteelSegment(0, z0, face.LengthMm, z1),
                            DiagonalDown = new SteelSegment(0, z1, face.LengthMm, z0)
                        };
                        brace.LengthMm = brace.DiagonalUp.Length;
                        face.Braces.Add(brace);
                    }
                    foreach (double z in panels)
                    {
                        face.Gussets.Add(new SteelPoint(0, z));
                        face.Gussets.Add(new SteelPoint(face.LengthMm, z));
                    }
                }
                Faces.Add(face);
            }
        }

        static bool FaceHasColumn(string face, SteelColumnPlan c)
        {
            switch (face)
            {
                case "Front": return c.Corner.StartsWith("Front", StringComparison.Ordinal);
                case "Back": return c.Corner.StartsWith("Back", StringComparison.Ordinal);
                case "Left": return c.Corner.EndsWith("Left", StringComparison.Ordinal);
                default: return c.Corner.EndsWith("Right", StringComparison.Ordinal);
            }
        }

        // ---------- tower top ----------
        void BuildTowerTop()
        {
            var top = new SteelTowerTop
            {
                ElevationMm = TopMm,
                Clear = Plan.Clear,
                MachineEnvelope = Plan.Clear,
                Outriggers = false,
                Columns = MakeColumns(),
                Beams = MakeRing()
            };
            double x0 = top.Columns.Min(c => c.Box.X0), y0 = top.Columns.Min(c => c.Box.Y0);
            double x1 = top.Columns.Max(c => c.Box.X1), y1 = top.Columns.Max(c => c.Box.Y1);
            top.Outline = new SteelRect(x0, y0, x1, y1);
            TowerTop = top;
        }

        // ---------- the building slab edge and the machine housing ----------
        void BuildSlabAndHousing()
        {
            var i = Inputs;
            double bb = i.Beam.WidthMm;
            double cx = i.WidthMm / 2, half = i.SlabEdgeWidthMm / 2;
            SlabEdge = new SteelRect(cx - half, -bb - i.SlabEdgeDepthMm, cx + half, -bb);
            double deck = TopMm + i.Beam.HeightMm / 2;
            Housing = new SteelHousing
            {
                Plan = TowerTop.Outline,
                DeckMm = deck,
                HeightMm = i.HousingHeightMm,
                WallMm = i.HousingWallMm,
                RoofMm = i.HousingRoofMm,
                OverhangMm = i.HousingOverhangMm,
                RoofBottomMm = deck + i.HousingHeightMm,
                RoofTopMm = deck + i.HousingHeightMm + i.HousingRoofMm
            };
        }

        // ---------- member list ----------
        static double PlateKg(double a, double b, double t)
        {
            return a * b * t * 1e-9 * SteelDensityKgPerM3;
        }

        void BuildMembers()
        {
            var i = Inputs;
            int alongWidth = Levels["Front"].Count + Levels["Back"].Count, alongDepth = Levels["Left"].Count + Levels["Right"].Count;
            Members.Add(new SteelMember { Item = "Column", Profile = i.Column.Name, LengthMm = ColumnLengthMm, Count = 4, MassPerPieceKg = i.Column.MassKgPerM * ColumnLengthMm / 1000.0 });
            Members.Add(new SteelMember { Item = "Beam along the width", Profile = i.Beam.Name, LengthMm = i.WidthMm, Count = alongWidth, MassPerPieceKg = i.Beam.MassKgPerM * i.WidthMm / 1000.0 });
            Members.Add(new SteelMember { Item = "Beam along the depth", Profile = i.Beam.Name, LengthMm = i.DepthMm, Count = alongDepth, MassPerPieceKg = i.Beam.MassKgPerM * i.DepthMm / 1000.0 });
            var braces = Faces.Where(f => !f.IsEntrance).SelectMany(f => f.Braces).Select(b => b.LengthMm).ToList();
            foreach (var group in braces.GroupBy(l => Math.Round(l, 1)).OrderBy(g => g.Key))
            {
                // two diagonals in every panel
                Members.Add(new SteelMember
                {
                    Item = "Brace",
                    Profile = i.Brace.Name,
                    LengthMm = group.Key,
                    Count = 2 * group.Count(),
                    MassPerPieceKg = i.Brace.MassKgPerM * group.Key / 1000.0
                });
            }
            Members.Add(new SteelMember
            {
                Item = "Base plate",
                Profile = "PL " + N(i.PlateWidthMm) + "x" + N(i.PlateDepthMm) + "x" + N(i.PlateThicknessMm),
                LengthMm = i.PlateWidthMm,
                Count = 4,
                MassPerPieceKg = PlateKg(i.PlateWidthMm, i.PlateDepthMm, i.PlateThicknessMm)
            });
            int gussets = Faces.Where(f => !f.IsEntrance).Sum(f => f.Gussets.Count);
            Members.Add(new SteelMember
            {
                Item = "Gusset plate",
                Profile = "PL " + N(i.GussetLengthMm) + "x" + N(i.GussetWidthMm) + "x" + N(i.GussetThicknessMm),
                LengthMm = i.GussetLengthMm,
                Count = gussets,
                MassPerPieceKg = PlateKg(i.GussetLengthMm, i.GussetWidthMm, i.GussetThicknessMm)
            });
            Members.Add(new SteelMember { Item = "Anchor bolt", Profile = "M" + N(i.BoltDiameterMm), LengthMm = 0, Count = 4 * i.BoltCount, MassPerPieceKg = 0 });
        }

        static string N(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
