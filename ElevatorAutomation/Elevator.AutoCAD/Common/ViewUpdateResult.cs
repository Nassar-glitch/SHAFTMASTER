using System.Collections.Generic;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.Common
{
    public sealed class ViewUpdateResult
    {
        public Dictionary<string, double> Dimensions;
        public Dictionary<string, double> Clearances;
        public Dictionary<string, string> Annotations;
        public PlanCoverageReport Coverage;
    }
}
