using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;

namespace Elevator.AutoCAD.SectionBB
{
    public sealed class SectionBinding
    {
        public string Key;
        public string Role;
        public string Type;
        public LocalPoint[] SourcePoints;
        public bool Visible;
        public double[] Bounds;
        public double? Radius;
    }
}
