using System.Collections.Generic;
using Elevator.Core.GuideRails;

namespace Elevator.Core.Geometry
{
    public sealed class SectionAAGeometry
    {
        public double PitZ;
        public double Underside;
        public double R;
        public double Ceiling;
        public double CarZ;
        public double CWBottom;
        public double Travel;
        public BracketLayout Brackets;
        public List<VerticalFloor> Floors;
        public string LayoutStatus = "PASS";
        public string RailEndAdequacy = "NOT_YET_EVALUABLE";
    }
}
