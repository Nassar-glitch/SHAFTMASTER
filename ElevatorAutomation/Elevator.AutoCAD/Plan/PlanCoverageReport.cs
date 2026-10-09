using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Counts of bound and unbound plan entities.</summary>
    public sealed class PlanCoverageReport
    {
        public int ModelEntities;
        public int BoundEntities;
        public int UnboundEntities;
        public int Hatches;
        public int Dimensions;
        public int DrivenAnnotations;
        public int OtherViewEntities;
        public Dictionary<string, int> UnboundByType;
        public string[] StaticAnnotations;
    }
}
