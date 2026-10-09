using System.Collections.Generic;
using System.Linq;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.Calculations
{
    /// <summary>P1–P4 in Basic mode: the total mass acts at the traction-sheave centre and is shared over the four bearings.</summary>
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

        /// <summary>True when the result is a Basic result for the current inputs and geometry.</summary>
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

        /// <summary>Calculates P1–P4 in Basic mode.</summary>
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
            var inputs = CollectMasses(p, c, r);
            if (r.Issues.Count > 0)
            {
                r.Status = inputs.Invalid ? "INVALID_INPUT" : "MISSING_INPUT";
                return r;
            }
            double mass = inputs.Masses.Values.Sum();
            r = SimplifiedVerticalSolver.Solve(g, mass * ChassisCalculationEngine.GravityMetresPerSecondSquared);
            r.TotalMassKg = mass;
            r.MassCompositionKg = inputs.Masses;
            r.InputFingerprint = Fingerprint(p, g);
            if (r.Reactions != null)
                AddRopeTensions(r, p, c);
            return r;
        }

        // The masses of the Basic mode with their problems; optional blank additions count as zero and are labelled "not included".
        static MassInputs CollectMasses(ElevatorProject p, ChassisCalculationInputs c, ChassisCalculationResult r)
        {
            var inputs = new MassInputs(r);
            inputs.Add("Elevator.CapacityKg", "Rated capacity", p.Elevator.CapacityKg, true);
            inputs.Add("Calculations.CarMassKg", "Empty car", c.CarMassKg, true);
            inputs.Add("Calculations.CounterweightMassKg", "Counterweight", c.CounterweightMassKg, true);
            inputs.Add("Calculations.MachineMassKg", "Machine", c.MachineMassKg, true);
            inputs.Add(
                "Calculations.CarSideAdditionalSuspendedMassKg",
                "Effective car-side additions / compensation",
                c.CarSideAdditionalSuspendedMassKg,
                false);
            inputs.Add(
                "Calculations.CounterweightSideAdditionalSuspendedMassKg",
                "Effective CW-side additions / compensation",
                c.CounterweightSideAdditionalSuspendedMassKg,
                false);
            if (c.IncludeChassisSelfWeight == true)
                inputs.Add("Calculations.ChassisMassKg", "Chassis assembly", c.ChassisMassKg, true);
            else
                inputs.Masses["Chassis assembly (not included)"] = 0;
            if (c.IncludeHangerAssemblyWeight == true)
                inputs.Add("Calculations.HangerAssemblyMassKg", "Hanger assembly", c.HangerAssemblyMassKg, true);
            else
                inputs.Masses["Hanger assembly (not included)"] = 0;
            if (!p.Suspension.RopingRatio.HasValue || p.Suspension.RopingRatio.Value != 1)
            {
                inputs.Invalid = true;
                r.Issues.Add(new CalculationIssue("Suspension.RopingRatio", "Only the supported explicit static 1:1 case is implemented."));
            }
            return inputs;
        }

        static void AddRopeTensions(ChassisCalculationResult r, ElevatorProject p, ChassisCalculationInputs c)
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

        // Collects the component masses and the problems found in them.
        sealed class MassInputs
        {
            readonly ChassisCalculationResult result;

            public MassInputs(ChassisCalculationResult result)
            {
                this.result = result;
            }

            public readonly Dictionary<string, double> Masses = new Dictionary<string, double>();
            public bool Invalid;

            public void Add(string field, string label, double? value, bool required)
            {
                if (!value.HasValue)
                {
                    if (required)
                        result.Issues.Add(new CalculationIssue(field, "NOT PROVIDED; mass in kg required."));
                    else
                        Masses[label + " (not included)"] = 0;
                    return;
                }
                if (!ChassisReactionSolver.Finite(value.Value) || (required ? value.Value <= 0 : value.Value < 0))
                {
                    Invalid = true;
                    result.Issues.Add(new CalculationIssue(field, "Finite " + (required ? "positive" : "nonnegative") + " mass required."));
                    return;
                }
                Masses[label] = value.Value;
            }
        }
    }
}
