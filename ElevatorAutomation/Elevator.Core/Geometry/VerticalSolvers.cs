using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
namespace Elevator.Core.Vertical
{
    public static class FloorStackGenerator
    {
        public static List<VerticalFloor> Create(
            int modeledLevels,
            string convention,
            string first,
            double[] heights,
            List<VerticalFloor> previous)
        {
            if (modeledLevels < 3 || modeledLevels > 100)
                throw new ArgumentException("Use 3..100 modeled levels including R (at least two normal levels).");
            if (heights == null || heights.Length != modeledLevels - 2 || heights.Any(h => !Finite(h) || h <= 0))
                throw new ArgumentException("One positive interval is required between each pair of normal floors; R is derived from OH.");
            int start = 0;
            if (convention == "Numeric")
            {
                if (!Int32.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
                    throw new ArgumentException("Numeric first level must be an integer.");
            }
            else if (convention == "BG")
            {
                if (first == "B")
                    start = -1;
                else if (first == "G")
                    start = 0;
                else if (!Int32.TryParse(first, out start) || start < 1)
                    throw new ArgumentException("B/G naming starts at B, G, or a positive integer.");
            }
            else
                throw new ArgumentException("Unsupported naming convention.");
            var result = new List<VerticalFloor>();
            double z = 0;
            for (int i = 0; i < modeledLevels; i++)
            {
                bool roof = i == modeledLevels - 1;
                int n = start + i;
                string name = roof ? "R" : convention == "BG"
                    ? (n == -1 ? "B" : n == 0 ? "G" : n.ToString(CultureInfo.InvariantCulture))
                    : n.ToString(CultureInfo.InvariantCulture);
                // Preserve IDs for retained normal levels and the roof across edits.
                var old = previous == null ? null : previous.SingleOrDefault(f => f.Name == name && f.IsRoof == roof);
                if (previous != null && previous.Count == modeledLevels && previous[i].IsRoof == roof)
                    old = previous[i];
                result.Add(new VerticalFloor
                {
                    FloorId = old == null ? Guid.NewGuid().ToString("N") : old.FloorId,
                    Name = name,
                    IsRoof = roof,
                    Served = !roof && (old == null || old.Served),
                    FflZ = z
                });
                if (i < heights.Length)
                    z += heights[i];
            }
            return result;
        }
        public static bool Finite(double x)
        {
            return !Double.IsNaN(x) && !Double.IsInfinity(x);
        }
    }
    public static class SuspensionTopology
    {
        public static double CounterweightBottom(double pitZ, double calibration, double highestServedZ, double carFloorZ)
        {
            return pitZ + calibration + highestServedZ - carFloorZ;
        }
    }
    public static class BracketLayoutSolver
    {
        public static BracketLayout Solve(
            double bottom,
            double top,
            double maximum,
            IEnumerable<VerticalFloor> floors,
            double slabThickness,
            double openingHeight)
        {
            if (!FloorStackGenerator.Finite(maximum) || maximum <= 0)
                throw new ArgumentException("Bracket maximum spacing must be explicitly selected and positive.");
            if (top <= bottom)
                throw new ArgumentException("Invalid rail run.");
            if ((top - bottom) / maximum > 100000)
                throw new ArgumentException("Requested bracket density exceeds the V1 drawing resource limit (100000 intervals).");
            var r = new BracketLayout();
            var anchors = new SortedSet<double> { bottom, top };
            foreach (var f in floors.Where(f => !f.IsRoof))
            {
                // Known floor/slab boundaries are anchors; no unproven structural capacity implied.
                if (f.FflZ - slabThickness > bottom && f.FflZ - slabThickness < top)
                    anchors.Add(f.FflZ - slabThickness);
                if (f.FflZ > bottom && f.FflZ < top)
                    anchors.Add(f.FflZ);
                if (f.Served)
                    r.Observations.Add(f.FloorId
                        + ": landing opening "
                        + f.FflZ.ToString("R", CultureInfo.InvariantCulture)
                        + ".."
                        + (f.FflZ + openingHeight).ToString("R", CultureInfo.InvariantCulture)
                        + "; brackets are on Plan side-guide supports, not the entrance wall. Fixing capacity/exclusions unverified.");
            }
            double[] a = anchors.ToArray();
            r.Elevations.Add(a[0]);
            for (int i = 1; i < a.Length; i++)
            {
                int n = (int)Math.Ceiling((a[i] - a[i - 1]) / maximum);
                for (int j = 1; j <= n; j++)
                    r.Elevations.Add(a[i - 1] + (a[i] - a[i - 1]) * j / n);
            }
            for (int i = 1; i < r.Elevations.Count; i++)
            {
                double gap = r.Elevations[i] - r.Elevations[i - 1];
                r.ConsecutiveSpacings.Add(gap);
                if (gap > maximum + 1e-7)
                    r.SpacingStatus = "FAIL";
            }
            r.Observations.Add("Endpoint markers are provisional bracket positions; rail-end/anchor/joint suitability NOT_YET_EVALUABLE.");
            return r;
        }
    }
    public static class VerticalSectionCalculator
    {
        public static VerticalGeometry Calculate(VerticalProjectData d, VerticalSource s)
        {
            if (d == null || d.SchemaVersion != 1 || s == null || s.Plan == null || s.Section == null)
                throw new ArgumentException("Supported vertical data and coordinated Plan/B-B results required.");
            if (d.SuspensionTopology != "1:1")
                throw new ArgumentException("Only confirmed 1:1 topology is implemented.");
            double[] positive = { d.PitDepthMm,
                d.OverheadMm,
                d.MachineRoomClearHeightMm,
                d.CarInternalHeightMm,
                d.CarOverallHeightMm,
                d.DoorClearHeightMm,
                d.DoorOpeningHeightMm,
                d.LandingSlabGraphicalThicknessMm,
                d.CounterweightOverallHeightMm,
                s.Section.Reference.SourceSlabThickness };
            if (positive.Any(v => !FloorStackGenerator.Finite(v) || v <= 0)
                || !FloorStackGenerator.Finite(d.LevelingOffsetMm)
                || !FloorStackGenerator.Finite(d.CarEnvelopeBottomBelowCarFFLMm)
                || d.CarEnvelopeBottomBelowCarFFLMm < 0
                || d.CarOverallHeightMm - d.CarEnvelopeBottomBelowCarFFLMm < d.CarInternalHeightMm
                || d.DoorOpeningHeightMm < d.DoorClearHeightMm)
                throw new ArgumentException("Invalid vertical envelope/datum values.");
            var floors = d.Floors;
            if (floors == null
                || floors.Count < 3
                || floors.Any(
                    f => String.IsNullOrWhiteSpace(f.FloorId) || String.IsNullOrWhiteSpace(f.Name) || !FloorStackGenerator.Finite(f.FflZ))
                || floors.Select(f => f.FloorId).Distinct().Count() != floors.Count
                || floors.Select(f => f.Name).Distinct().Count() != floors.Count
                || floors.Count(f => f.IsRoof) != 1
                || !floors.Last().IsRoof
                || floors.Last().Served
                || Math.Abs(floors[0].FflZ) > 1e-8)
                throw new ArgumentException("Invalid floor stack/identities; R is a distinct unserved structural level.");
            var normal = floors.Where(f => !f.IsRoof).ToArray();
            var served = normal.Where(f => f.Served).ToArray();
            if (served.Length < 2)
                throw new ArgumentException("At least two served landings required.");
            for (int i = 1; i < normal.Length; i++)
                if (normal[i].FflZ <= normal[i - 1].FflZ)
                    throw new ArgumentException("Normal FFL elevations must increase.");
            var car = normal.SingleOrDefault(f => f.FloorId == d.CarReferenceFloorId);
            if (car == null || !car.Served)
                throw new ArgumentException("Select a served car reference landing.");
            var g = new VerticalGeometry
            {
                Floors = floors.Select(f =>
                new VerticalFloor { FloorId = f.FloorId, Name = f.Name, Served = f.Served, IsRoof = f.IsRoof, FflZ = f.FflZ }).ToList()
            };
            g.PitFloorZ = -d.PitDepthMm;
            g.HighestServedZ = served.Last().FflZ;
            g.LowestServedZ = served.First().FflZ;
            g.TravelMm = g.HighestServedZ - g.LowestServedZ;
            g.MRSlabUndersideZ = g.HighestServedZ + d.OverheadMm;
            g.RoofZ = g.MRSlabUndersideZ + s.Section.Reference.SourceSlabThickness;
            g.CeilingZ = g.RoofZ + d.MachineRoomClearHeightMm;
            g.Floors.Last().FflZ = g.RoofZ;
            if (normal.Last().FflZ >= g.MRSlabUndersideZ)
                throw new ArgumentException("A modeled NO STOP level intersects/exceeds the shaft ceiling; adjust OH or floor stack.");
            g.CarFloorZ = car.FflZ + d.LevelingOffsetMm;
            g.CarBottomZ = g.CarFloorZ - d.CarEnvelopeBottomBelowCarFFLMm;
            g.CarTopZ = g.CarBottomZ + d.CarOverallHeightMm;
            double calibration = d.CWBottomAbovePitFloorAtHighestServedLandingMm ?? d.ProvisionalCWCalibrationMm;
            if (!FloorStackGenerator.Finite(calibration) || calibration < 0)
                throw new ArgumentException("CW reference must be a finite nonnegative height above finished pit floor.");
            g.CwBottomZ = SuspensionTopology.CounterweightBottom(g.PitFloorZ, calibration, g.HighestServedZ, g.CarFloorZ);
            g.CwTopZ = g.CwBottomZ + d.CounterweightOverallHeightMm;
            g.CWAbsolutePositionStatus = d.CWAbsolutePositionStatus;
            g.CodeDiameterRatioStatus = s.CodeDiameterRatioStatus;
            if (g.CarBottomZ < g.PitFloorZ || g.CarTopZ > g.MRSlabUndersideZ || g.CwBottomZ < g.PitFloorZ || g.CwTopZ > g.MRSlabUndersideZ)
            {
                g.LayoutStatus = "WARNING";
                g.ReviewItems.Add("Displayed car/CW envelope intersects shaft vertical limits.");
            }
            for (int i = 0; i < normal.Length; i++)
                if (normal[i].Served && normal[i].FflZ + d.DoorOpeningHeightMm
                    > (i + 1 < normal.Length ? normal[i + 1].FflZ - d.LandingSlabGraphicalThicknessMm : g.MRSlabUndersideZ))
                {
                    g.LayoutStatus = "WARNING";
                    g.ReviewItems.Add(normal[i].Name + ": opening intersects next slab/ceiling.");
                }
            g.Brackets = BracketLayoutSolver.Solve(
                g.PitFloorZ,
                g.MRSlabUndersideZ,
                d.RequestedBracketMaxSpacingMm,
                g.Floors,
                d.LandingSlabGraphicalThicknessMm,
                d.DoorOpeningHeightMm);
            g.ReviewItems.Add("CW absolute position: "
                + g.CWAbsolutePositionStatus
                + "; provisional calibration, when missing = "
                + d.ProvisionalCWCalibrationMm
                + " mm (test drawing only).");
            g.ReviewItems.Add("Car envelope offset: "
                + d.CarEnvelopeBottomBelowCarFFLMm
                + " mm; "
                + d.CarEnvelopeOffsetSource
                + "; not member thicknesses.");
            g.ReviewItems.Add(
                "Bracket fixing / rail ends / pit and overhead code clearances: NOT_YET_EVALUABLE. Rail joints: NOT_YET_IMPLEMENTED.");
            g.ReviewItems.Add(
                "Car frame/shoes/safety gear, CW internals, rope terminations, buffers/governor vertical hardware: "
                + "DETAIL_DEVELOPMENT_REQUIRED.");
            g.ReviewItems.Add(
                "Suspension ropes shown as the coordinated projected rope path; individual groove offsets and terminations not supplied.");
            g.ReviewItems.Add(
                "D/d: " + s.CodeDiameterRatioStatus + ". Hanger MECHANICAL REVIEW REQUIRED; steel preliminary; concrete NOT EVALUATED.");
            g.ReviewItems.Add("Coordinated upper reeving: "
                + s.Section.ReevingGeometryStatus
                + "; wrap "
                + (s.Section.Arrangement.CalculatedWrapAngleDeg.HasValue
                ? s.Section.Arrangement.CalculatedWrapAngleDeg.Value.ToString("0.###", CultureInfo.InvariantCulture) + " deg"
                : "unknown") + " / target >=152 deg. Slab clearance: " + s.Section.Arrangement.DeflectorSlabClearanceStatus + ".");
            BuildingVerticalGenerator.Generate(g, d, s);
            ShaftVerticalGenerator.Generate(g, d, s);
            LandingGenerator.Generate(g, d, s);
            CarVerticalGenerator.Generate(g, d, s);
            CounterweightVerticalGenerator.Generate(g, d, s);
            CarGuideRailGenerator.Generate(g, d, s);
            CWGuideRailGenerator.Generate(g, d, s);
            GuideRailBracketGenerator.Generate(g, d, s);
            SuspensionRopeVerticalGenerator.Generate(g, d, s);
            PitGenerator.Generate(g, d, s);
            MachineRoomVerticalGenerator.Generate(g, d, s);
            VerticalDimensionGenerator.Generate(g, d, s);
            AnnotationLayoutEngine.Generate(g, d, s);
            if (g.Entities.Select(e => e.Id).Distinct().Count() != g.Entities.Count
                || g.Entities.Any(e => e.Points.Any(v => !FloorStackGenerator.Finite(v))))
                throw new InvalidOperationException("Non-deterministic/invalid generated geometry.");
            return g;
        }
    }
}
