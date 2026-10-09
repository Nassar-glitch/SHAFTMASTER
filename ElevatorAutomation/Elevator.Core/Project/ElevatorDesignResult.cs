using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Project
{
    /// <summary>Result of the shared design: validation, plan, sections, chassis geometry and calculation.</summary>
    public sealed class ElevatorDesignResult
    {
        public ElevatorParameters Plan;
        public SectionAAGeometry SectionAA;
        public List<FloorElevation> Floors;
        public List<ValidationIssue> Validation = new List<ValidationIssue>();
        public ChassisCalculationGeometry ChassisGeometry;
        public ChassisCalculationResult ChassisCalculation;
        public ValidationStatus LayoutStatus = ValidationStatus.NOT_YET_EVALUABLE;
        public ValidationStatus WrapAngleStatus = ValidationStatus.NOT_YET_EVALUABLE;
        public ValidationStatus CodeDiameterRatioStatus = ValidationStatus.NOT_YET_EVALUABLE;

        public bool CanGenerate
        {
            get
            {
                return !Validation.Any(v => v.BlocksGeneration);
            }
        }

        // Accepted V1 component dimensions are results of the selected native template, not editable inputs.
        public double CarInternalHeightMm = 2200;
        public double CarOverallHeightMm = 2400;
        public double CounterweightHeightMm = 3000;
        public double DoorClearHeightMm = 2000;
        public double DoorConstructionHeightMm = 2160;
        public double LandingSlabThicknessMm = 250;
        public double MachineRoomClearHeightMm = 2200;
        public string CarRailProfile = "T70/65/9";
        public string CounterweightRailProfile = "T50/50/5";
    }
}
