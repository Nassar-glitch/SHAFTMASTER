using System;
using System.Globalization;

namespace Elevator.Core.Calculations
{
    /// <summary>Display conversion of a force in newtons to kilograms-force (kg = N / 9.81), rounded up to the next multiple of 5 kg.</summary>
    public static class LoadUnits
    {
        public const double GravityMps2 = 9.81;
        public const double KgStep = 5;

        public static double Kg(double newtons)
        {
            return Math.Ceiling(newtons / GravityMps2 / KgStep - 1e-9) * KgStep;
        }

        public static string KgText(double newtons)
        {
            return Kg(newtons).ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
