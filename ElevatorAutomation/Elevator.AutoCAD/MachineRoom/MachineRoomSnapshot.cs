using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Plan;
using Elevator.Core.MachineRoom;

namespace Elevator.AutoCAD.MachineRoom
{
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

        public T Get<T>(string id) where T : Entity
        {
            return (T)Entities[id];
        }
    }
}
