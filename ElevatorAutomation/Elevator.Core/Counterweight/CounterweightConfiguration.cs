namespace Elevator.Core.Counterweight
{
    public sealed class CounterweightConfiguration
    {
        public string Position;
        public double? ReferenceCarElevationMm;
        public double? ReferenceBottomElevationMm;
        public string CalibrationStatus = "NOT PROVIDED";
    }
}
