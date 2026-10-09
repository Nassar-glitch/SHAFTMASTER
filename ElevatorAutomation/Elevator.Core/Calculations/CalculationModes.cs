using System.Linq;

namespace Elevator.Core.Calculations
{
    public static class CalculationModes
    {
        public static string Resolve(ChassisCalculationInputs c)
        {
            if (c == null)
                return "Basic";
            if (c.Mode != null)
                return c.Mode;
            // Any pre-mode calculation data belongs to the pre-existing Advanced workflow.
            return typeof(ChassisCalculationInputs).GetFields().Any(f =>
                f.Name != "Mode" && f.Name != "SafetyPit" && f.Name != "CarGuideForces" && f.GetValue(c) != null) ? "Advanced" : "Basic";
        }
    }
}
