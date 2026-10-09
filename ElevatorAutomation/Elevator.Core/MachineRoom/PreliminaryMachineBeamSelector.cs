using System;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Preliminary machine beam table by rated capacity and span; no structural check.</summary>
    public sealed class PreliminaryMachineBeamSelector : IMachineBeamSelector
    {
        /// <summary>Selects the preliminary machine beam for a rated capacity and span; no structural check.</summary>
        public MachineBeamSelection Select(double capacityKg, double spanMm)
        {
            if (Double.IsNaN(capacityKg)
                || Double.IsInfinity(capacityKg)
                || capacityKg <= 0
                || Double.IsNaN(spanMm)
                || Double.IsInfinity(spanMm)
                || spanMm <= 0)
                throw new ArgumentException("Capacity and measured support span must be finite and positive.");
            double depth = capacityKg < 480 ? (spanMm < 2200 ? 140 : 160) : (capacityKg <= 800 ? 160 : (capacityKg <= 1500 ? 200 : 240));
            return new MachineBeamSelection
            {
                CapacityKg = capacityKg,
                SpanMm = spanMm,
                NominalDepthMm = depth,
                SectionDesignation = "UPN " + depth,
                StructuralStatus = capacityKg < 480 && spanMm >= 2200 ? "CALCULATION REQUIRED" : "PRELIMINARY"
            };
        }
    }
}
