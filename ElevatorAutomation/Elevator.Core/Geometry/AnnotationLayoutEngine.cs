using System;
using System.Globalization;

namespace Elevator.Core.Geometry
{
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
