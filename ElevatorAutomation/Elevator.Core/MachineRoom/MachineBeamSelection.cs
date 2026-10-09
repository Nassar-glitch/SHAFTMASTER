namespace Elevator.Core.MachineRoom
{
    /// <summary>The preliminary machine beam selected for a rated capacity and span.</summary>
    public sealed class MachineBeamSelection
    {
        public string SectionDesignation;
        public string SelectionMethod = "PreliminaryProjectRule";
        public string StructuralStatus;
        public double NominalDepthMm;
        public double SpanMm;
        public double CapacityKg;
        public double? ReactionN;
        public double? MaxBendingMomentNm;
        public double? MaxShearN;
        public double? BendingStressMPa;
        public double? DeflectionMm;
        public double? Utilization;
    }
}
