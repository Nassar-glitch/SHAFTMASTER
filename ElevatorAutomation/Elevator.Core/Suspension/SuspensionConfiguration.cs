namespace Elevator.Core.Suspension
{
    /// <summary>Suspension inputs of the project: rope data and roping ratio.</summary>
    public sealed class SuspensionConfiguration
    {
        public string Type;
        public double? RopeDiameterMm;
        public double? RopingRatio;
        public int? RopeCount;
    }
}
