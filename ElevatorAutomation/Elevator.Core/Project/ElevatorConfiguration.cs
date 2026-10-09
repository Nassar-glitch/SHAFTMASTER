namespace Elevator.Core.Project
{
    public sealed class ElevatorConfiguration
    {
        public string SystemId = "traction-geared";
        public ElevatorType LegacyFamily = ElevatorType.ExistingPlan;
        public string TemplateId = "existing-plan-v1";
        public double? CapacityKg;
        public double? SpeedMetresPerSecond;
        public string DriveType;
    }
}
