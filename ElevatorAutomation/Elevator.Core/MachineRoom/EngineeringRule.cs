using System.Collections.Generic;

namespace Elevator.Core.MachineRoom
{
    /// <summary>One machine-room rule with its source, requirement, calculated value and status.</summary>
    public sealed class EngineeringRule
    {
        public string RuleId;
        public string Description;
        public string SourceDocument;
        public string Edition;
        public string Clause;
        public RequirementType RequirementType;
        public Dictionary<string, object> InputParameters;
        public string CalculatedValue;
        public string RequiredValue;
        public string Status;
        public string EngineeringNote;
    }
}
