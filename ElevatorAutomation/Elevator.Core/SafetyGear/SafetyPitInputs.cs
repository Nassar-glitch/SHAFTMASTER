namespace Elevator.Core.SafetyGear
{
    /// <summary>Safety-gear and buffer choices for P5–P7.</summary>
    public sealed class SafetyPitInputs
    {
        public string SafetyGearType;
        public int? CarGuideCount;
        public int? CarBufferCount;
        public int? CounterweightBufferCount;
        public string CarBufferSharing;
        public string CounterweightBufferSharing;
        public string CarAdditionalMassAllocation;
        public string CounterweightAdditionalMassAllocation;
        public double? CarEventAdditionalMassPercent;
        public double? CounterweightEventAdditionalMassPercent;

        // Buffer type (EnergyAccumulation / EnergyDissipation). A project selection only; P6/P7 equations do not use it.
        public string BufferType;
    }
}
