using System;
using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public static class ChassisDependencies
    {
        public static double Datum(string reference, double car, double rear, double cw = Double.NaN)
        {
            switch (reference)
            {
                case "FrontBeam":
                    return 0;
                case "RearBeam":
                    return rear;
                case "Machine":
                    return car;
                case "Counterweight":
                    return cw;
                default:
                    throw new ArgumentException("Unknown chassis datum: " + reference);
            }
        }

        public static LocalPoint Target(ChassisDependency d, int i, double center, double car, double rear, double cw = Double.NaN)
        {
            var p = d.Offsets[i];
            return new LocalPoint(center + p.X, Datum(d.References[i], car, rear, cw) + p.Y, p.Z);
        }
    }
}
