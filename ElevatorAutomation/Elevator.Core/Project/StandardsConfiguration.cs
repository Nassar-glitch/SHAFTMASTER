using System.Collections.Generic;

namespace Elevator.Core.Project
{
    public sealed class StandardsConfiguration
    {
        public string RuleSetId = "accepted-project-rules-v1";
        public string CodeInterpretation = "Existing EN 81-20:2014 interpretation; not full certification";
        public string ManufacturerConstraints;
        public string CompanyConstraints;
        public List<string> EngineeringAssumptions = new List<string>();
    }
}
