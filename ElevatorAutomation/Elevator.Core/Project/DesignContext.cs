using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;

namespace Elevator.Core.Project
{
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
    }
}
