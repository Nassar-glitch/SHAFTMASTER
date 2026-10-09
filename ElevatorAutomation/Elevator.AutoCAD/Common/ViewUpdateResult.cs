using System.Collections.Generic;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Dimensions, clearances, annotations and coverage reported by a view update.</summary>
    public sealed class ViewUpdateResult
    {
        public Dictionary<string, double> Dimensions;
        public Dictionary<string, double> Clearances;
        public Dictionary<string, string> Annotations;
        public PlanCoverageReport Coverage;
    }
}
