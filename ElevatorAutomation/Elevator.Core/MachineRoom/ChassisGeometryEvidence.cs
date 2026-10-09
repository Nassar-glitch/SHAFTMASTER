using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Native chassis drawing evidence: the contours and the dimensions they were read at.</summary>
    public sealed class ChassisGeometryEvidence
    {
        public double SW;
        public double SD;
        public double CarRow;
        public double CwRow;
        public double Pitch;
        public MachineRoomExtensions Extensions;
        public double GovernorStartOffset;
        public List<ChassisContour> Contours = new List<ChassisContour>();
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
    }
}
