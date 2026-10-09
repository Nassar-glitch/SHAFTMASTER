using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.SafetyGear
{
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
