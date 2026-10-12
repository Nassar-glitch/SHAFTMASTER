namespace Elevator.Core.Presets
{
    /// <summary>
    /// Title-block presets of the drawing sheet (DESIGN-FLOW "Layouts and PDF"): the control, the supply, the type of work and the drawing name. Each
    /// project follows them until the user edits its own value (lock and follow, principle 4).
    /// </summary>
    public sealed class TitleBlockPreset
    {
        /// <summary>The shipped control text.</summary>
        public const string DefaultControl = "SIMPLEX COLLECTIVE DOWN.";
        /// <summary>The shipped supply text.</summary>
        public const string DefaultSupply = "3 PHASE 380 V. 50 Hz.";

        /// <summary>The shipped type of work: "elevator works".</summary>
        public const string DefaultTypeOfWork = "\u0623\u0639\u0645\u0627\u0644 \u0645\u0635\u0627\u0639\u062F"; // أعمال مصاعد

        /// <summary>The shipped drawing name: "executive works drawing of the elevator".</summary>
        public const string DefaultDrawingName = "لوحة الأعمال التنفيذية للمصعد"; // لوحة الأعمال التنفيذية للمصعد

        public string Control;
        public string Supply;
        /// <summary>The type of work; null in an older library: the shipped default.</summary>
        public string TypeOfWork;
        /// <summary>The drawing name; null in an older library: the shipped default.</summary>
        public string DrawingName;

        /// <summary>The type of work of this preset, or the shipped default.</summary>
        public string TypeOfWorkOrDefault()
        {
            return TypeOfWork ?? DefaultTypeOfWork;
        }

        /// <summary>The drawing name of this preset, or the shipped default.</summary>
        public string DrawingNameOrDefault()
        {
            return DrawingName ?? DefaultDrawingName;
        }

        /// <summary>The shipped title-block presets.</summary>
        public static TitleBlockPreset Default()
        {
            return new TitleBlockPreset { Control = DefaultControl, Supply = DefaultSupply, TypeOfWork = DefaultTypeOfWork, DrawingName = DefaultDrawingName };
        }
    }
}
