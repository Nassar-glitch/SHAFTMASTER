using Elevator.Core.Geometry;

namespace Elevator.Core.GuideRails
{
    public static class CWGuideRailGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            CarGuideRailGenerator.Rail(g, s.CwRailProjectedEdges, "CWRails");
        }
    }
}
