using Elevator.Core.Shaft;

namespace Elevator.Core.Geometry
{
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
}
