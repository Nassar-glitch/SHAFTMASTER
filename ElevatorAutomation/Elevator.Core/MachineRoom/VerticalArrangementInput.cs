namespace Elevator.Core.MachineRoom
{
    /// <summary>Inputs of the vertical machine arrangement: drops, sheaves, steel and hanger.</summary>
    public sealed class VerticalArrangementInput
    {
        public double CarDropX;
        public double CwDropX;
        public double TractionDiameterMm;
        public double DeflectorDiameterMm;
        public double MachineCenterAboveSteelMm;
        public double FixedLevelingDepthMm;
        public double SteelDepthMm;
        public double HangerTopBelowSteelTopMm;
        public double HangerBottomAboveDeflectorCenterMm;
        public double? MinimumDeflectorSlabClearanceMm;
        public int StackedSteelLevels;
        public double MinConcreteHeightMm = 350;
        public double MaxConcreteHeightMm = 1000;
        public double TargetWrapAngleDeg = 152;
        public bool AdjustableDeflectorHangerConfirmed;
        public double? PreferredHeightIncrementMm;
        public double? ReferenceConcreteHeightMm;
    }
}
