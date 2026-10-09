using System.Collections.Generic;

namespace Elevator.Core
{
    public sealed class ProjectInput
    {
        public int SchemaVersion
        {
            get; set;
        }

        public ElevatorType ElevatorType
        {
            get; set;
        }

        public string TemplateId
        {
            get; set;
        }

        public double SW
        {
            get; set;
        }

        public double SD
        {
            get; set;
        }

        public string ProjectName
        {
            get; set;
        }

        public double? Capacity
        {
            get; set;
        }

        public string PlanTitle
        {
            get; set;
        }

        public double? MachineRoomLeftExtension
        {
            get; set;
        }

        public double? MachineRoomRightExtension
        {
            get; set;
        }

        public double? MachineRoomFrontExtension
        {
            get; set;
        }

        public double? MachineRoomRearExtension
        {
            get; set;
        }

        public double? SuspensionRopeDiameterMm
        {
            get; set;
        }

        public double? TractionSheavePitchDiameterMm
        {
            get; set;
        }

        public int? NumberOfSuspensionRopes
        {
            get; set;
        }

        public double? MinimumDeflectorSlabClearanceMm
        {
            get; set;
        }

        public int? Stops
        {
            get; set;
        }

        public double? Travel
        {
            get; set;
        }

        public double? Pit
        {
            get; set;
        }

        public double? Overhead
        {
            get; set;
        }

        public double[] FloorHeights
        {
            get; set;
        }

        public string DoorType
        {
            get; set;
        }

        public string EntranceConfiguration
        {
            get; set;
        }

        public string CounterweightPosition
        {
            get; set;
        }

        public string MachineType
        {
            get; set;
        }

        public double? Speed
        {
            get; set;
        }

        public string RailType
        {
            get; set;
        }

        public string SuspensionType
        {
            get; set;
        }

        public Dictionary<string, object> Extensions
        {
            get; set;
        }

        public ProjectInput()
        {
            SchemaVersion = 1;
            TemplateId = "existing-plan-v1";
            Extensions = new Dictionary<string, object>();
        }
    }

    public sealed class FamilyDefinition
    {
        public ElevatorType ElevatorType
        {
            get; set;
        }

        public string TemplateId
        {
            get; set;
        }

        public string TemplateFile
        {
            get; set;
        }

        public string ProfileFile
        {
            get; set;
        }

        public bool Enabled
        {
            get; set;
        }

        public string Status
        {
            get; set;
        }
    }
}
