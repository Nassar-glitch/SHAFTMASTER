using System.Linq;

namespace Elevator.Core.Calculations
{
    /// <summary>Resolves the P1–P4 calculation mode of a project: Basic, or Advanced for projects that carry Advanced data.</summary>
    public static class CalculationModes
    {
        /// <summary>The calculation mode, Basic or Advanced, of the inputs; Advanced for older projects that carry Advanced data.</summary>
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
