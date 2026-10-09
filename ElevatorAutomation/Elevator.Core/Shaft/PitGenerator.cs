using Elevator.Core.Geometry;

namespace Elevator.Core.Shaft
{
    public static class PitGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Line("Pit.FinishedFloor", "Structure", -s.Plan.SD, g.PitFloorZ, 0, g.PitFloorZ);
            if (s.GovernorProjectedAxisX.HasValue)
            {
                g.Line(
                                    "Governor.ProjectedAxisOnly",
                                    "Datum",
                                    s.GovernorProjectedAxisX.Value,
                                    g.PitFloorZ,
                                    s.GovernorProjectedAxisX.Value,
                                    g.MRSlabUndersideZ);
            }
            g.Note(
                "Pit.Review",
                "PIT EQUIPMENT / BUFFERS / GOVERNOR TENSION DEVICE\\PVERTICAL COMPONENT DATA REQUIRED",
                2300,
                g.PitFloorZ + 500);
        }
    }
}
