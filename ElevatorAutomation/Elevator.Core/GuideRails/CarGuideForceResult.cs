using System.Collections.Generic;
using Elevator.Core.Calculations;

namespace Elevator.Core.GuideRails
{
    /// <summary>FF1 and FF2 results with status, issues and calculation details.</summary>
    public sealed class CarGuideForceResult
    {
        public string Status = "MISSING_INPUT";
        public string RuleVersion = CarGuideForceCalculationEngine.RuleVersion;
        public string InputFingerprint;
        public string RuleBasis = CarGuideForceCalculationEngine.RuleBasis;
        public double? CapacityKg;
        public double? InternalWidthMm;
        public double? InternalDepthMm;
        public double? GuideShoeSpacingMm;
        public double? FF1BaseN;
        public double? FF1DesignN;
        public double? FF2BaseN;
        public double? FF2DesignN;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public List<string> Details = new List<string>();
        internal CarGuideForceGeometry Geometry;
    }
}
