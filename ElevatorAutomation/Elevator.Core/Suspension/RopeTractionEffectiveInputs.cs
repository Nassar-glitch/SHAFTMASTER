namespace Elevator.Core.Suspension
{
    /// <summary>The inputs used by the rope and traction checks: project values with the presets applied.</summary>
    public sealed class RopeTractionEffectiveInputs
    {
        public string RopeDesignation;
        public string GrooveType;
        public string TractionLimitingMethod;
        public double? MinimumBreakingLoadKn;
        public double? RopeMassKgPerM;
        public double? UndercutAngleDeg;
        public double? GrooveAngleDeg;
        public double? TravellingCableKgPerM;
        public double? BrakingRetardationMps2;
        public double? DeflectorMassKg;
        public double? RopeDiameterMm;
        public double? SheavePitchDiameterMm;
        public double? RopingRatio;
        public double? SpeedMps;
        public double? CapacityKg;
        public double? CarMassKg;
        public double? CounterweightMassKg;
        public double? OverheadMm;
        public int? RopeCount;
    }
}
