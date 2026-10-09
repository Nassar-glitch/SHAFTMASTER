using System.Linq;
using Elevator.Core.Geometry;

namespace Elevator.Core.Shaft
{
    /// <summary>Adds the building floors to the vertical section geometry.</summary>
    public static class BuildingVerticalGenerator
    {
        /// <summary>1000 mm is a drawing crop of the landing beyond the shaft, not building design width.</summary>
        public const double LandingDrawingExtent = 1000;

        /// <summary>Adds the building floors to the vertical section geometry.</summary>
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (var f in g.Floors.Where(f => !f.IsRoof))
            {
                g.Box(
                    "Floor." + f.FloorId + ".Slab",
                    "Concrete",
                    0,
                    f.FflZ - d.LandingSlabGraphicalThicknessMm,
                    LandingDrawingExtent,
                    f.FflZ);
                g.Line("Floor." + f.FloorId + ".Datum", "Datum", s.RoomRearX - 250, f.FflZ, LandingDrawingExtent, f.FflZ);
            }
            g.Line("Building.MRCeiling", "Structure", s.RoomRearX, g.CeilingZ, s.RoomFrontX, g.CeilingZ);
            g.Line("Building.MRRear", "Structure", s.RoomRearX, g.RoofZ, s.RoomRearX, g.CeilingZ);
            g.Line("Building.MRFront", "Structure", s.RoomFrontX, g.RoofZ, s.RoomFrontX, g.CeilingZ);
        }
    }
}
