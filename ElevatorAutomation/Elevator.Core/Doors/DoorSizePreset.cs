namespace Elevator.Core.Doors
{
    /// <summary>The clearance presets of one clear-opening size of an automatic door type (mm).</summary>
    public sealed class DoorSizePreset
    {
        public double OpeningMm;

        /// <summary>Preferred car return on the fixed side (2C: each side); the layout may shrink it down to the minimum.</summary>
        public double FixedReturnMm;

        /// <summary>Preferred car return on the stacking side (2C: equal to the fixed side).</summary>
        public double StackReturnMm;

        /// <summary>Preferred landing jamb on the fixed side (2C: the stacking pocket).</summary>
        public double LandingFixedJambMm;

        /// <summary>Landing stacking pocket beyond the opening edge on the stacking side.</summary>
        public double LandingStackPocketMm;
    }
}
