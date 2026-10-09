namespace Elevator.Core.Suspension
{
    /// <summary>
    /// Geometry shared with the drawings: deflector (MachineRoomRules.SelectDeflector on TZ), reeving path and wrap angle
    /// (MachineRoomSectionRules / ReevingSolver), served travel (ProjectFloors).
    /// </summary>
    public sealed class RopeTractionGeometry
    {
        public bool Available;
        public string Problem;
        public double? TravelMm;
        public double? TzMm;
        public bool DeflectorPresent;
        public bool DeflectorPathValid;
        public double? DeflectorPitchMm;
        public double? WrapAngleDeg;
        public double? CarWidthMm;
        public double? CarDepthMm;

        /// <summary>A copy, so that a cached geometry is never shared between callers.</summary>
        public RopeTractionGeometry Copy()
        {
            return (RopeTractionGeometry)MemberwiseClone();
        }
    }
}
