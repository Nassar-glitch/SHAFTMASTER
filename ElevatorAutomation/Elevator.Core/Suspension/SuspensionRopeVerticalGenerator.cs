using Elevator.Core.Geometry;

namespace Elevator.Core.Suspension
{
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
}
