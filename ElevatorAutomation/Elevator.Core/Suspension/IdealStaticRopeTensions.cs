using System;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Suspension
{
    // Ideal rope tension equation only. Ratio 2 supports the independent DOCX fixture;
    // this does not implement a 2:1 anchorage topology or authorize its live drawings.
    public static class IdealStaticRopeTensions
    {
        public static StaticRopeTensions Calculate(double carSideMassKg, double cwSideMassKg, int ratio, int ropes, double gravity)
        {
            if (!ChassisReactionSolver.Finite(carSideMassKg)
                || !ChassisReactionSolver.Finite(cwSideMassKg)
                || !ChassisReactionSolver.Finite(gravity)
                || carSideMassKg <= 0
                || cwSideMassKg <= 0
                || gravity <= 0
                || ropes <= 0
                || (ratio != 1 && ratio != 2))
                throw new ArgumentException("Positive finite masses/gravity, rope count and explicit ratio 1 or 2 required.");
            double tc = carSideMassKg * gravity / ratio, tw = cwSideMassKg * gravity / ratio;
            if (!ChassisReactionSolver.Finite(tc) || !ChassisReactionSolver.Finite(tw))
                throw new ArgumentException("Rope force overflow.");
            return new StaticRopeTensions
            {
                CarBundleN = tc,
                CounterweightBundleN = tw,
                CarPerRopeN = tc / ropes,
                CounterweightPerRopeN = tw / ropes
            };
        }
    }
}
