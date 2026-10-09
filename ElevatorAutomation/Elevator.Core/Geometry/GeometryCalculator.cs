using System;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Geometry
{
    public sealed class GeometryCalculator
    {
        public readonly ElevatorParameters Old;
        public readonly ElevatorParameters Next;

        public GeometryCalculator(ElevatorParameters old, ElevatorParameters next)
        {
            Old = old;
            Next = next;
        }

        public double DX(string a)
        {
            return Next.X(a) - Old.X(a);
        }

        public double DY(string a)
        {
            return Next.Y(a) - Old.Y(a);
        }

        public double MapX(double x, params string[] a)
        {
            string b = a.OrderBy(k => Math.Abs(x - Old.X(k))).First();
            return x + DX(b);
        }

        public double MapY(double y, params string[] a)
        {
            string b = a.OrderBy(k => Math.Abs(y - Old.Y(k))).First();
            return y + DY(b);
        }

        public LocalPoint Translation(string role)
        {
            switch (role)
            {
                case "carLeft":
                case "governor":
                    return new LocalPoint(DX("railLeft"), DY("carRow"));
                case "carRight":
                    return new LocalPoint(DX("railRight"), DY("carRow"));
                case "cwLeft":
                    return new LocalPoint(DX("cwRailLeft"), DY("cwRow"));
                case "cwRight":
                    return new LocalPoint(DX("cwRailRight"), DY("cwRow"));
                case "carMark":
                    return new LocalPoint(DX("center"), DY("carRow"));
                case "cwMark":
                    return new LocalPoint(DX("center"), DY("cwRow"));
                case "doorLeft":
                    return new LocalPoint(DX("doorLeft"), 0);
                case "doorRight":
                    return new LocalPoint(DX("doorRight"), 0);
                case "centerFront":
                    return new LocalPoint(DX("center"), 0);
                case "centerRear":
                    return new LocalPoint(DX("center"), DY("rear"));
                default:
                    throw new ArgumentException("No rigid translation for role " + role);
            }
        }

        public bool IsFlexible(string role)
        {
            return new[]
            {
                "wall",
                "cabin",
                "entrance",
                "cwOutline",
                "carSpan",
                "carSupportLeft",
                "carSupportRight",
                "carCL",
                "cwCL",
                "mainCL",
                "doorSpan",
                "doorLeader"
            }.Contains(role);
        }

        public LocalPoint MapPoint(LocalPoint world, string role)
        {
            LocalPoint p = world;
            double x = p.X, y = p.Y;
            switch (role)
            {
                case "wall":
                    x = MapX(x, "left", "right", "jambLeft", "jambRight");
                    y = MapY(y, "front", "rear");
                    break;
                case "cabin":
                    x = MapX(x, "carLeft", "carRight", "doorLeft", "doorRight");
                    y = MapY(y, "carFront", "carRear");
                    break;
                case "entrance":
                    x = MapX(x, "doorLeft", "doorRight");
                    y += DY("carFront");
                    break;
                case "cwOutline":
                    x = MapX(x, "cwLeft", "cwRight");
                    y += DY("cwRow");
                    break;
                case "carSpan":
                    x += DX(x < Old.Center ? "railLeft" : "railRight");
                    y += DY("carRow");
                    break;
                case "carSupportLeft":
                    x += Math.Abs(x) < 0.01 ? DX("left") : DX("railLeft");
                    y += DY("carRow");
                    break;
                case "carSupportRight":
                    x += Math.Abs(x - Old.SW) < 0.01 ? DX("right") : DX("railRight");
                    y += DY("carRow");
                    break;
                case "carCL":
                case "cwCL":
                    x = MapX(x, "left", "right");
                    y += DY(role == "carCL" ? "carRow" : "cwRow");
                    break;
                case "mainCL":
                    x += DX("center");
                    y = MapY(y, "front", "rear");
                    break;
                case "doorSpan":
                case "doorLeader":
                    x = MapX(x, "doorLeft", "center", "doorRight");
                    break;
                default:
                    return world + Translation(role);
            }
            return new LocalPoint(x, y, p.Z);
        }
    }
}
