using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class ChassisCalculationResult
    {
        public string Mode = "Advanced";
        public double? TotalMassKg;
        public double? TotalVerticalLoadN;
        public Dictionary<string, double> MassCompositionKg;
        public SimplifiedDistances SimplifiedGeometry;
        public string Status = "MISSING_INPUT";
        public string ModelVersion = "ReleasedTwoStageWithRigidSpreader-v1";
        public string InputFingerprint;
        public string Units = "mm, kg, N, N.mm (divide N.mm by 1000 for N.m)";
        public string VerificationStatus = "ENGINEERING REVIEW REQUIRED - simplified reactions only";
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public List<LoadNode> Loads = new List<LoadNode>();
        public List<MemberTransfer> Transfers = new List<MemberTransfer>();
        public List<BearingReaction> Reactions; // Null means unavailable; never four fabricated zeros.
        public double? VerticalResidualN;
        public double? MomentXResidualNmm;
        public double? MomentYResidualNmm;
        public LocalPoint UnverifiedLateralForceN;
        public double UnverifiedYawMomentNmm;
        public double? CarBundleTensionN;
        public double? CounterweightBundleTensionN;
        public double? CarPerRopeTensionN;
        public double? CounterweightPerRopeTensionN;
        public List<string> Assumptions = new List<string>();
    }
}
