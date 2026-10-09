using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD
{
    // Read existing semantic bindings only. No new tags, entity handles or template mutation.
    public static class ChassisCalculationGeometryReader
    {
        public static ChassisGeometryEvidence Read(MachineRoomSnapshot s)
        {
            var result = new ChassisGeometryEvidence
            {
                SW = s.SW,
                SD = s.SD,
                CarRow = s.CarRow,
                CwRow = s.CwRow,
                Pitch = s.PitchProjection,
                Extensions = s.Extensions,
                GovernorStartOffset = s.GovernorStartOffset
            };
            foreach (var b in s.Manifest.Entities.Where(b => b.Key.StartsWith("MachineRoom.Support.")
                || b.Key.StartsWith("MachineRoom.Chassis.")
                || b.Key.StartsWith("MachineRoom.MachineChassis.")
                || b.Key.StartsWith("MachineRoom.TractionSheave.")
                || b.Key.StartsWith("MachineRoom.DeflectorSheave.")))
            {
                var e = s.Entities[b.Key];
                var poly = e as Polyline;
                if (!(e is Line) && poly == null)
                    continue; // Never use generic extents/curve samples as contact edges.
                bool straight = poly == null || Enumerable.Range(0, poly.NumberOfVertices).All(i => Math.Abs(poly.GetBulgeAt(i)) < 1e-9);
                result.Contours.Add(new ChassisContour
                {
                    Id = b.Key,
                    Role = b.Role,
                    Straight = straight,
                    Points = MachineRoomDependencies.Points(e, s.Frame),
                    Dependency = b.Dependency
                });
            }
            if (s.Manifest.DependencyVersion != 1)
            {
                result.Issues.Add(new CalculationIssue(
                                    "Geometry.Dependencies",
                                    "Reviewed drawing dependency recipes are required; no independent coordinate fallback."));
            }
            return result;
        }
    }
}
