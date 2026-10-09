using System;

namespace Elevator.Reporting.Figures
{
    /// <summary>The font family used by figures.</summary>
    public static class FigureFonts
    {
        static string family;
        public static Func<string> Resolve = () => "Arial";

        public static string Family
        {
            get
            {
                return family ?? (family = Resolve());
            }
        }
    }
}
