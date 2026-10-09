using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    public sealed class MachineRoomSeed
    {
        public string SourceSha256;
        public double SW;
        public double SD;
        public List<MachineRoomSeedEntity> Entities;
    }
}
