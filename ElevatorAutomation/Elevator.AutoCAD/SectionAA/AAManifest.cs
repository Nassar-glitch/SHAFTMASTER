using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.SectionAA
{
    public sealed class AAManifest
    {
        public int SchemaVersion = 1;
        public List<string> Keys;
        public string InputJson;
        public double CarAttachmentZ = 2300;
        public double CWAttachmentZ = 3000;
    }
}
