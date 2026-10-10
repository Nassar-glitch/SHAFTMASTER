using Elevator.Core.Doors;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;

namespace Elevator.Core.Project
{
    /// <summary>The reviewed context of a design: equipment profile, machine-room and B-B references and chassis evidence.</summary>
    public sealed class DesignContext
    {
        public EquipmentProfile Equipment;
        public double CounterweightDepthMm;
        public MachineRoomExtensions RoomExtensions;
        public SectionReference SectionReference;
        public double FrontSupportDepthMm;
        public double RearSupportOffsetMm;
        public double RetainedDeflectorPitchMm;
        public bool RetainedDeflectorVisible;
        public ChassisGeometryEvidence ChassisEvidence;

        /// <summary>The door catalog (preset library); null: the shipped default catalog.</summary>
        public DoorCatalog Doors;
    }
}
