using System;

namespace Elevator.Core.Doors
{
    /// <summary>The car door types and handings of a project (DESIGN-FLOW "Doors").</summary>
    public static class DoorTypes
    {
        /// <summary>Manual door: the existing allowances, unchanged.</summary>
        public const string Manual = "Manual";
        /// <summary>Automatic two-panel telescopic side-opening door.</summary>
        public const string Automatic2S = "Automatic2S";
        /// <summary>Automatic two-panel centre-opening door.</summary>
        public const string Automatic2C = "Automatic2C";
        /// <summary>Handing: the stacking side is opposite the speed governor.</summary>
        public const string Auto = "Auto";
        /// <summary>Stacking side left.</summary>
        public const string Left = "Left";
        /// <summary>Stacking side right.</summary>
        public const string Right = "Right";

        public static readonly string[] All = { Manual, Automatic2S, Automatic2C };
        public static readonly string[] Handings = { Auto, Left, Right };

        /// <summary>The canonical type: null, empty and unknown text are Manual.</summary>
        public static string Normalize(string type)
        {
            if (String.Equals(type, Automatic2S, StringComparison.OrdinalIgnoreCase))
                return Automatic2S;
            if (String.Equals(type, Automatic2C, StringComparison.OrdinalIgnoreCase))
                return Automatic2C;
            return Manual;
        }

        /// <summary>True for the automatic types.</summary>
        public static bool IsAutomatic(string type)
        {
            return Normalize(type) != Manual;
        }

        /// <summary>The name shown to the user.</summary>
        public static string Display(string type)
        {
            switch (Normalize(type))
            {
                case Automatic2S:
                    return "Automatic 2S (side opening)";
                case Automatic2C:
                    return "Automatic 2C (centre opening)";
                default:
                    return "Manual";
            }
        }
    }
}
