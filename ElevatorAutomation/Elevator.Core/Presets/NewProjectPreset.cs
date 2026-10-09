namespace Elevator.Core.Presets
{
    /// <summary>Presets of a new project: shaft, floors, rated load and speed.</summary>
    public sealed class NewProjectPreset
    {
        public double? ShaftWidthMm;
        public double? ShaftDepthMm;
        public double? PitDepthMm;
        public double? OverheadMm;
        public double? FloorHeightMm;
        public double? CapacityKg;
        public double? SpeedMps;
        public int? Stops;
    }
}
