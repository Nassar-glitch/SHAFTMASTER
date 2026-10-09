using System.Collections.Generic;

namespace Elevator.Core.MachineRoom
{
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
