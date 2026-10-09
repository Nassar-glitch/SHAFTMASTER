using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Calculations
{
    public static class SimplifiedVerticalSolver
    {
        public const string Model = "TwoStageVerticalMachineDatum-v1";

        static void Require(bool ok, string message)
        {
            if (!ok)
                throw new ArgumentException(message);
        }

        public static ChassisCalculationResult Solve(ChassisCalculationGeometry g, double force)
        {
            var r = new ChassisCalculationResult { Mode = "Basic", ModelVersion = Model };
            try
            {
                var seats = ValidateBearings(g, force);
                var point = g.TractionCentre;
                Require(ChassisReactionSolver.Finite(point), "Native traction datum unavailable.");
                var d = DistributeLoad(seats, point, force);
                r.SimplifiedGeometry = d;
                r.TotalVerticalLoadN = force;
                r.Reactions = BearingReactions(seats, d);
                r.Loads.Add(new LoadNode
                {
                    Id = "SimplifiedVerticalResultant",
                    PositionMm = point,
                    ForceN = new LocalPoint(0, 0, -force),
                    AssumedLocation = true,
                    Source = "Native traction-sheave centre used as the user-accepted assumed machine load reference, not measured CG."
                });
                CheckEquilibrium(r, d, point, force);
                r.Status = r.Reactions.Any(v => v.UpwardN < -force * 1e-9) ? "UPLIFT_REVIEW_REQUIRED" : "CALCULATED_UNDER_ASSUMPTIONS";
                if (r.Status == "UPLIFT_REVIEW_REQUIRED")
                {
                    r.Issues.Add(new CalculationIssue(
                        "Bearings",
                        "Negative reactions retained; compression-only bearing seats require uplift/restraint review."));
                }
                AddAssumptions(r);
            }
            catch (ArgumentException e)
            {
                r.Status = "INVALID_GEOMETRY_OR_LOAD";
                r.Reactions = null;
                r.SimplifiedGeometry = null;
                r.Issues.Add(new CalculationIssue("Basic.Geometry", e.Message));
            }
            return r;
        }

        // The four contact bearings in the order P1 right/rear, P2 right/front, P3 left/rear, P4 left/front.
        static BearingSeat[] ValidateBearings(ChassisCalculationGeometry g, double force)
        {
            Require(
                g != null && g.Units == "mm" && ChassisReactionSolver.Finite(force) && force > 0,
                "Finite positive vertical load and mm geometry required.");
            Require(
                g.Bearings != null && g.Bearings.Count == 4 && g.Bearings.Select(b => b.Id).Distinct().Count() == 4,
                "Four unique bearing contact regions required.");
            Func<string, BearingSeat> seat = id => g.Bearings.SingleOrDefault(b => b.Id == id);
            var p1 = seat("P1");
            var p2 = seat("P2");
            var p3 = seat("P3");
            var p4 = seat("P4");
            Require(
                p1 != null && p2 != null && p3 != null && p4 != null,
                "Required orientation: P1 right/rear, P2 right/front, P3 left/rear, P4 left/front.");
            foreach (var b in g.Bearings)
            {
                Require(ChassisReactionSolver.Finite(b.Centre), "Nonfinite bearing: " + b.Id);
                var contact = ChassisGeometry.ContactCentroid(b.ContactBoundary);
                Require(Math.Abs(contact.X - b.Centre.X) < .02
                    && Math.Abs(contact.Y - b.Centre.Y) < .02
                    && Math.Abs(contact.Z - b.Centre.Z) < .02, "Bearing centre must be the contact-area centroid: " + b.Id);
                Require(Math.Abs(b.Centre.Z - g.BearingPlaneZMm) < .02, "Noncoplanar bearing elevations.");
            }
            // Numerical equality only: this recipe cannot silently replace skew rows by averages.
            Require(
                Math.Abs(p2.Centre.Y - p4.Centre.Y) < 1e-7 && Math.Abs(p1.Centre.Y - p3.Centre.Y) < 1e-7,
                "Incompatible transverse bearing rows; Basic requires aligned front P4/P2 and rear P3/P1 rows.");
            return new[] { p1, p2, p3, p4 };
        }

        // Two-stage sharing of the load: front/rear by the longitudinal lever arms.
        static SimplifiedDistances DistributeLoad(BearingSeat[] seats, LocalPoint point, double force)
        {
            BearingSeat p1 = seats[0], p2 = seats[1], p3 = seats[2], p4 = seats[3];
            var d = new SimplifiedDistances
            {
                LoadReference = point,
                FrontRowY = p4.Centre.Y,
                RearRowY = p3.Centre.Y,
                FrontTransverseSpanMm = p2.Centre.X - p4.Centre.X,
                RearTransverseSpanMm = p1.Centre.X - p3.Centre.X
            };
            d.LongitudinalSpanMm = d.RearRowY - d.FrontRowY;
            Require(
                d.LongitudinalSpanMm > 0 && d.FrontTransverseSpanMm > 0 && d.RearTransverseSpanMm > 0,
                "Invalid P orientation or nonpositive bearing spans.");
            d.LoadFromFrontMm = point.Y - d.FrontRowY;
            d.LoadToRearMm = d.RearRowY - point.Y;
            d.FrontLoadFromLeftMm = point.X - p4.Centre.X;
            d.FrontLoadToRightMm = p2.Centre.X - point.X;
            d.RearLoadFromLeftMm = point.X - p3.Centre.X;
            d.RearLoadToRightMm = p1.Centre.X - point.X;
            d.FrontLoadN = force * d.LoadToRearMm / d.LongitudinalSpanMm;
            d.RearLoadN = force * d.LoadFromFrontMm / d.LongitudinalSpanMm;
            return d;
        }

        // Front P4/P2 and rear P3/P1 shares by the transverse lever arms at the same X station.
        static List<BearingReaction> BearingReactions(BearingSeat[] seats, SimplifiedDistances d)
        {
            BearingSeat p1 = seats[0], p2 = seats[1], p3 = seats[2], p4 = seats[3];
            return new List<BearingReaction>
            {
                new BearingReaction
                {
                    Id = "P1",
                    PositionMm = p1.Centre,
                    UpwardN = d.RearLoadN * d.RearLoadFromLeftMm / d.RearTransverseSpanMm
                },
                new BearingReaction
                {
                    Id = "P2",
                    PositionMm = p2.Centre,
                    UpwardN = d.FrontLoadN * d.FrontLoadFromLeftMm / d.FrontTransverseSpanMm
                },
                new BearingReaction
                {
                    Id = "P3",
                    PositionMm = p3.Centre,
                    UpwardN = d.RearLoadN * d.RearLoadToRightMm / d.RearTransverseSpanMm
                },
                new BearingReaction
                {
                    Id = "P4",
                    PositionMm = p4.Centre,
                    UpwardN = d.FrontLoadN * d.FrontLoadToRightMm / d.FrontTransverseSpanMm
                }
            };
        }

        static void CheckEquilibrium(ChassisCalculationResult r, SimplifiedDistances d, LocalPoint point, double force)
        {
            r.VerticalResidualN = r.Reactions.Sum(v => v.UpwardN) - force;
            r.MomentXResidualNmm = r.Reactions.Sum(v => v.PositionMm.Y * v.UpwardN) - force * point.Y;
            r.MomentYResidualNmm = force * point.X - r.Reactions.Sum(v => v.PositionMm.X * v.UpwardN);
            Require(r.Reactions.All(v => ChassisReactionSolver.Finite(v.UpwardN))
                && Math.Abs(r.VerticalResidualN.Value) < force * 1e-8
                && Math.Abs(r.MomentXResidualNmm.Value) < force * Math.Max(1, d.LongitudinalSpanMm) * 1e-8
                && Math.Abs(r.MomentYResidualNmm.Value) < force * Math.Max(1, d.FrontTransverseSpanMm) * 1e-8,
                    "Numerical overflow or force/moment equilibrium failure.");
        }

        static void AddAssumptions(ChassisCalculationResult r)
        {
            r.Assumptions.Add("Simplified static vertical distribution at the assumed machine load reference.");
            r.Assumptions.Add(
                "Total vertical load applied once at native traction-sheave centre. Not a measured machine CG. No separate rope/sheave "
                + "actions or assembly CGs added.");
            r.Assumptions.Add(
                "Declared two-stage sharing: front/rear shares by longitudinal lever arms, then front P4/P2 and rear P3/P1 by actual "
                + "transverse lever arms at the same X station. This additional sharing assumption makes four reactions determinate; "
                + "it is not a stiffness or structural verification.");
        }
    }
}
