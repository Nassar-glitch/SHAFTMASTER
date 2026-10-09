using System;
using System.Collections.Generic;
namespace Elevator.Core.Project
{
    // Authoritative inputs; measured template data and calculated geometry live in context/results.
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
        public Elevator.Core.Calculations.ChassisCalculationInputs Calculations = new Elevator.Core.Calculations.ChassisCalculationInputs();
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
    public sealed class ProjectInfo { public string ProjectId = Guid.NewGuid().ToString("N"); public string Name; }
    // All fields optional, no defaults; blank means not shown on the calculation sheet.
    public sealed class ProjectReportDetails { public string ClientName, DocumentNumber, Revision, PreparedBy, CheckedBy, ApprovedBy; }
    public sealed class ElevatorConfiguration
    {
        public string SystemId = "traction-geared";
        public ElevatorType LegacyFamily = ElevatorType.ExistingPlan;
        public string TemplateId = "existing-plan-v1";
        public double? CapacityKg, SpeedMetresPerSecond;
        public string DriveType;
    }
    public sealed class BuildingConfiguration { public List<FloorDefinition> Floors = new List<FloorDefinition>(); }
    public sealed class FloorDefinition
    {
        public string FloorId = Guid.NewGuid().ToString("N"), Name;
        // Interval to NEXT normal floor; last is null. Structural R is calculated.
        public double? HeightToNextMm;
        public bool IsServed = true;
        public LandingDoorConfiguration LandingDoor = new LandingDoorConfiguration();
    }
    public sealed class LandingDoorConfiguration { public string Type, EntranceConfiguration; }
    public sealed class ShaftConfiguration { public double WidthMm, DepthMm; public double? PitDepthMm, OverheadMm; }
    public sealed class CarConfiguration { public string DisplayFloorId; }
    public sealed class CounterweightConfiguration
    {
        public string Position;
        public double? ReferenceCarElevationMm, ReferenceBottomElevationMm;
        public string CalibrationStatus = "NOT PROVIDED";
    }
    public sealed class DoorConfiguration { public string Type, EntranceConfiguration; }
    public sealed class MachineConfiguration { public string Type; public double? TractionSheavePitchDiameterMm; }
    public sealed class SuspensionConfiguration { public string Type; public double? RopeDiameterMm, RopingRatio; public int? RopeCount; }
    public sealed class RailConfiguration { public string RequestedRailType; public double? BracketMaximumSpacingMm; public string BracketSource = "NOT PROVIDED"; }
    public sealed class MachineRoomConfiguration
    {
        public double? LeftExtensionMm, RightExtensionMm, FrontExtensionMm, RearExtensionMm, MinimumDeflectorSlabClearanceMm;
    }
    public sealed class DrawingConfiguration { public string PlanTitle; public double ViewMarginMm = 1000; public string Arrangement = "TwoByTwo"; }
    public sealed class StandardsConfiguration
    {
        public string RuleSetId = "accepted-project-rules-v1";
        public string CodeInterpretation = "Existing EN 81-20:2014 interpretation; not full certification";
        public string ManufacturerConstraints, CompanyConstraints;
        public List<string> EngineeringAssumptions = new List<string>();
    }
    public enum ValidationStatus { PASS, WARNING, FAIL, NOT_YET_EVALUABLE, REVIEW_REQUIRED }
    public sealed class ValidationIssue { public string Code, Field, Message; public ValidationStatus Status; public bool BlocksGeneration; }
    public sealed class ProjectDocument { public int schemaVersion = 1; public ElevatorProject project; }
}
