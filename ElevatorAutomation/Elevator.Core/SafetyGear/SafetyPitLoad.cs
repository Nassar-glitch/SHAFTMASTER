using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.SafetyGear
{
    /// <summary>One of the loads P5, P6 and P7 with its mass, status, issues and details.</summary>
    public sealed class SafetyPitLoad
    {
        public string Id;
        public string Application;
        public string Status = "MISSING_INPUT";
        public string Units = "N";
        public double? GroupLoadN;
        public double? PerComponentLoadN;
        public double? ParticipatingMassKg;
        public int? ComponentCount;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public List<string> Details = new List<string>();
    }
}
