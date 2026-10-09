using System;
using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Evaluates how a chassis contour follows the shared design dimensions.</summary>
    public static class ChassisDependencies
    {
        /// <summary>The datum coordinate of a reference: front beam, rear beam, machine or counterweight.</summary>
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

        /// <summary>The target position of point i of a contour that follows its dependency.</summary>
        public static LocalPoint Target(ChassisDependency d, int i, double center, double car, double rear, double cw = Double.NaN)
        {
            var p = d.Offsets[i];
            return new LocalPoint(center + p.X, Datum(d.References[i], car, rear, cw) + p.Y, p.Z);
        }
    }
}
