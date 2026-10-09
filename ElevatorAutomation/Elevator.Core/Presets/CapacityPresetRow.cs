namespace Elevator.Core.Presets
{
    /// <summary>One row of the capacity table: car and machine mass for a rated load.</summary>
    public sealed class CapacityPresetRow
    {
        public double CapacityKg;
        public double CarMassKg;
        public double MachineMassKg;
    }
}
