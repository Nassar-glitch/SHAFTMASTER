using System;
using System.Linq;
using System.Globalization;
namespace Elevator.Core.Vertical
{
    public static class BuildingVerticalGenerator
    {
        // 1000 mm is a drawing crop of the landing beyond the shaft, not building design width.
        public const double LandingDrawingExtent = 1000;
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (var f in g.Floors.Where(f => !f.IsRoof))
            {
                g.Box(
                    "Floor." + f.FloorId + ".Slab",
                    "Concrete",
                    0,
                    f.FflZ - d.LandingSlabGraphicalThicknessMm,
                    LandingDrawingExtent,
                    f.FflZ);
                g.Line("Floor." + f.FloorId + ".Datum", "Datum", s.RoomRearX - 250, f.FflZ, LandingDrawingExtent, f.FflZ);
            }
            g.Line("Building.MRCeiling", "Structure", s.RoomRearX, g.CeilingZ, s.RoomFrontX, g.CeilingZ);
            g.Line("Building.MRRear", "Structure", s.RoomRearX, g.RoofZ, s.RoomRearX, g.CeilingZ);
            g.Line("Building.MRFront", "Structure", s.RoomFrontX, g.RoofZ, s.RoomFrontX, g.CeilingZ);
        }
    }
    public static class ShaftVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Box("Shaft.RearWall", "Concrete", -s.Plan.SD - s.RearWallThicknessMm, g.PitFloorZ, -s.Plan.SD, g.MRSlabUndersideZ);
            double z = g.PitFloorZ;
            int n = 0;
            foreach (var f in g.Floors.Where(f => !f.IsRoof && f.Served))
            {
                if (f.FflZ > z)
                    g.Box("Shaft.FrontPier." + (n++), "Concrete", 0, z, s.FrontWallThicknessMm, f.FflZ);
                z = Math.Max(z, f.FflZ + d.DoorOpeningHeightMm);
            }
            if (z < g.MRSlabUndersideZ)
                g.Box("Shaft.FrontPier." + (n++), "Concrete", 0, z, s.FrontWallThicknessMm, g.MRSlabUndersideZ);
        }
    }
    public static class LandingGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (var f in g.Floors.Where(f => f.Served && !f.IsRoof))
            {
                string id = "Door." + f.FloorId;
                g.Box(id + ".Opening", "Door", s.LandingDoorBackX, f.FflZ, s.LandingDoorFrontX, f.FflZ + d.DoorOpeningHeightMm);
                g.Line(
                    id + ".ClearHead",
                    "Door",
                    s.LandingDoorBackX,
                    f.FflZ + d.DoorClearHeightMm,
                    s.LandingDoorFrontX,
                    f.FflZ + d.DoorClearHeightMm);
                g.Line(id + ".Sill", "Door", -s.Plan.CarFront, f.FflZ, s.FrontWallThicknessMm, f.FflZ);
                g.Line(
                    id + ".FrameTop",
                    "Door",
                    s.LandingDoorFrontX,
                    f.FflZ + d.DoorOpeningHeightMm,
                    s.FrontWallThicknessMm,
                    f.FflZ + d.DoorOpeningHeightMm);
            }
        }
    }
    public static class CarVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            double rear = -s.Plan.CarRear, front = -s.Plan.CarFront;
            g.Box("Car.Envelope", "Car", rear, g.CarBottomZ, front, g.CarTopZ);
            g.Line("Car.Floor", "Car", rear, g.CarFloorZ, front, g.CarFloorZ);
            g.Line("Car.InternalCeiling", "Car", rear, g.CarFloorZ + d.CarInternalHeightMm, front, g.CarFloorZ + d.CarInternalHeightMm);
            g.Box("Car.RearShell", "Envelope", s.CarRearOuterX, g.CarFloorZ, rear, g.CarFloorZ + d.CarInternalHeightMm);
            g.Box("Car.Door", "Door", s.CarDoorBackX, g.CarFloorZ, s.CarDoorFrontX, g.CarFloorZ + d.DoorOpeningHeightMm);
            g.Line(
                "Car.DoorClearHead",
                "Door",
                s.CarDoorBackX,
                g.CarFloorZ + d.DoorClearHeightMm,
                s.CarDoorFrontX,
                g.CarFloorZ + d.DoorClearHeightMm);
            g.Line("Car.GuideReference", "Datum", -s.Plan.CarRow, g.CarBottomZ, -s.Plan.CarRow, g.CarTopZ);
            // Section hatching of the envelope bands communicates an assembly, without defining member thicknesses.
            g.Box("Car.EnvelopeLowerBand", "Envelope", rear, g.CarBottomZ, front, g.CarFloorZ);
            g.Box("Car.EnvelopeUpperBand", "Envelope", rear, g.CarFloorZ + d.CarInternalHeightMm, front, g.CarTopZ);
            g.Note(
                "Car.Label",
                "CAR\\P1:1\\PFFL " + g.CarFloorZ.ToString("0.###", CultureInfo.InvariantCulture),
                -s.Plan.CarRow + 100,
                g.CarFloorZ + 1250);
            g.Note("Car.DetailStatus", "CAR FRAME / SHOES / SAFETY GEAR\\PDETAIL DEVELOPMENT REQUIRED", 2300, g.CarFloorZ + 1500);
        }
    }
    public static class CounterweightVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Box("CW.Envelope", "CW", -s.Plan.CwRear, g.CwBottomZ, -s.Plan.CwFront, g.CwTopZ);
            // Diagonal envelope symbol: no invented frame members or weight-stack count.
            g.Line("CW.EnvelopeSymbolA", "CW", -s.Plan.CwRear, g.CwBottomZ, -s.Plan.CwFront, g.CwTopZ);
            g.Line("CW.EnvelopeSymbolB", "CW", -s.Plan.CwFront, g.CwBottomZ, -s.Plan.CwRear, g.CwTopZ);
            g.Note("CW.Label", "COUNTERWEIGHT\\P" + (g.CWAbsolutePositionStatus == "PROVISIONAL_REFERENCE_INPUT_REQUIRED"
                ? "PROVISIONAL REFERENCE\\PINPUT REQUIRED"
                : "PROJECT REFERENCE\\PSUPPLIED"), s.RoomRearX - 1450, g.CwBottomZ + d.CounterweightOverallHeightMm / 2);
            g.Line(
                "CW.Leader",
                "Annotation",
                s.RoomRearX - 250,
                g.CwBottomZ + d.CounterweightOverallHeightMm / 2,
                -s.Plan.CwRear,
                g.CwBottomZ + d.CounterweightOverallHeightMm / 2);
        }
    }
    public static class CarGuideRailGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            Rail(g, s.CarRailProjectedEdges, "CarRails");
        }
        internal static void Rail(VerticalGeometry g, double[] edges, string name)
        {
            foreach (string physical in new[] { "Left", "Right" })
                for (int i = 0; i < edges.Length; i++)
                    g.Line(name + "." + physical + ".Edge" + i, "Rail", edges[i], g.PitFloorZ, edges[i], g.MRSlabUndersideZ);
        }
    }
    public static class CWGuideRailGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            CarGuideRailGenerator.Rail(g, s.CwRailProjectedEdges, "CWRails");
        }
    }
    public static class GuideRailBracketGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (string family in new[] { "Car", "CW" })
            {
                double[] a = family == "Car" ? s.CarBracketProjectedEdges : s.CwBracketProjectedEdges;
                for (int i = 0; i < g.Brackets.Elevations.Count; i++)
                    foreach (string physical in new[] { "Left", "Right" })
                    {
                        // A transverse support line at each exact elevation; no fabricated vertical plate/bolt dimensions.
                        double z = g.Brackets.Elevations[i];
                        g.Line("Bracket." + family + "." + physical + "." + i, "Bracket", a.Min(), z, a.Max(), z);
                    }
            }
        }
    }
    public static class SuspensionRopeVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            if (!s.Section.HasCalculatedPath)
            {
                g.ReviewItems.Add("Upper coordinated rope path is not evaluable.");
                return;
            }
            // Drops join the cloned B-B sheave paths at exact center-line tangencies.
            g.Line("Rope.CarDrop", "Rope", s.Section.CarDropX, g.CarTopZ, s.Section.CarDropX, g.RoofZ + s.Section.TractionZ);
            g.Line("Rope.CWDrop", "Rope", s.Section.CwDropX, g.CwTopZ, s.Section.CwDropX, g.RoofZ + s.Section.DeflectorZ);
        }
    }
    public static class PitGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Line("Pit.FinishedFloor", "Structure", -s.Plan.SD, g.PitFloorZ, 0, g.PitFloorZ);
            if (s.GovernorProjectedAxisX.HasValue)
                g.Line(
                    "Governor.ProjectedAxisOnly",
                    "Datum",
                    s.GovernorProjectedAxisX.Value,
                    g.PitFloorZ,
                    s.GovernorProjectedAxisX.Value,
                    g.MRSlabUndersideZ);
            g.Note(
                "Pit.Review",
                "PIT EQUIPMENT / BUFFERS / GOVERNOR TENSION DEVICE\\PVERTICAL COMPONENT DATA REQUIRED",
                2300,
                g.PitFloorZ + 500);
        }
    }
    public static class MachineRoomVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Note(
                "MR.Title",
                "MACHINE ROOM  |  COORDINATED B-B\\PEquipment translated by R; original geometry retained",
                s.RoomRearX,
                g.CeilingZ + 300);
        }
    }
    public static class VerticalDimensionGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            var f = g.Floors;
            double x = BuildingVerticalGenerator.LandingDrawingExtent + 350;
            for (int i = 1; i < f.Count; i++)
                g.Add(
                    "Dimension.Floor." + f[i - 1].FloorId + "." + f[i].FloorId,
                    "Dimension",
                    "Dimension",
                    "FFL_DERIVED",
                    null,
                    1000,
                    f[i - 1].FflZ,
                    1000,
                    f[i].FflZ,
                    x);
            g.Add("Dimension.Pit", "Dimension", "Dimension", "PROJECT_DATUM", "PIT <>", 0, g.PitFloorZ, 0, 0, x);
            g.Add(
                "Dimension.Travel",
                "Dimension",
                "Dimension",
                "SERVED_FFL_DERIVED",
                "TRAVEL <>",
                1000,
                g.LowestServedZ,
                1000,
                g.HighestServedZ,
                x + 350);
            g.Add("Dimension.OH", "Dimension", "Dimension", "PROJECT_DATUM", "OH <>", 0, g.HighestServedZ, 0, g.MRSlabUndersideZ, x + 700);
        }
    }
    public static class AnnotationLayoutEngine
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (var f in g.Floors)
            {
                string label = f.Name
                    + "  |  FFL "
                    + f.FflZ.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)
                    + " mm"
                    + (f.IsRoof ? "  |  MR STRUCTURAL LEVEL" : !f.Served ? "  |  NO STOP" : "");
                g.Note("Floor." + f.FloorId + ".Label", label, 320, f.FflZ + 125);
            }
            g.Note(
                "Title",
                "EXPERIMENTAL FULL VERTICAL SECTION\\PCAR RIGHT / CW LEFT  |  TRUE HEIGHT  |  ALL VALUES mm",
                s.RoomRearX - 1450,
                g.CeilingZ + 900);
            g.Note("ReviewSummary", String.Join("\\P", g.ReviewItems.ToArray()), 2300, g.CeilingZ);
            g.Note("Rails.Label", "CAR T70/65/9  |  CW T50/50/5\\PMAX BRACKET SPACING "
                + d.RequestedBracketMaxSpacingMm.ToString("0.###", CultureInfo.InvariantCulture)
                + " mm\\PPROJECT INPUT - STRUCTURAL/FIXING REVIEW REQUIRED\\PCoincident rails retain separate physical IDs",
                    2300,
                    g.CeilingZ - 1900);
            g.Note("Provenance", "V1 GRAPHICAL SLABS 250; FFL = SLAB TOP\\PPIT BASE / MR ROOF STRUCTURAL THICKNESS NOT DEFINED\\P"
                + (d.FixtureLabel ?? "EXPERIMENTAL PROJECT - REVIEW BEFORE USE"), 2300, g.CeilingZ - 2800);
            g.Note(
                "Governor.ReferenceNote",
                "GOVERNOR: PROJECTED PLAN AXIS ONLY\\PMOUNTING / TENSION DEVICE ELEVATIONS NOT SUPPLIED\\PNO VERTICAL HARDWARE OR BUFFER "
                + "VALIDATION",
                2300,
                g.CeilingZ - 3800);
        }
    }
}



