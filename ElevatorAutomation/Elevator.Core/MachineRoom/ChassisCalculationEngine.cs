using System;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;
using Elevator.Core.Project;
using Elevator.Core.Suspension;

namespace Elevator.Core.MachineRoom
{
    public sealed class ChassisCalculationEngine
    {
        public const double GravityMetresPerSecondSquared = 9.81;

        static LocalPoint Add(LocalPoint a, LocalPoint b)
        {
            return ChassisReactionSolver.Add(a, b);
        }

        static LocalPoint Sub(LocalPoint a, LocalPoint b)
        {
            return ChassisReactionSolver.Sub(a, b);
        }

        static LocalPoint Scale(LocalPoint a, double s)
        {
            return ChassisReactionSolver.Scale(a, s);
        }

        static LocalPoint Cross(LocalPoint a, LocalPoint b)
        {
            return ChassisReactionSolver.Cross(a, b);
        }

        static bool HasText(string s)
        {
            return !String.IsNullOrWhiteSpace(s);
        }

        public static string Fingerprint(ElevatorProject p, ChassisCalculationGeometry g)
        {
            return CalculationFingerprint.Hash(new
            {
                Version = ChassisReactionSolver.Model,
                Gravity = GravityMetresPerSecondSquared,
                Inputs = CalculationFingerprint.ChassisInputs(p == null ? null : p.Calculations),
                Capacity = p == null || p.Elevator == null ? null : p.Elevator.CapacityKg,
                Suspension = p == null ? null : p.Suspension,
                Shaft = p == null ? null : p.Shaft,
                Machine = p == null ? null : p.Machine,
                MachineRoom = p == null ? null : p.MachineRoom,
                Geometry = g
            });
        }

        public static bool IsCurrent(ChassisCalculationResult r, ElevatorProject p, ChassisCalculationGeometry g)
        {
            return IsCurrent(r, p, g, null);
        }

        public static bool IsCurrent(
            ChassisCalculationResult r,
            ElevatorProject p,
            ChassisCalculationGeometry g,
            DesignContext currentContext)
        {
            return r != null
                && (r.Status == "CALCULATED_UNDER_ASSUMPTIONS" || r.Status == "UPLIFT_REVIEW_REQUIRED")
                && r.Reactions != null
                && r.ModelVersion == ChassisReactionSolver.Model
                && r.InputFingerprint != null
                && ChassisGeometryProvenance.Validate(p, g, currentContext).IsValid
                && r.InputFingerprint == Fingerprint(p, g);
        }

        public ChassisCalculationResult Calculate(ElevatorProject p, ChassisCalculationGeometry g)
        {
            return Calculate(p, g, null);
        }

        public ChassisCalculationResult Calculate(ElevatorProject p, ChassisCalculationGeometry g, DesignContext currentContext)
        {
            var r = new ChassisCalculationResult();
            var provenance = ChassisGeometryProvenance.Validate(p, g, currentContext);
            if (!provenance.IsValid)
            {
                r.Status = provenance.Status;
                r.Issues.AddRange(provenance.Issues);
                return r;
            }
            r.InputFingerprint = Fingerprint(p, g);
            if (p == null || p.Calculations == null || p.Elevator == null || p.Suspension == null)
            {
                r.Issues.Add(new CalculationIssue("Calculations", "Canonical calculation inputs or project groups unavailable."));
                return r;
            }
            var c = p.Calculations;
            bool invalid = false;
            Action<string, double?, bool> number = (name, v, positive) =>
            {
                if (!v.HasValue)
                    r.Issues.Add(
                new CalculationIssue(name, "NOT PROVIDED (no assumed zero)."));
                else if (!ChassisReactionSolver.Finite(
                v.Value) || (positive ? v.Value <= 0 : v.Value < 0))
                {
                    invalid = true;
                    r.Issues.Add(
                    new CalculationIssue(name, "Finite " + (positive ? "positive" : "nonnegative") + " value required."));
                }
            };
            Action<string, double?> offset =
                (name, v) =>
                {
                    if (!v.HasValue || !ChassisReactionSolver.Finite(v.Value))
                    {
                        if (v.HasValue)
                            invalid =
                true;
                        r.Issues.Add(
                    new CalculationIssue(name, "Explicit finite location offset in mm required; zero may be supplied deliberately."));
                    }
                };
            Action<string, string> source = (name, v) =>
            {
                if (!HasText(v))
                    r.Issues.Add(
                new CalculationIssue(name, "Engineering source or explicitly identified editable assumption required."));
            };
            if (c.LoadCase != "StaticRatedLoad1To1")
                r.Issues.Add(new CalculationIssue(
                    "Calculations.LoadCase",
                    "Select StaticRatedLoad1To1; acceleration, emergency and alternative topology cases are not implemented."));
            if (!p.Suspension.RopingRatio.HasValue || p.Suspension.RopingRatio.Value != 1)
                r.Issues.Add(new CalculationIssue(
                    "Suspension.RopingRatio",
                    "Explicit 1:1 topology required; note 2:1 example is a separate fixture."));
            if (!p.Suspension.RopeCount.HasValue || p.Suspension.RopeCount.Value <= 0)
                r.Issues.Add(new CalculationIssue("Suspension.RopeCount", "Positive rope count required for per-rope results."));
            number("Elevator.CapacityKg", p.Elevator.CapacityKg, true);
            number("Calculations.CarMassKg", c.CarMassKg, true);
            number("Calculations.CounterweightMassKg", c.CounterweightMassKg, true);
            number("Calculations.MachineMassKg", c.MachineMassKg, true);
            number("Calculations.CarSideAdditionalSuspendedMassKg", c.CarSideAdditionalSuspendedMassKg, false);
            number("Calculations.CounterweightSideAdditionalSuspendedMassKg", c.CounterweightSideAdditionalSuspendedMassKg, false);
            offset("Calculations.MachineCgOffsetXMm", c.MachineCgOffsetXMm);
            offset("Calculations.MachineCgOffsetYMm", c.MachineCgOffsetYMm);
            offset("Calculations.MachineCgOffsetZMm", c.MachineCgOffsetZMm);
            source("Calculations.MachineCgSource", c.MachineCgSource);
            source("Calculations.LeftCarryingMemberId", c.LeftCarryingMemberId);
            source("Calculations.RightCarryingMemberId", c.RightCarryingMemberId);
            source("Calculations.TransferAssumptionSource", c.TransferAssumptionSource);
            if (c.TransferModel != ChassisReactionSolver.Model)
                r.Issues.Add(new CalculationIssue(
                    "Calculations.TransferModel",
                    "Explicit selection of " + ChassisReactionSolver.Model + " required."));
            if (g == null)
                r.Issues.Add(new CalculationIssue("Geometry", "Calculated native geometry unavailable."));
            else
            {
                r.Issues.AddRange(g.Issues);
                if (g.Section == null
                    || !g.Section.HasCalculatedPath
                    || g.Section.Arrangement == null
                    || g.Section.Arrangement.Reeving == null
                    || g.Section.Arrangement.Reeving.GeometryStatus != "PASS")
                    r.Issues.Add(new CalculationIssue(
                        "Geometry.Reeving",
                        "A supported calculated 1:1 deflector path is required; direct routing has no verified force-path model in this "
                        + "phase."));
                if (g.Section != null && g.Section.DeflectorVisible)
                {
                    offset("Calculations.HangerAttachmentOffsetXMm", c.HangerAttachmentOffsetXMm);
                    offset("Calculations.HangerAttachmentOffsetYMm", c.HangerAttachmentOffsetYMm);
                    source("Calculations.HangerAttachmentSource", c.HangerAttachmentSource);
                }
            }
            if (!c.IncludeChassisSelfWeight.HasValue)
                r.Issues.Add(
                    new CalculationIssue("Calculations.IncludeChassisSelfWeight", "Explicit inclusion or documented exclusion required."));
            source("Calculations.ChassisWeightSourceOrExclusionReason", c.ChassisWeightSourceOrExclusionReason);
            if (c.IncludeChassisSelfWeight == true)
            {
                number("Calculations.ChassisMassKg", c.ChassisMassKg, true);
                offset("Calculations.ChassisCgOffsetXMm", c.ChassisCgOffsetXMm);
                offset("Calculations.ChassisCgOffsetYMm", c.ChassisCgOffsetYMm);
                offset("Calculations.ChassisCgOffsetZMm", c.ChassisCgOffsetZMm);
            }
            if (!c.IncludeHangerAssemblyWeight.HasValue)
                r.Issues.Add(new CalculationIssue(
                    "Calculations.IncludeHangerAssemblyWeight",
                    "Explicit inclusion or documented exclusion required."));
            source("Calculations.HangerWeightSourceOrExclusionReason", c.HangerWeightSourceOrExclusionReason);
            if (c.IncludeHangerAssemblyWeight == true)
            {
                number("Calculations.HangerAssemblyMassKg", c.HangerAssemblyMassKg, true);
                offset("Calculations.HangerCgOffsetXMm", c.HangerCgOffsetXMm);
                offset("Calculations.HangerCgOffsetYMm", c.HangerCgOffsetYMm);
                offset("Calculations.HangerCgOffsetZMm", c.HangerCgOffsetZMm);
            }
            if (r.Issues.Count > 0)
            {
                if (invalid)
                    r.Status = "INVALID_INPUT";
                return r;
            }
            if (p.Shaft == null
                || Math.Abs(p.Shaft.WidthMm - g.ShaftWidthMm) > .02
                || Math.Abs(p.Shaft.DepthMm - g.ShaftDepthMm) > .02
                || !ChassisReactionSolver.Finite(p.Shaft.WidthMm)
                || !ChassisReactionSolver.Finite(p.Shaft.DepthMm)
                || (g.Section.Beam != null && Math.Abs(g.Section.Beam.CapacityKg - p.Elevator.CapacityKg.Value) > .02))
            {
                r.Status = "STALE_GEOMETRY";
                r.Issues.Add(new CalculationIssue(
                    "Geometry",
                    "Shaft/capacity differs from calculated drawing geometry; recalculate shared design first."));
                return r;
            }
            var geometryCheck = ChassisReactionSolver.Solve(
                g,
                new[] { new LoadNode { Id = "GeometryValidation", PositionMm = g.TractionCentre, ForceN = new LocalPoint(0, 0, -1) } },
                c.LeftCarryingMemberId,
                c.RightCarryingMemberId);
            if (geometryCheck.Reactions == null)
            {
                geometryCheck.Loads.Clear();
                geometryCheck.InputFingerprint = r.InputFingerprint;
                return geometryCheck;
            }
            StaticRopeTensions tensions;
            try
            {
                tensions = IdealStaticRopeTensions.Calculate(
                    p.Elevator.CapacityKg.Value + c.CarMassKg.Value + c.CarSideAdditionalSuspendedMassKg.Value,
                    c.CounterweightMassKg.Value + c.CounterweightSideAdditionalSuspendedMassKg.Value,
                    1,
                    p.Suspension.RopeCount.Value,
                    GravityMetresPerSecondSquared);
            }
            catch (ArgumentException e)
            {
                r.Status =
                "INVALID_INPUT";
                r.Issues.Add(new CalculationIssue("SuspendedMasses", e.Message));
                return r;
            }
            double tc = tensions.CarBundleN, tw = tensions.CounterweightBundleN;
            var s = g.Section;
            var reeving = s.Arrangement.Reeving;
            var a = new LocalPoint(g.TractionCentre.X, -reeving.TractionTangentPoint.X, reeving.TractionTangentPoint.Y);
            var b = new LocalPoint(g.DeflectorCentre.X, -reeving.DeflectorTangentPoint.X, reeving.DeflectorTangentPoint.Y);
            var delta = Sub(b, a);
            double len = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
            if (len <= 0 || !ChassisReactionSolver.Finite(len))
            {
                r.Status = "INVALID_GEOMETRY_OR_LOAD";
                r.Issues.Add(new CalculationIssue("Geometry.Tangent", "Invalid tangent segment."));
                return r;
            }
            var tangent = Scale(delta, 1 / len);
            var down = new LocalPoint(0, 0, -1);
            var carContact = new LocalPoint(g.TractionCentre.X, -s.CarDropX, g.TractionCentre.Z);
            var cwContact = new LocalPoint(g.DeflectorCentre.X, -s.CwDropX, g.DeflectorCentre.Z);
            var tf = Add(Scale(down, tc), Scale(tangent, tw));
            var df = Add(Scale(tangent, -tw), Scale(down, tw));
            var tm = Add(Cross(Sub(carContact, g.TractionCentre), Scale(down, tc)), Cross(Sub(a, g.TractionCentre), Scale(tangent, tw)));
            var dm = Add(Cross(Sub(b, g.DeflectorCentre), Scale(tangent, -tw)), Cross(Sub(cwContact, g.DeflectorCentre), Scale(down, tw)));
            var hanger = new LocalPoint(
                g.DeflectorCentre.X + c.HangerAttachmentOffsetXMm.Value,
                g.DeflectorCentre.Y + c.HangerAttachmentOffsetYMm.Value,
                g.HangerAttachmentZMm);
            r.Loads.Add(Weight(
                "MachineWeight",
                Add(g.TractionCentre, new LocalPoint(c.MachineCgOffsetXMm.Value, c.MachineCgOffsetYMm.Value, c.MachineCgOffsetZMm.Value)),
                c.MachineMassKg.Value,
                c.MachineCgSource));
            r.Loads.Add(new LoadNode
            {
                Id = "TractionRopeAction",
                Kind = "rope",
                PositionMm = g.TractionCentre,
                ForceN = tf,
                CoupleNmm = tm,
                Source = "1:1 static rated case; tangent and car-drop forces reduced to pitch centre with preserved couple"
            });
            r.Loads.Add(new LoadNode
            {
                Id = "DeflectorHangerAction",
                Kind = "rope-transfer",
                PositionMm = hanger,
                ForceN = df,
                CoupleNmm = Add(dm, Cross(Sub(g.DeflectorCentre, hanger), df)),
                AssumedLocation = true,
                Source = c.HangerAttachmentSource + "; full force/couple shifted from deflector centre"
            });
            if (c.IncludeChassisSelfWeight == true)
                r.Loads.Add(Weight("ChassisWeight", Add(
                    new LocalPoint(
                        g.TractionCentre.X,
                        (g.Bearings.Single(v => v.Id == "P4").Centre.Y + g.Bearings.Single(v => v.Id == "P3").Centre.Y) / 2,
                        g.BearingPlaneZMm),
                    new LocalPoint(c.ChassisCgOffsetXMm.Value, c.ChassisCgOffsetYMm.Value, c.ChassisCgOffsetZMm.Value)),
                        c.ChassisMassKg.Value,
                        c.ChassisWeightSourceOrExclusionReason));
            if (c.IncludeHangerAssemblyWeight == true)
            {
                var weight = Weight(
                    "HangerAssemblyWeight",
                    Add(g.DeflectorCentre, new LocalPoint(c.HangerCgOffsetXMm.Value, c.HangerCgOffsetYMm.Value, c.HangerCgOffsetZMm.Value)),
                    c.HangerAssemblyMassKg.Value,
                    c.HangerWeightSourceOrExclusionReason);
                weight.CoupleNmm = Cross(Sub(weight.PositionMm, hanger), weight.ForceN);
                weight.PositionMm = hanger;
                r.Loads.Add(weight);
            }
            var solved = ChassisReactionSolver.Solve(g, r.Loads, c.LeftCarryingMemberId, c.RightCarryingMemberId);
            solved.InputFingerprint = r.InputFingerprint;
            solved.CarBundleTensionN = tc;
            solved.CounterweightBundleTensionN = tw;
            solved.CarPerRopeTensionN = tc / p.Suspension.RopeCount.Value;
            solved.CounterweightPerRopeTensionN = tw / p.Suspension.RopeCount.Value;
            solved.Assumptions.Add(
                "Static rated load, g=9.81 m/s2, equal tension per rope, ideal frictionless deflector. Effective additional suspended "
                + "masses explicitly assigned to each drop; no separate car/CW weight applied again at chassis.");
            solved.Assumptions.Add("Machine CG source: "
                + c.MachineCgSource
                + "; transfer source: "
                + c.TransferAssumptionSource
                + "; hanger source: "
                + c.HangerAttachmentSource);
            solved.Assumptions.Add("Chassis weight: "
                + c.ChassisWeightSourceOrExclusionReason
                + "; hanger assembly weight: "
                + c.HangerWeightSourceOrExclusionReason);
            if (c.IncludeChassisSelfWeight == true)
                solved.Assumptions.Add(
                    "Chassis self-weight lumped at declared total CG through the same ideal upper-member spreading path; member-by-member "
                    + "dead-load distribution is not represented and requires engineering acceptance.");
            return solved;
        }

        static LoadNode Weight(string id, LocalPoint point, double mass, string source)
        {
            return new LoadNode
            {
                Id = id,
                Kind = "gravity",
                PositionMm = point,
                ForceN = new LocalPoint(0, 0, -mass * GravityMetresPerSecondSquared),
                Source = source,
                AssumedLocation = true
            };
        }
    }
}
