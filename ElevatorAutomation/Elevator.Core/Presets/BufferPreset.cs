namespace Elevator.Core.Presets
{
    /// <summary>Buffer preset: speed up to which energy-accumulation buffers apply, buffer counts and sharing.</summary>
    public sealed class BufferPreset
    {
        public double? EnergyAccumulationUpToSpeedMps;
        public int CarCount;
        public int CounterweightCount;
        public string Sharing;
    }
}
