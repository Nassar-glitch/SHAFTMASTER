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
    public sealed class AAPart
    {
        public string Handle;
        public string Role;
        public string Key;
        public double[] Bounds;
    }
}
