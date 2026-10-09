using Elevator.Core.Geometry;

namespace Elevator.Core.GuideRails
{
    /// <summary>Adds the counterweight guide rails to the vertical section geometry.</summary>
    public static class CWGuideRailGenerator
    {
        /// <summary>Adds the counterweight guide rails to the vertical section geometry.</summary>
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            CarGuideRailGenerator.Rail(g, s.CwRailProjectedEdges, "CWRails");
        }
    }
}
