namespace Elevator.Core.Counterweight
{
    /// <summary>Counterweight inputs of the project.</summary>
    public sealed class CounterweightConfiguration
    {
        public string Position;
        public double? ReferenceCarElevationMm;
        public double? ReferenceBottomElevationMm;
        public string CalibrationStatus = "NOT PROVIDED";
    }
}
