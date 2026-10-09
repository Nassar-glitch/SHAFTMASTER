using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>Reviewed description of the machine-room master drawing.</summary>
    public sealed class MachineRoomSeed
    {
        public string SourceSha256;
        public double SW;
        public double SD;
        public List<MachineRoomSeedEntity> Entities;
    }
}
