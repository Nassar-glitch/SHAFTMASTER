using System;

namespace Elevator.Reporting.Figures
{
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
