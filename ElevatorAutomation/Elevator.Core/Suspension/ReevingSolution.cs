using Elevator.Core.Geometry;

namespace Elevator.Core.Suspension
{
    /// <summary>Result of the traction and deflector geometry: tangents, directions and wrap angle.</summary>
    public sealed class ReevingSolution
    {
        public LocalPoint TractionSheaveCenter;
        public LocalPoint DeflectorSheaveCenter;
        public LocalPoint TractionTangentPoint;
        public LocalPoint DeflectorTangentPoint;
        public LocalPoint RopeEntryDirection;
        public LocalPoint RopeExitDirection;
        public double TractionSheavePitchDiameterMm;
        public double DeflectorSheavePitchDiameterMm;
        public double HorizontalCenterOffsetMm;
        public double VerticalCenterOffsetMm;
        public double CenterDistanceMm;
        public double? CalculatedWrapAngleDeg;
        public double? RopeEntryAngleDeg;
        public double? RopeExitAngleDeg;
        public double TargetWrapAngleDeg = 152;
        public string GeometryStatus;
        public string WrapAngleStatus;
    }
}
