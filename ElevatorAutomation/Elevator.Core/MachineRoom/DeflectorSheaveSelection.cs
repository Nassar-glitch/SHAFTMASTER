namespace Elevator.Core.MachineRoom
{
    /// <summary>The deflector sheave selected from TZ: pitch diameter and selection status.</summary>
    public sealed class DeflectorSheaveSelection
    {
        public bool IsRequired { get; set; }
        public double? PitchDiameterMm { get; set; }
        public string SelectionStatus { get; set; }
        public string RuleSource { get; set; }
    }
}
