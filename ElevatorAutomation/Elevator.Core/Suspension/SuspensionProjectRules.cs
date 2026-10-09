using System;

namespace Elevator.Core.Suspension
{
    public static class SuspensionProjectRules
    {
        public static void Validate(double? diameter, int? number)
        {
            if (diameter.HasValue && Array.IndexOf(new[] { 6.5, 8, 10, 11, 12 }, diameter.Value) < 0)
                throw new ArgumentException("Supported suspension rope diameters: 6.5, 8, 10, 11, 12 mm.");
            if (number.HasValue && (number < 4 || number > 7))
                throw new ArgumentException("Supported suspension rope counts: 4 through 7.");
        }
    }
}
