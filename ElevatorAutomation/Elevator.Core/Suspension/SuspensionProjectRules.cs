using System;

namespace Elevator.Core.Suspension
{
    /// <summary>Validation of the suspension rope diameter and number.</summary>
    public static class SuspensionProjectRules
    {
        /// <summary>
        /// Throws ArgumentException when the rope diameter is not 6.5, 8, 10, 11 or 12 mm, or the rope count is outside 4–7.
        /// </summary>
        public static void Validate(double? diameter, int? number)
        {
            if (diameter.HasValue && Array.IndexOf(new[] { 6.5, 8, 10, 11, 12 }, diameter.Value) < 0)
                throw new ArgumentException("Supported suspension rope diameters: 6.5, 8, 10, 11, 12 mm.");
            if (number.HasValue && (number < 4 || number > 7))
                throw new ArgumentException("Supported suspension rope counts: 4 through 7.");
        }
    }
}
