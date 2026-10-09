using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.SectionBB
{
    /// <summary>The recorded entities and reference data of the B-B view.</summary>
    public sealed class SectionManifest
    {
        public int SchemaVersion = 1;
        public SectionReference Reference;
        public List<SectionBinding> Entities = new List<SectionBinding>();
    }
}
