using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public static class ChassisReactionSolver
    {
        public const string Model = "ReleasedTwoStageWithRigidSpreader-v1";

        public static bool Finite(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v);
        }

        public static bool Finite(LocalPoint p)
        {
            return Finite(p.X) && Finite(p.Y) && Finite(p.Z);
        }

        public static LocalPoint Add(LocalPoint a, LocalPoint b)
        {
            return new LocalPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static LocalPoint Sub(LocalPoint a, LocalPoint b)
        {
            return new LocalPoint(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static LocalPoint Scale(LocalPoint a, double s)
        {
            return new LocalPoint(a.X * s, a.Y * s, a.Z * s);
        }

        public static LocalPoint Cross(LocalPoint a, LocalPoint b)
        {
            return new LocalPoint(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        static void Require(bool b, string message)
        {
            if (!b)
                throw new ArgumentException(message);
        }

        // In addition to released member ends: ideal rigid transverse spreaders apply nodal
        // force/couple via work-conjugate influence functions. This fixes the otherwise
        // indeterminate fourth reaction. It is NOT a welded-grillage stiffness solution.
        public static ChassisCalculationResult Solve(
            ChassisCalculationGeometry g,
            IEnumerable<LoadNode> loads,
            string leftId,
            string rightId)
        {
            var r = new ChassisCalculationResult();
            try
            {
                Require(g != null && g.Units == "mm", "Geometry must explicitly use mm.");
                Require(g.Issues != null && g.Issues.Count == 0, "Unresolved geometry evidence.");
                Require(
                    g.Bearings != null && g.Bearings.Count == 4 && g.Bearings.Select(b => b.Id).Distinct().Count() == 4,
                    "Four uniquely named contact bearings required.");
                var p1 = g.Bearings.SingleOrDefault(b => b.Id == "P1");
                var p2 = g.Bearings.SingleOrDefault(b => b.Id == "P2");
                var p3 = g.Bearings.SingleOrDefault(b => b.Id == "P3");
                var p4 = g.Bearings.SingleOrDefault(b => b.Id == "P4");
                Require(
                    p1 != null && p2 != null && p3 != null && p4 != null,
                    "P1 right/rear, P2 right/front, P3 left/rear, P4 left/front required.");
                foreach (var b in g.Bearings)
                {
                    Require(Finite(b.Centre), "Nonfinite bearing.");
                    Require(Sub(b.Centre, ChassisGeometry.ContactCentroid(b.ContactBoundary)).X * Sub(
                        b.Centre,
                        ChassisGeometry.ContactCentroid(b.ContactBoundary)).X + Sub(b.Centre, ChassisGeometry.ContactCentroid(
                        b.ContactBoundary)).Y * Sub(b.Centre, ChassisGeometry.ContactCentroid(b.ContactBoundary)).Y < .0004,
                            "Bearing centre differs from contact-region centroid: " + b.Id);
                    Require(Math.Abs(b.Centre.Z - g.BearingPlaneZMm) < .02, "Bearing elevations are not coplanar.");
                }
                Require(
                    Math.Abs(p1.Centre.Y - p3.Centre.Y) < .02 && Math.Abs(p2.Centre.Y - p4.Centre.Y) < .02,
                    "Front/rear bearing pairs must have common transverse rows.");
                double yf = p4.Centre.Y, yr = p3.Centre.Y, span = yr - yf;
                Require(span > 0 && p1.Centre.X > p3.Centre.X && p2.Centre.X > p4.Centre.X, "Invalid bearing orientation/span.");
                var left = g.LongitudinalMembers.SingleOrDefault(m => m.Id == leftId);
                var right = g.LongitudinalMembers.SingleOrDefault(m => m.Id == rightId);
                Require(left != null && right != null && left != right, "Two distinct explicitly selected carrying members required.");
                foreach (var m in new[] { left, right })
                {
                    Require(Finite(m.Front) && Finite(m.Rear), "Nonfinite member geometry.");
                    Require(
                        Math.Abs(m.Front.Y - yf) < .02 && Math.Abs(m.Rear.Y - yr) < .02,
                        "Member transfer stations differ from contact bearing rows.");
                }
                Require(
                    right.Front.X > left.Front.X && right.Rear.X > left.Rear.X,
                    "Selected members cross or have zero transverse spacing.");
                r.Loads = loads == null ? new List<LoadNode>() : loads.ToList();
                Require(r.Loads.Count > 0
                    && r.Loads.All(n => n != null && !String.IsNullOrWhiteSpace(n.Id))
                    && r.Loads.Select(n => n.Id).Distinct().Count() == r.Loads.Count, "Unique nonempty load nodes required.");
                r.Reactions = new[] { p1, p2, p3, p4 }.Select(b => new BearingReaction { Id = b.Id, PositionMm = b.Centre }).ToList();
                foreach (var n in r.Loads)
                {
                    Require(Finite(n.PositionMm) && Finite(n.ForceN) && Finite(n.CoupleNmm), "Nonfinite load: " + n.Id);
                    double t = (n.PositionMm.Y - yf) / span;
                    double xl = left.Front.X + (left.Rear.X - left.Front.X) * t, xr =
                        right.Front.X + (right.Rear.X - right.Front.X) * t, w = xr - xl;
                    Require(w > 0, "Selected carrying members cross at load station: " + n.Id);
                    double sl = (xr - n.PositionMm.X) / w, sr = 1 - sl;
                    double dxl = (left.Rear.X - left.Front.X) / span, dxr = (right.Rear.X - right.Front.X) / span;
                    double dslDy = (dxr * w - (xr - n.PositionMm.X) * (dxr - dxl)) / (w * w);
                    double z = n.PositionMm.Z - g.BearingPlaneZMm, d = -n.ForceN.Z;
                    // First moments required at bearing plane, including horizontal-force eccentricity.
                    double cx = z * n.ForceN.X + n.CoupleNmm.Y, cy = z * n.ForceN.Y - n.CoupleNmm.X;
                    for (int k = 0; k < 2; k++)
                    {
                        var m = k == 0 ? left : right;
                        double share = k == 0 ? sl : sr, dsdx = (k == 0 ? -1 : 1) / w, dsdy = k == 0 ? dslDy : -dslDy;
                        double f = d * share * (1 - t) + cx * dsdx * (1 - t) + cy * (dsdy * (1 - t) - share / span);
                        double b = d * share * t + cx * dsdx * t + cy * (dsdy * t + share / span);
                        r.Transfers.Add(new MemberTransfer
                        {
                            LoadId = n.Id,
                            MemberId = m.Id,
                            FrontDownwardN = f,
                            RearDownwardN = b,
                            FrontPointMm = m.Front,
                            RearPointMm = m.Rear
                        });
                        r.Reactions.Single(v => v.Id == "P4").UpwardN += f * (p2.Centre.X - m.Front.X) / (p2.Centre.X - p4.Centre.X);
                        r.Reactions.Single(v => v.Id == "P2").UpwardN += f * (m.Front.X - p4.Centre.X) / (p2.Centre.X - p4.Centre.X);
                        r.Reactions.Single(v => v.Id == "P3").UpwardN += b * (p1.Centre.X - m.Rear.X) / (p1.Centre.X - p3.Centre.X);
                        r.Reactions.Single(v => v.Id == "P1").UpwardN += b * (m.Rear.X - p3.Centre.X) / (p1.Centre.X - p3.Centre.X);
                    }
                }
                double fz = 0, mx = 0, my = 0;
                var lateral = new LocalPoint(0, 0, 0);
                double yaw = 0;
                foreach (var n in r.Loads)
                {
                    var p = Sub(n.PositionMm, new LocalPoint(0, 0, g.BearingPlaneZMm));
                    var m = Add(Cross(p, n.ForceN), n.CoupleNmm);
                    fz += n.ForceN.Z;
                    mx += m.X;
                    my += m.Y;
                    yaw += m.Z;
                    lateral = Add(lateral, new LocalPoint(n.ForceN.X, n.ForceN.Y, 0));
                }
                r.VerticalResidualN = fz + r.Reactions.Sum(v => v.UpwardN);
                r.MomentXResidualNmm = mx + r.Reactions.Sum(v => v.PositionMm.Y * v.UpwardN);
                r.MomentYResidualNmm = my - r.Reactions.Sum(v => v.PositionMm.X * v.UpwardN);
                r.UnverifiedLateralForceN = lateral;
                r.UnverifiedYawMomentNmm = yaw;
                double scale = Math.Max(1, r.Loads.Sum(n => Math.Abs(n.ForceN.Z) + Math.Abs(n.ForceN.X) + Math.Abs(n.ForceN.Y))), length =
                    Math.Max(1, span);
                Require(r.Reactions.All(v => Finite(v.UpwardN))
                    && Finite(r.VerticalResidualN.Value)
                    && Finite(r.MomentXResidualNmm.Value)
                    && Finite(r.MomentYResidualNmm.Value), "Numerical overflow.");
                Require(Math.Abs(r.VerticalResidualN.Value) < scale * 1e-8
                    && Math.Abs(r.MomentXResidualNmm.Value) < scale * length * 1e-8
                    && Math.Abs(r.MomentYResidualNmm.Value) < scale * length * 1e-8,
                        "Vertical force/overturning moment equilibrium failed.");
                r.Status = "CALCULATED_UNDER_ASSUMPTIONS";
                if (r.Reactions.Any(v => v.UpwardN < -scale * 1e-9))
                {
                    r.Status = "UPLIFT_REVIEW_REQUIRED";
                    r.Issues.Add(new CalculationIssue(
                        "Bearings",
                        "Negative reactions retained; compression-only seats cannot realize this load case without reviewed hold-down."));
                }
                if (r.Transfers
                    .GroupBy(v => v.MemberId)
                    .Any(a => a.Sum(v => v.FrontDownwardN) < -scale * 1e-9 || a.Sum(v => v.RearDownwardN) < -scale * 1e-9))
                {
                    r.Issues.Add(new CalculationIssue(
                                            "MemberTransfers",
                                            "Net tensile member transfer requires mechanical review; no connection adequacy is claimed."));
                }
                r.Assumptions.Add(
                    "Two selected upper longitudinal members, simple/released ends on front/rear transverse members; transverse members "
                    + "simply supported at actual P contact centroids.");
                r.Assumptions.Add(
                    "Ideal rigid transverse spreader at each nodal action. Share by member lever arms; moments by work-conjugate "
                    + "derivatives of the same two-stage influence functions. No equal-share default or grillage stiffness analysis.");
                r.Assumptions.Add(
                    "Bearing plane projected from B-B steel bottom; lateral/yaw restraint acts at this plane and remains unverified. "
                    + "Horizontal force height and all applied couples contribute to vertical reactions.");
                r.Assumptions.Add(
                    "Other native longitudinal profile is not automatically a third load-carrying path. Selected pair, spreading/moment "
                    + "transfer and load locations require an explicit engineering source.");
            }
            catch (ArgumentException ex)
            {
                r.Status =
                "INVALID_GEOMETRY_OR_LOAD";
                r.Reactions = null;
                r.Issues.Add(new CalculationIssue("ReactionModel", ex.Message));
            }
            return r;
        }
    }
}
