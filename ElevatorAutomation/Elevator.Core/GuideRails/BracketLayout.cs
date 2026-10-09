using System.Collections.Generic;

namespace Elevator.Core.GuideRails
{
    public sealed class BracketLayout
    {
        public List<double> Elevations = new List<double>();
        public List<double> ConsecutiveSpacings = new List<double>();
        public List<string> Observations = new List<string>();
        public string SpacingStatus = "PASS";
        public string FixingStatus = "NOT_YET_EVALUABLE";
        public string RailEndAdequacyStatus = "NOT_YET_EVALUABLE";
        public string RailJointDetailStatus = "NOT_YET_IMPLEMENTED";
    }
}
