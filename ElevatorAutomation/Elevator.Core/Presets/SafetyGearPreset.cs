namespace Elevator.Core.Presets
{
    /// <summary>Safety-gear preset: speed above which progressive gear is used, and the car guide count.</summary>
    public sealed class SafetyGearPreset
    {
        public double? ProgressiveAboveSpeedMps;
        public int CarGuideCount;
    }
}
