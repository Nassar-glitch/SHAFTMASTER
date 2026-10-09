using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.SectionBB
{
    public sealed class SectionManifest
    {
        public int SchemaVersion = 1;
        public SectionReference Reference;
        public List<SectionBinding> Entities = new List<SectionBinding>();
    }
}
