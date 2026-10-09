using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.SectionAA
{
    public sealed class AASeed
    {
        public string SourceSha256;
        public List<AAPart> Entities;
    }
}
