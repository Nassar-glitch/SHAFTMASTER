using System;
using System.Linq;
using Elevator.Core.Geometry;

namespace Elevator.Core.Shaft
{
    public static class ShaftVerticalGenerator
    {
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Box("Shaft.RearWall", "Concrete", -s.Plan.SD - s.RearWallThicknessMm, g.PitFloorZ, -s.Plan.SD, g.MRSlabUndersideZ);
            double z = g.PitFloorZ;
            int n = 0;
            foreach (var f in g.Floors.Where(f => !f.IsRoof && f.Served))
            {
                if (f.FflZ > z)
                    g.Box("Shaft.FrontPier." + (n++), "Concrete", 0, z, s.FrontWallThicknessMm, f.FflZ);
                z = Math.Max(z, f.FflZ + d.DoorOpeningHeightMm);
            }
            if (z < g.MRSlabUndersideZ)
                g.Box("Shaft.FrontPier." + (n++), "Concrete", 0, z, s.FrontWallThicknessMm, g.MRSlabUndersideZ);
        }
    }
}
