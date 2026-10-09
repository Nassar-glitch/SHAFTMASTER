using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    public sealed class MachineRoomManifest
    {
        public int SchemaVersion = 1;
        public string TemplateId = "machine-room-reference-v1";
        public List<MachineRoomBinding> Entities = new List<MachineRoomBinding>();
        public int DependencyVersion;
    }
}
