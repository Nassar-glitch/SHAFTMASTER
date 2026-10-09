using System;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.Core.MachineRoom
{
    public sealed class MachineRoomSectionGeometry
    {
        public readonly MachineRoomSectionParameters P;
        public readonly ElevatorParameters Design;

        private SectionReference R
        {
            get
            {
                return P.Reference;
            }
        }

        public MachineRoomSectionGeometry(ElevatorParameters design)
        {
            Design = design;
            P = design.MachineRoomSection;
        }

        private double SupportX(double x)
        {
            return x + (x < -R.SourceSD / 2 ? P.RearSupportX - R.SourceRearSupportX : P.FrontSupportX - R.SourceFrontSupportX);
        }

        private double SlabX(double x)
        {
            double[] old = { -R.SourceCarRow - R.CarOpeningWidth / 2,
                -R.SourceCarRow + R.CarOpeningWidth / 2,
                -R.SourceCwRow - R.CwOpeningWidth / 2,
                -R.SourceCwRow + R.CwOpeningWidth / 2 };
            double[] next = { P.CarDropX - P.CarOpeningWidth / 2,
                P.CarDropX + P.CarOpeningWidth / 2,
                P.CwDropX - P.CwOpeningWidth / 2,
                P.CwDropX + P.CwOpeningWidth / 2 };
            for (int i = 0; i < old.Length; i++)
            {
                if (Math.Abs(x - old[i]) < 2)
                    return next[i];
            }
            if (Math.Abs(x + R.SourceCarRow) < 2)
                return P.CarDropX;
            if (Math.Abs(x + R.SourceCwRow) < 2)
                return P.CwDropX;
            return x + (x < -R.SourceSD / 2 ? R.SourceSD - Design.SD : 0);
        }

        public LocalPoint Map(LocalPoint p, string role)
        {
            double x = p.X, y = p.Y;
            switch (role)
            {
                case "machine":
                case "traction":
                    x += P.TractionX - R.SourceTractionX;
                    y += P.TractionZ - R.SourceTractionZ;
                    break;
                case "deflector":
                    x += P.DeflectorX - R.SourceDeflectorX;
                    y += P.DeflectorZ - R.SourceDeflectorZ;
                    break;
                case "hanger":
                    x += P.DeflectorX - R.SourceDeflectorX;
                    y = p.Y > (R.SourceDeflectorZ + R.SourceSteelTopZ) / 2
                        ? P.SteelTopZ + (p.Y - R.SourceSteelTopZ)
                        : P.DeflectorZ + (p.Y - R.SourceDeflectorZ);
                    break;
                case "stand":
                    x = SupportX(x);
                    y += P.ConcreteHeight - R.SourceStandBottomZ;
                    break;
                case "steel":
                case "steelDetail":
                    x = SupportX(x);
                    y = p.Y > (R.SourceSteelBottomZ + R.SourceSteelTopZ) / 2 ? P.SteelTopZ : P.SteelBottomZ;
                    if (role == "steelDetail")
                    {
                        y = p.Y > (R.SourceSteelBottomZ + R.SourceSteelTopZ) / 2
                                                    ? P.SteelTopZ + (p.Y - R.SourceSteelTopZ)
                                                    : P.SteelBottomZ + (p.Y - R.SourceSteelBottomZ);
                    }
                    break;
                case "concrete":
                    x += x < -R.SourceSD / 2 ? R.SourceSD - Design.SD : 0;
                    y = p.Y > R.SourceConcreteHeight / 2 ? P.ConcreteHeight : 0;
                    break;
                case "slab":
                    x = SlabX(x);
                    break;
                case "spanNote":
                    x += Math.Abs(x + R.SourceCwRow) < Math.Abs(x + R.SourceCarRow)
                        ? P.CwDropX + R.SourceCwRow
                        : P.CarDropX + R.SourceCarRow;
                    break;
                default:
                    throw new ArgumentException("Unsupported section mapping: " + role);
            }
            return new LocalPoint(x, y, p.Z);
        }

        public bool Flexible(string role)
        {
            return role == "slab"
                || role == "concrete"
                || role == "steel"
                || role == "steelDetail"
                || role == "hanger"
                || role == "spanNote";
        }
    }
}
