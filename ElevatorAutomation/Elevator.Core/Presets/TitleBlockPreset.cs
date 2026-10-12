namespace Elevator.Core.Presets
{
    /// <summary>
    /// Title-block presets of the drawing sheet (DESIGN-FLOW "Layouts and PDF"): the control and the supply texts. Each project follows
    /// them until the user edits its own value (lock and follow, principle 4).
    /// </summary>
    public sealed class TitleBlockPreset
    {
        /// <summary>The shipped control text.</summary>
        public const string DefaultControl = "SIMPLEX COLLECTIVE DOWN.";
        /// <summary>The shipped supply text.</summary>
        public const string DefaultSupply = "3 PHASE 380 V. 50 Hz.";

        public string Control;
        public string Supply;

        /// <summary>The shipped title-block presets.</summary>
        public static TitleBlockPreset Default()
        {
            return new TitleBlockPreset { Control = DefaultControl, Supply = DefaultSupply };
        }
    }
}
