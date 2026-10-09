using System;
using System.Globalization;
using System.Linq;

namespace Elevator.Core.Suspension
{
    // Traction sheave groove types of EN 81-50:2014 5.11.2.3.1.
    public static class GrooveTypes
    {
        public const string UndercutU = "UndercutU";
        public const string VHardened = "VHardened";
        public const string VUnhardened = "VUnhardened";
        public static readonly string[] All = { UndercutU, VHardened, VUnhardened };

        public static string Label(string type)
        {
            return type == UndercutU
                ? "Semi-circular / undercut"
                : type == VHardened ? "V-groove, hardened" : type == VUnhardened ? "V-groove, unhardened" : type;
        }

        public static bool NeedsUndercut(string type)
        {
            return type == UndercutU || type == VUnhardened;
        }

        public static bool NeedsGrooveAngle(string type)
        {
            return type == UndercutU || type == VHardened || type == VUnhardened;
        }

        // Limits of EN 81-50:2014 5.11.2.3.1: β ≤ 105°; semi-circular γ ≥ 25°; V-groove γ ≥ 35° for lifts.
        public static string AngleProblem(string type, double? beta, double? gamma)
        {
            if (!All.Contains(type))
                return "groove type must be one of " + String.Join(", ", All);
            if (NeedsUndercut(type))
            {
                if (!beta.HasValue)
                    return "undercut angle β required";
                if (Double.IsNaN(beta.Value) || beta.Value < 0 || beta.Value > 105)
                    return "undercut angle β must be 0–105°";
                if (type == VUnhardened && beta.Value <= 0)
                    return "an unhardened V-groove needs an undercut (β > 0)";
            }
            if (NeedsGrooveAngle(type))
            {
                if (!gamma.HasValue)
                    return "groove angle γ required";
                double min = type == UndercutU ? 25 : 35;
                if (Double.IsNaN(gamma.Value) || gamma.Value < min || gamma.Value >= 180)
                    return "groove angle γ must be at least " + min.ToString(CultureInfo.InvariantCulture) + "° and below 180°";
            }
            return null;
        }
    }
}
