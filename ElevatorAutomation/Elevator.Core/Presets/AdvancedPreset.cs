namespace Elevator.Core.Presets
{
    /// <summary>Presets of the Advanced P1–P4 inputs.</summary>
    public sealed class AdvancedPreset
    {
        public double? MachineCgOffsetXMm;
        public double? MachineCgOffsetYMm;
        public double? MachineCgOffsetZMm;
        public double? HangerAttachmentOffsetXMm;
        public double? HangerAttachmentOffsetYMm;
        public double? AdditionalMassKg;
        public string LoadCase;
        public string TransferModel;
        public string MachineCgBasis;
        public string TransferBasis;
        public string HangerAttachmentBasis;
        public string ExcludedWeightBasis;
    }
}
