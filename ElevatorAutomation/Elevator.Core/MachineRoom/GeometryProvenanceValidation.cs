using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.MachineRoom
{
    public sealed class GeometryProvenanceValidation
    {
        public string Status;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();

        public bool IsValid
        {
            get
            {
                return Issues.Count == 0;
            }
        }
    }
}
