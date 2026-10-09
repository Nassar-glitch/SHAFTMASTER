using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Map from legacy entity keys to current keys.</summary>
    public sealed class LegacyMap
    {
        public string SemanticId { get; set; }
        public string LegacyKey { get; set; }
        public string Handle { get; set; }
    }
}
