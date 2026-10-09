using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Result of checking that chassis geometry still matches the inputs it was generated from.</summary>
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
