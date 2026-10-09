using System.Collections.Generic;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Machine-room results: governor offset, deflector, reeving and rules.</summary>
    public sealed class MachineRoomParameters
    {
        public MachineRoomExtensions Extensions;
        public double RoomWidth;
        public double RoomDepth;
        public DeflectorSheaveSelection Deflector;
        public GovernorOffsetSelection Governor;
        public ReevingGeometry Reeving;
        public List<EngineeringRule> Rules;

        // CAD validation sets PASS only after the current view has been checked.
        public string LayoutStatus = "NOT YET EVALUATED";
        public string CodeDiameterRatioStatus = "NOT YET EVALUABLE";

        public double[] AvailableDeflectorPitchDiametersMm
        {
            get
            {
                return new double[] { 250, 400 };
            }
        }
    }
}
