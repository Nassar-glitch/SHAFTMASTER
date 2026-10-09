using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>The recorded entities and reference data of the machine-room view.</summary>
    public sealed class MachineRoomManifest
    {
        public int SchemaVersion = 1;
        public string TemplateId = "machine-room-reference-v1";
        public List<MachineRoomBinding> Entities = new List<MachineRoomBinding>();
        public int DependencyVersion;
    }
}
