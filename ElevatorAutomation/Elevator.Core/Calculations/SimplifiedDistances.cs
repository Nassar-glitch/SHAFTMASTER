using Elevator.Core.Geometry;

namespace Elevator.Core.Calculations
{
    /// <summary>Lever arms and spans of the Basic two-stage load distribution, in mm.</summary>
    public sealed class SimplifiedDistances
    {
        public LocalPoint LoadReference;
        public double FrontRowY;
        public double RearRowY;
        public double LongitudinalSpanMm;
        public double LoadFromFrontMm;
        public double LoadToRearMm;
        public double FrontTransverseSpanMm;
        public double RearTransverseSpanMm;
        public double FrontLoadFromLeftMm;
        public double FrontLoadToRightMm;
        public double RearLoadFromLeftMm;
        public double RearLoadToRightMm;
        public double FrontLoadN;
        public double RearLoadN;
    }
}
