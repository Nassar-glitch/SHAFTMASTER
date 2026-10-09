using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>One entity of the reviewed machine-room master.</summary>
    public sealed class MachineRoomSeedEntity
    {
        public string Key;
        public string Type;
        public string Role;
        public string Prefix;
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
    }
}
