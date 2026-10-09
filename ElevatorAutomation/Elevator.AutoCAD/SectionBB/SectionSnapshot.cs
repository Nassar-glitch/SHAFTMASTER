using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.SectionBB
{
    /// <summary>The B-B entities read from the drawing with their frame and manifest.</summary>
    public sealed class SectionSnapshot
    {
        public ShaftFrame Frame;
        public SectionManifest Manifest;
        public Dictionary<string, Entity> Entities;
    }
}
