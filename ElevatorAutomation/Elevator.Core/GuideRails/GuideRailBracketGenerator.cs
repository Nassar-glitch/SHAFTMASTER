using System.Linq;
using Elevator.Core.Geometry;

namespace Elevator.Core.GuideRails
{
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
}
