namespace Elevator.Core.Suspension
{
    /// <summary>Static rope tensions of the car and counterweight sides, in total and per rope.</summary>
    public sealed class StaticRopeTensions
    {
        public double CarBundleN;
        public double CounterweightBundleN;
        public double CarPerRopeN;
        public double CounterweightPerRopeN;
    }
}
