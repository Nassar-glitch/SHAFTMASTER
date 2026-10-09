namespace Elevator.Core.MachineRoom
{
    public sealed class DeflectorSheaveSelection
    {
        public bool IsRequired { get; set; }
        public double? PitchDiameterMm { get; set; }
        public string SelectionStatus { get; set; }
        public string RuleSource { get; set; }
    }
}
