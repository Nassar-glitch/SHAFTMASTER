using System.Linq;
using Elevator.Core.Geometry;

namespace Elevator.Core.Doors
{
    /// <summary>Adds the landings and landing doors to the vertical section geometry.</summary>
    public static class LandingGenerator
    {
        /// <summary>Adds the landings and landing doors to the vertical section geometry.</summary>
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            foreach (var f in g.Floors.Where(f => f.Served && !f.IsRoof))
            {
                string id = "Door." + f.FloorId;
                g.Box(id + ".Opening", "Door", s.LandingDoorBackX, f.FflZ, s.LandingDoorFrontX, f.FflZ + d.DoorOpeningHeightMm);
                g.Line(
                    id + ".ClearHead",
                    "Door",
                    s.LandingDoorBackX,
                    f.FflZ + d.DoorClearHeightMm,
                    s.LandingDoorFrontX,
                    f.FflZ + d.DoorClearHeightMm);
                g.Line(id + ".Sill", "Door", -s.Plan.CarFront, f.FflZ, s.FrontWallThicknessMm, f.FflZ);
                g.Line(
                    id + ".FrameTop",
                    "Door",
                    s.LandingDoorFrontX,
                    f.FflZ + d.DoorOpeningHeightMm,
                    s.FrontWallThicknessMm,
                    f.FflZ + d.DoorOpeningHeightMm);
            }
        }
    }
}
