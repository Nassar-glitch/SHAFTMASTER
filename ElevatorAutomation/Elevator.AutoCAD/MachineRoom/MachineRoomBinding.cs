using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>A bound machine-room entity: key, role, source points and dependency.</summary>
    public sealed class MachineRoomBinding
    {
        public string Key;
        public string Type;
        public string Role;
        public bool OriginalVisible;
        public ChassisDependency Dependency;
        public double[] StructuralYOffsets;
        public bool[] StructuralRear;
    }
}
