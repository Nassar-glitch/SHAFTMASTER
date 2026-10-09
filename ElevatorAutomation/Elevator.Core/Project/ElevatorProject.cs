using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Core.Car;
using Elevator.Core.Counterweight;
using Elevator.Core.Doors;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;
using Elevator.Core.Shaft;
using Elevator.Core.Suspension;

namespace Elevator.Core.Project
{
    /// <summary>Authoritative inputs; measured template data and calculated geometry live in context/results.</summary>
    public sealed class ElevatorProject
    {
        public ProjectInfo Info = new ProjectInfo();
        public ElevatorConfiguration Elevator = new ElevatorConfiguration();
        public BuildingConfiguration Building = new BuildingConfiguration();
        public ShaftConfiguration Shaft = new ShaftConfiguration();
        public CarConfiguration Car = new CarConfiguration();
        public CounterweightConfiguration Counterweight = new CounterweightConfiguration();
        public DoorConfiguration Doors = new DoorConfiguration();
        public MachineConfiguration Machine = new MachineConfiguration();
        public SuspensionConfiguration Suspension = new SuspensionConfiguration();
        public RailConfiguration Rails = new RailConfiguration();
        public MachineRoomConfiguration MachineRoom = new MachineRoomConfiguration();
        public DrawingConfiguration Drawing = new DrawingConfiguration();
        public StandardsConfiguration Standards = new StandardsConfiguration();
        public ChassisCalculationInputs Calculations = new ChassisCalculationInputs();

        // Optional calculation-sheet metadata. Null in older projects; never used by any calculation,
        // drawing geometry, STEP parameter or calculation fingerprint.
        public ProjectReportDetails Report;

        // Keys of preset-capable fields the user has edited (locked). Null in older projects: every such field follows its preset.
        public List<string> UserEditedFields;

        // True for projects created from the presets (or after "Fill blanks from presets"): unlocked project values follow
        // config/presets.json. Null in older projects: their values count as user-edited and blanks stay blank.
        public bool? FollowsPresets;
        public Dictionary<string, object> Extensions = new Dictionary<string, object>();
    }
}
