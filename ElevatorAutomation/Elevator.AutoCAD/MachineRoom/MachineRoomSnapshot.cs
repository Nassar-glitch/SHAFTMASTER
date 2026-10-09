using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Plan;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>The machine-room entities read from the drawing with their frame and manifest.</summary>
    public sealed class MachineRoomSnapshot
    {
        public ShaftFrame Frame;
        public MachineRoomManifest Manifest;
        public Dictionary<string, Entity> Entities;
        public double SW;
        public double SD;
        public double CarRow;
        public double CwRow;
        public double PitchProjection;
        public double GovernorOffset;
        public double GovernorStartOffset;
        public MachineRoomExtensions Extensions;

        /// <summary>An entity of the snapshot by key, as a type.</summary>
        public T Get<T>(string id) where T : Entity
        {
            return (T)Entities[id];
        }
    }
}
