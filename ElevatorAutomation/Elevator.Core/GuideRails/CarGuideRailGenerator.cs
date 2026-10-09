using Elevator.Core.Geometry;

namespace Elevator.Core.GuideRails
{
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
}
