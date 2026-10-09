using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Car;
using Elevator.Core.Counterweight;
using Elevator.Core.Doors;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;
using Elevator.Core.Shaft;
using Elevator.Core.Suspension;

namespace Elevator.Core.Geometry
{
    /// <summary>Calculates the vertical section from the vertical project data and the coordinated plan and B-B results.</summary>
    public static class VerticalSectionCalculator
    {
        /// <summary>Calculates the vertical section geometry.</summary>
        public static VerticalGeometry Calculate(VerticalProjectData d, VerticalSource s)
        {
            ValidateData(d, s);
            var floors = d.Floors;
            ValidateFloorStack(floors);
            var normal = floors.Where(f => !f.IsRoof).ToArray();
            var served = normal.Where(f => f.Served).ToArray();
            ValidateServedLandings(normal, served);
            var car = normal.SingleOrDefault(f => f.FloorId == d.CarReferenceFloorId);
            if (car == null || !car.Served)
                throw new ArgumentException("Select a served car reference landing.");
            VerticalGeometry g = BuildStack(d, s, floors, normal, served, car);
            PlaceCounterweight(d, s, g);
            CheckEnvelopeFits(d, normal, g);
            g.Brackets = BracketLayoutSolver.Solve(
                g.PitFloorZ,
                g.MRSlabUndersideZ,
                d.RequestedBracketMaxSpacingMm,
                g.Floors,
                d.LandingSlabGraphicalThicknessMm,
                d.DoorOpeningHeightMm);
            AddReviewItems(d, s, g);
            RunGenerators(d, s, g);
            if (g.Entities.Select(e => e.Id).Distinct().Count() != g.Entities.Count
                || g.Entities.Any(e => e.Points.Any(v => !FloorStackGenerator.Finite(v))))
                throw new InvalidOperationException("Non-deterministic/invalid generated geometry.");
            return g;
        }

        private static void ValidateData(VerticalProjectData d, VerticalSource s)
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
        }

        private static void ValidateFloorStack(List<VerticalFloor> floors)
        {
            if (floors == null
                            || floors.Count < 3
                            || floors.Any(
                                f => String.IsNullOrWhiteSpace(f.FloorId)
                                    || String.IsNullOrWhiteSpace(f.Name)
                                    || !FloorStackGenerator.Finite(f.FflZ))
                            || floors.Select(f => f.FloorId).Distinct().Count() != floors.Count
                            || floors.Select(f => f.Name).Distinct().Count() != floors.Count
                            || floors.Count(f => f.IsRoof) != 1
                            || !floors.Last().IsRoof
                            || floors.Last().Served
                            || Math.Abs(floors[0].FflZ) > 1e-8)
                throw new ArgumentException("Invalid floor stack/identities; R is a distinct unserved structural level.");
        }

        private static void ValidateServedLandings(VerticalFloor[] normal, VerticalFloor[] served)
        {
            if (served.Length < 2)
                throw new ArgumentException("At least two served landings required.");
            for (int i = 1; i < normal.Length; i++)
            {
                if (normal[i].FflZ <= normal[i - 1].FflZ)
                    throw new ArgumentException("Normal FFL elevations must increase.");
            }
        }

        private static VerticalGeometry BuildStack(
            VerticalProjectData d,
            VerticalSource s,
            List<VerticalFloor> floors,
            VerticalFloor[] normal,
            VerticalFloor[] served,
            VerticalFloor car)
        {
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
            return g;
        }

        private static void PlaceCounterweight(VerticalProjectData d, VerticalSource s, VerticalGeometry g)
        {
            double calibration = d.CWBottomAbovePitFloorAtHighestServedLandingMm ?? d.ProvisionalCWCalibrationMm;
            if (!FloorStackGenerator.Finite(calibration) || calibration < 0)
                throw new ArgumentException("CW reference must be a finite nonnegative height above finished pit floor.");
            g.CwBottomZ = SuspensionTopology.CounterweightBottom(g.PitFloorZ, calibration, g.HighestServedZ, g.CarFloorZ);
            g.CwTopZ = g.CwBottomZ + d.CounterweightOverallHeightMm;
            g.CWAbsolutePositionStatus = d.CWAbsolutePositionStatus;
            g.CodeDiameterRatioStatus = s.CodeDiameterRatioStatus;
        }

        private static void CheckEnvelopeFits(VerticalProjectData d, VerticalFloor[] normal, VerticalGeometry g)
        {
            if (g.CarBottomZ < g.PitFloorZ || g.CarTopZ > g.MRSlabUndersideZ || g.CwBottomZ < g.PitFloorZ || g.CwTopZ > g.MRSlabUndersideZ)
            {
                g.LayoutStatus = "WARNING";
                g.ReviewItems.Add("Displayed car/CW envelope intersects shaft vertical limits.");
            }
            for (int i = 0; i < normal.Length; i++)
            {
                if (normal[i].Served && normal[i].FflZ + d.DoorOpeningHeightMm
                                    > (i + 1 < normal.Length ? normal[i + 1].FflZ - d.LandingSlabGraphicalThicknessMm : g.MRSlabUndersideZ))
                {
                    g.LayoutStatus = "WARNING";
                    g.ReviewItems.Add(normal[i].Name + ": opening intersects next slab/ceiling.");
                }
            }
        }

        private static void AddReviewItems(VerticalProjectData d, VerticalSource s, VerticalGeometry g)
        {
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
        }

        private static void RunGenerators(VerticalProjectData d, VerticalSource s, VerticalGeometry g)
        {
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
        }
    }
}
