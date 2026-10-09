using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.MachineRoom;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.SectionBB
{
    public sealed class SectionSeed
    {
        public string SourceSha256;
        public SectionReference Reference;
        public List<MachineRoomSeedEntity> Entities;
    }
}
