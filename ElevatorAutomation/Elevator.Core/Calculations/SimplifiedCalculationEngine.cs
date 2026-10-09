using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.Calculations
{
    public sealed class SimplifiedCalculationEngine
    {
        static string Fingerprint(ElevatorProject p, ChassisCalculationGeometry g)
        {
            var c = p.Calculations;
            return CalculationFingerprint.Hash(new
            {
                Version = SimplifiedVerticalSolver.Model,
                Mode = CalculationModes.Resolve(c),
                p.Elevator.CapacityKg,
                c.CarMassKg,
                c.CounterweightMassKg,
                c.MachineMassKg,
                c.CarSideAdditionalSuspendedMassKg,
                c.CounterweightSideAdditionalSuspendedMassKg,
                c.IncludeChassisSelfWeight,
                ChassisMass = c.IncludeChassisSelfWeight == true ? c.ChassisMassKg : null,
                c.IncludeHangerAssemblyWeight,
                HangerMass = c.IncludeHangerAssemblyWeight == true ? c.HangerAssemblyMassKg : null,
                RopingRatio = p.Suspension.RopingRatio,
                p.Suspension.RopeCount,
                Geometry = g
            });
        }

        public static bool IsCurrent(ChassisCalculationResult r, ElevatorProject p, ChassisCalculationGeometry g, DesignContext ctx)
        {
            return p != null
                && p.Calculations != null
                && r != null
                && r.Mode == "Basic"
                && r.ModelVersion == SimplifiedVerticalSolver.Model
                && CalculationModes.Resolve(p.Calculations) == "Basic"
                && r.Reactions != null
                && ChassisGeometryProvenance.Validate(p, g, ctx).IsValid
                && r.InputFingerprint == Fingerprint(p, g);
        }

        public ChassisCalculationResult Calculate(ElevatorProject p, ChassisCalculationGeometry g, DesignContext ctx)
        {
            var r = new ChassisCalculationResult { Mode = "Basic", ModelVersion = SimplifiedVerticalSolver.Model };
            var valid = ChassisGeometryProvenance.Validate(p, g, ctx);
            if (!valid.IsValid)
            {
                r.Status = valid.Status;
                r.Issues.AddRange(valid.Issues);
                return r;
            }
            if (p.Calculations == null)
            {
                r.Issues.Add(new CalculationIssue("Calculations", "Component masses are not provided."));
                return r;
            }
            var c = p.Calculations;
            if (CalculationModes.Resolve(c) != "Basic")
            {
                r.Status = "INVALID_INPUT";
                r.Issues.Add(new CalculationIssue(
                    "Calculations.Mode",
                    "Select Basic explicitly; populated legacy Advanced projects are not converted automatically."));
                return r;
            }
            bool invalid = false;
            var masses = new Dictionary<string, double>();
            Action<string, string, double?, bool> add =
                (field, label, value, required) =>
                {
                    if (!value.HasValue)
                    {
                        if (required)
                            r.Issues.Add(new CalculationIssue(
                field,
                "NOT PROVIDED; mass in kg required."));
                        else
                            masses[label + " (not included)"] =
                    0;
                        return;
                    }
                    if (!ChassisReactionSolver.Finite(
                value.Value) || (required ? value.Value <= 0 : value.Value < 0))
                    {
                        invalid = true;
                        r.Issues.Add(new CalculationIssue(
                    field,
                    "Finite " + (required ? "positive" : "nonnegative") + " mass required."));
                        return;
                    }
                    masses[label] = value.Value;
                };
            add("Elevator.CapacityKg", "Rated capacity", p.Elevator.CapacityKg, true);
            add("Calculations.CarMassKg", "Empty car", c.CarMassKg, true);
            add("Calculations.CounterweightMassKg", "Counterweight", c.CounterweightMassKg, true);
            add("Calculations.MachineMassKg", "Machine", c.MachineMassKg, true);
            add(
                "Calculations.CarSideAdditionalSuspendedMassKg",
                "Effective car-side additions / compensation",
                c.CarSideAdditionalSuspendedMassKg,
                false);
            add(
                "Calculations.CounterweightSideAdditionalSuspendedMassKg",
                "Effective CW-side additions / compensation",
                c.CounterweightSideAdditionalSuspendedMassKg,
                false);
            if (c.IncludeChassisSelfWeight == true)
                add("Calculations.ChassisMassKg", "Chassis assembly", c.ChassisMassKg, true);
            else
                masses["Chassis assembly (not included)"] = 0;
            if (c.IncludeHangerAssemblyWeight == true)
                add("Calculations.HangerAssemblyMassKg", "Hanger assembly", c.HangerAssemblyMassKg, true);
            else
                masses["Hanger assembly (not included)"] = 0;
            if (!p.Suspension.RopingRatio.HasValue || p.Suspension.RopingRatio.Value != 1)
            {
                invalid = true;
                r.Issues.Add(new CalculationIssue("Suspension.RopingRatio", "Only the supported explicit static 1:1 case is implemented."));
            }
            if (r.Issues.Count > 0)
            {
                r.Status = invalid ? "INVALID_INPUT" : "MISSING_INPUT";
                return r;
            }
            double mass = masses.Values.Sum();
            r = SimplifiedVerticalSolver.Solve(g, mass * ChassisCalculationEngine.GravityMetresPerSecondSquared);
            r.TotalMassKg = mass;
            r.MassCompositionKg = masses;
            r.InputFingerprint = Fingerprint(p, g);
            if (r.Reactions != null)
            {
                r.CarBundleTensionN = (p.Elevator.CapacityKg.Value + c.CarMassKg.Value + (c.CarSideAdditionalSuspendedMassKg ?? 0)) * 9.81;
                r.CounterweightBundleTensionN = (c.CounterweightMassKg.Value + (c.CounterweightSideAdditionalSuspendedMassKg ?? 0)) * 9.81;
                if (p.Suspension.RopeCount.HasValue && p.Suspension.RopeCount.Value > 0)
                {
                    r.CarPerRopeTensionN = r.CarBundleTensionN / p.Suspension.RopeCount.Value;
                    r.CounterweightPerRopeTensionN = r.CounterweightBundleTensionN / p.Suspension.RopeCount.Value;
                }
                r.Assumptions.Add(
                    "Optional blank side additions are not included; supplied effective masses are counted once, including compensation "
                    + "assigned to that side. Optional chassis/hanger mass is counted only when included; no automatic member weights "
                    + "without full properties/lengths. Rope tensions are separate informational results and are not added to the "
                    + "resultant again.");
            }
            return r;
        }
    }
}
