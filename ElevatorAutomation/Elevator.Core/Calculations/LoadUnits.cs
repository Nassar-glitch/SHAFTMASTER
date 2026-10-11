using System;
using System.Globalization;

namespace Elevator.Core.Calculations
{
    /// <summary>
    /// Display conversion of a force in newtons to kilograms (DESIGN-FLOW principle 10): kg = N / g, rounded UP to the next multiple of
    /// 5 kg, an exact multiple stays. A display rule only: every calculation and every OK / NOT OK result keeps the unrounded newtons.
    /// A negative force (an uplift) is converted by its magnitude and keeps its sign.
    /// </summary>
    public static class LoadUnits
    {
        /// <summary>The standard acceleration of the calculations (the chassis engine's constant, not a copy).</summary>
        public const double GravityMps2 = MachineRoom.ChassisCalculationEngine.GravityMetresPerSecondSquared;

        /// <summary>The rounding step in kg.</summary>
        public const double KgStep = 5;

        /// <summary>The kg value of a force in N: N / g rounded up to a multiple of 5 kg.</summary>
        public static double Kg(double newtons)
        {
            double steps = Math.Abs(newtons) / GravityMps2 / KgStep;
            // Floating-point noise on an exact multiple (1200 kg = 11772 N) must not jump to the next step.
            double nearest = Math.Round(steps);
            double up = Math.Abs(steps - nearest) <= 1e-9 * Math.Max(1.0, steps) ? nearest : Math.Ceiling(steps);
            double kg = up * KgStep;
            return newtons < 0 && kg > 0 ? -kg : kg;
        }

        /// <summary>The kg value as text, no unit, for example "1205".</summary>
        public static string KgText(double newtons)
        {
            return Kg(newtons).ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>The kg value with its unit: "1205 kg".</summary>
        public static string KgWithUnit(double newtons)
        {
            return KgText(newtons) + " kg";
        }

        /// <summary>A force text with its kg value beside it: "12.35 kN (1260 kg)".</summary>
        public static string Beside(string text, double newtons)
        {
            return text + " (" + KgWithUnit(newtons) + ")";
        }
    }
}
