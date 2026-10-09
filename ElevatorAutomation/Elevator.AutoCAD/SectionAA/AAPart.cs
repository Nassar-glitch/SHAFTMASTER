using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.SectionAA
{
    /// <summary>One entity of the A-A master.</summary>
    public sealed class AAPart
    {
        public string Handle;
        public string Role;
        public string Key;
        public double[] Bounds;
    }
}
