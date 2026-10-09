namespace Elevator.Core.Suspension
{
    // Rope / traction inputs (Suspension page). All optional and nullable; a null field of an unlocked key takes its preset
    // from config/presets.json through PresetFields. Older projects have no group.
    public sealed class RopeTractionInputs
    {
        public double? MinimumBreakingLoadKn;
        public double? RopeMassKgPerM;
        public string RopeDesignation;
        public string GrooveType;
        public double? UndercutAngleDeg;
        public double? GrooveAngleDeg;
        public double? TravellingCableKgPerM;
        public double? BrakingRetardationMps2;
        public double? DeflectorMassKg;
        public string TractionLimitingMethod;

        // Undercut β chosen by the preset selection (UndercutAngleStepsDeg) while β is unlocked; null = the library preset.
        // It is not a user value: a user edit is stored in UndercutAngleDeg and locks β.
        public double? SelectedUndercutAngleDeg;

        // Balance factor q chosen by the preset selection (BalanceFactorSteps) while the counterweight is unlocked; null = the library q.
        public double? SelectedBalanceFactor;
    }
}
