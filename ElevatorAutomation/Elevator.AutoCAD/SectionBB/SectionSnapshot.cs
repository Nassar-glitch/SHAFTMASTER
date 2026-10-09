using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Plan;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.SectionBB
{
    public sealed class SectionSnapshot
    {
        public ShaftFrame Frame;
        public SectionManifest Manifest;
        public Dictionary<string, Entity> Entities;
    }
}
