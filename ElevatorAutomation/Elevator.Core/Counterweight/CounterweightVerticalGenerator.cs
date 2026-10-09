using Elevator.Core.Geometry;

namespace Elevator.Core.Counterweight
{
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
}
