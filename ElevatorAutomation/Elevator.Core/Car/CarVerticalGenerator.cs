using System.Globalization;
using Elevator.Core.Geometry;

namespace Elevator.Core.Car
{
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
}
