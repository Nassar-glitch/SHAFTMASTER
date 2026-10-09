using System;

namespace Elevator.Core
{
    public interface IMachineBeamSelector
    {
        MachineBeamSelection Select(double capacityKg, double spanMm);
    }

    public sealed class PreliminaryMachineBeamSelector : IMachineBeamSelector
    {
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

    public static class SuspensionProjectRules
    {
        public static void Validate(double? diameter, int? number)
        {
            if (diameter.HasValue && Array.IndexOf(new[] { 6.5, 8, 10, 11, 12 }, diameter.Value) < 0)
                throw new ArgumentException("Supported suspension rope diameters: 6.5, 8, 10, 11, 12 mm.");
            if (number.HasValue && (number < 4 || number > 7))
                throw new ArgumentException("Supported suspension rope counts: 4 through 7.");
        }
    }
}
