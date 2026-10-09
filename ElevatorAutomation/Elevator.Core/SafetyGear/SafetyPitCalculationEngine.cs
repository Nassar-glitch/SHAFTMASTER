using System;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.SafetyGear
{
    public sealed class SafetyPitCalculationEngine
    {
        public const string RuleVersion = "GearboxNoteSafetyPit-v1";
        public const string SourceDocumentSha256 = "ECFD591133F5F97ED923F62A3A3EEA60C2B97A67805E60BCB7131C3DA7FB13DF";

        public const string RuleBasis =
            "Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx: Progressive Safety Gear p104–106 (equation 439730EB), Car "
            + "Buffer p108–110 (5429655E), Counterweight Buffer p112–114 (3897FF34), zero-based descendant paragraphs / Word paraIds. The "
            + "note supplies no P5–P7 standard clause or manufacturer basis. Adopted note rules, not independent code/manufacturer "
            + "certification.";

        public const double SafetyGearCoefficientNPerKg = 10.0;
        public const double BufferMinimumCoefficientNPerKg = 40.0;
        public const double BufferFactor = 4.0;
        public const double GravityMetresPerSecondSquared = 9.81;

        public static double BufferCoefficientNPerKg
        {
            get
            {
                return Math.Max(BufferMinimumCoefficientNPerKg, BufferFactor * GravityMetresPerSecondSquared);
            }
        }

        static string Fingerprint(ElevatorProject p)
        {
            if (p == null || p.Calculations == null || p.Elevator == null)
                return null;
            var c = p.Calculations;
            var s = c.SafetyPit ?? new SafetyPitInputs();
            return CalculationFingerprint.Hash(new
            {
                Rule = RuleVersion,
                p.Elevator.CapacityKg,
                c.CarMassKg,
                c.CounterweightMassKg,
                c.CarSideAdditionalSuspendedMassKg,
                c.CounterweightSideAdditionalSuspendedMassKg,
                s.SafetyGearType,
                s.CarGuideCount,
                s.CarBufferCount,
                s.CounterweightBufferCount,
                CarSharing = s.CarBufferCount > 1 ? s.CarBufferSharing : null,
                CounterweightSharing = s.CounterweightBufferCount > 1 ? s.CounterweightBufferSharing : null,
                CarAllocation = c.CarSideAdditionalSuspendedMassKg > 0 ? s.CarAdditionalMassAllocation : null,
                CounterweightAllocation = c.CounterweightSideAdditionalSuspendedMassKg > 0 ? s.CounterweightAdditionalMassAllocation : null,
                CarPercent = c.CarSideAdditionalSuspendedMassKg > 0 && s.CarAdditionalMassAllocation == "FractionParticipates"
                    ? s.CarEventAdditionalMassPercent
                    : null,
                CounterweightPercent = c.CounterweightSideAdditionalSuspendedMassKg > 0
                    && s.CounterweightAdditionalMassAllocation == "FractionParticipates" ? s.CounterweightEventAdditionalMassPercent : null
            });
        }

        public static bool IsCurrent(SafetyPitCalculationResult result, ElevatorProject project)
        {
            return result != null
                && result.RuleVersion == RuleVersion
                && result.InputFingerprint != null
                && result.InputFingerprint == Fingerprint(project);
        }

        static void Issue(SafetyPitLoad load, string field, string message, string status)
        {
            load.Issues.Add(new CalculationIssue(field, message));
            if (load.Status == "MISSING_INPUT" || status == "INVALID_INPUT" || status == "UNSUPPORTED_CONFIGURATION")
                load.Status = status;
        }

        static double? Mass(SafetyPitLoad load, double? value, string field, bool optional)
        {
            if (!value.HasValue)
            {
                if (!optional)
                    Issue(load, field, "NOT PROVIDED; mass in kg required.", "MISSING_INPUT");
                return optional ? (double?)0 : null;
            }
            if (!ChassisReactionSolver.Finite(value.Value) || (optional ? value.Value < 0 : value.Value <= 0))
            {
                Issue(load, field, "Finite " + (optional ? "nonnegative" : "positive") + " mass in kg required.", "INVALID_INPUT");
                return null;
            }
            return value;
        }

        static double? Additional(SafetyPitLoad load, double? value, string field, string allocation, double? percent, string side)
        {
            var amount = Mass(load, value, field, true);
            if (!amount.HasValue)
                return null;
            if (amount.Value == 0)
            {
                load.Details.Add(
                    side + " effective additions: " + (value.HasValue ? "explicit zero" : "not included (blank optional mass)") + ".");
                return 0;
            }
            double share;
            if (allocation == "AllParticipates")
                share = 1;
            else if (allocation == "NoneParticipates")
                share = 0;
            else if (allocation == "FractionParticipates")
            {
                if (!percent.HasValue)
                {
                    Issue(
                        load,
                        "SafetyPit." + side + "EventAdditionalMassPercent",
                        "Specify participating percentage of the existing additional mass; do not re-enter the mass.",
                        "MISSING_INPUT");
                    return null;
                }
                if (!ChassisReactionSolver.Finite(percent.Value) || percent.Value < 0 || percent.Value > 100)
                {
                    Issue(
                        load,
                        "SafetyPit." + side + "EventAdditionalMassPercent",
                        "Participating percentage must be finite, from 0 through 100.",
                        "INVALID_INPUT");
                    return null;
                }
                share = percent.Value / 100;
            }
            else
            {
                Issue(load, "SafetyPit." + side + "AdditionalMassAllocation", allocation == null
                    ? "Choose how the existing additional effective mass participates in this assembly's event."
                    : "Unsupported additional-mass allocation.", allocation == null ? "MISSING_INPUT" : "INVALID_INPUT");
                return null;
            }
            load.Details.Add(side
                + " additional event mass = existing "
                + amount.Value
                + " kg × "
                + (share * 100)
                + "% = "
                + (amount.Value * share)
                + " kg. Explicit event allocation; effective suspended mass is not automatically event mass.");
            return amount * share;
        }

        static void Buffers(SafetyPitLoad load, int? count, string sharing)
        {
            string side = load.Id == "P6" ? "Car" : "Counterweight";
            if (!count.HasValue)
            {
                Issue(
                    load,
                    "SafetyPit." + side + "BufferCount",
                    "Select the physical buffer quantity explicitly; a drawing symbol does not establish count.",
                    "MISSING_INPUT");
                return;
            }
            if (count.Value <= 0)
            {
                Issue(load, "SafetyPit." + side + "BufferCount", "Positive integer buffer count required.", "INVALID_INPUT");
                return;
            }
            load.ComponentCount = count;
            // A single physical buffer has no sharing degree of freedom. Retained inactive
            // multi-buffer choices cannot become hidden mandatory inputs.
            if (count == 1)
                return;
            if (sharing == null)
            {
                Issue(
                    load,
                    "SafetyPit." + side + "BufferSharing",
                    "Multiple buffers require an explicit sharing model; equal division is not automatic.",
                    "MISSING_INPUT");
                return;
            }
            if (sharing != "EqualSharing")
                Issue(
                    load,
                    "SafetyPit." + side + "BufferSharing",
                    "For multiple buffers this phase supports only explicitly assumed equal sharing.",
                    "UNSUPPORTED_CONFIGURATION");
            else
                load.Details.Add("Explicit ideal equal-sharing assumption across "
                    + count
                    + " physical buffers; not established by the note or verified stiffness/contact distribution.");
        }

        public SafetyPitCalculationResult Calculate(ElevatorProject project)
        {
            var r = new SafetyPitCalculationResult();
            r.InputFingerprint = Fingerprint(project);
            if (project == null || project.Elevator == null || project.Calculations == null)
            {
                foreach (var l in new[] { r.P5, r.P6, r.P7 })
                    Issue(l, "Project", "Project capacity and component masses are unavailable.", "MISSING_INPUT");
                return r;
            }
            var c = project.Calculations;
            var s = c.SafetyPit ?? new SafetyPitInputs();
            foreach (var l in new[] { r.P5, r.P6 })
            {
                var capacity = Mass(l, project.Elevator.CapacityKg, "Elevator.CapacityKg", false);
                var car = Mass(l, c.CarMassKg, "Calculations.CarMassKg", false);
                var add = Additional(
                    l,
                    c.CarSideAdditionalSuspendedMassKg,
                    "Calculations.CarSideAdditionalSuspendedMassKg",
                    s.CarAdditionalMassAllocation,
                    s.CarEventAdditionalMassPercent,
                    "Car");
                if (capacity.HasValue && car.HasValue && add.HasValue)
                {
                    l.ParticipatingMassKg = capacity + car + add;
                    l.Details.Add("Car event mass = capacity "
                        + capacity
                        + " + empty car "
                        + car
                        + " + allocated car additions "
                        + add
                        + " = "
                        + l.ParticipatingMassKg
                        + " kg. Machine, chassis and hanger masses excluded.");
                }
            }
            var cw = Mass(r.P7, c.CounterweightMassKg, "Calculations.CounterweightMassKg", false);
            var cwadd = Additional(
                r.P7,
                c.CounterweightSideAdditionalSuspendedMassKg,
                "Calculations.CounterweightSideAdditionalSuspendedMassKg",
                s.CounterweightAdditionalMassAllocation,
                s.CounterweightEventAdditionalMassPercent,
                "Counterweight");
            if (cw.HasValue && cwadd.HasValue)
            {
                r.P7.ParticipatingMassKg = cw + cwadd;
                r.P7.Details.Add("CW event mass = counterweight "
                    + cw
                    + " + allocated CW additions "
                    + cwadd
                    + " = "
                    + r.P7.ParticipatingMassKg
                    + " kg. Capacity/car/machine/chassis/hanger excluded.");
            }
            if (s.SafetyGearType == null)
                Issue(r.P5, "SafetyPit.SafetyGearType", "Select the physical car safety-gear type.", "MISSING_INPUT");
            else if (s.SafetyGearType != "Progressive")
                Issue(
                    r.P5,
                    "SafetyPit.SafetyGearType",
                    "Only the note's progressive car safety gear is supported.",
                    "UNSUPPORTED_CONFIGURATION");
            if (!s.CarGuideCount.HasValue)
                Issue(
                    r.P5,
                    "SafetyPit.CarGuideCount",
                    "Confirm physical car-guide quantity; CAD line count is not physical quantity.",
                    "MISSING_INPUT");
            else if (s.CarGuideCount <= 0)
                Issue(r.P5, "SafetyPit.CarGuideCount", "Positive physical guide count required.", "INVALID_INPUT");
            else if (s.CarGuideCount != 2)
                Issue(
                    r.P5,
                    "SafetyPit.CarGuideCount",
                    "Only the confirmed two-car-guide arrangement is supported.",
                    "UNSUPPORTED_CONFIGURATION");
            r.P5.ComponentCount = s.CarGuideCount;
            Buffers(r.P6, s.CarBufferCount, s.CarBufferSharing);
            Buffers(r.P7, s.CounterweightBufferCount, s.CounterweightBufferSharing);
            foreach (var l in new[] { r.P5, r.P6, r.P7 })
            {
                if (l.Issues.Count > 0)
                    continue;
                double coefficient = l == r.P5 ? SafetyGearCoefficientNPerKg : BufferCoefficientNPerKg;
                double force = l.ParticipatingMassKg.Value * coefficient;
                if (!ChassisReactionSolver.Finite(force) || !ChassisReactionSolver.Finite(force * l.ComponentCount.Value))
                {
                    Issue(l, "Mass", "Load exceeds finite numerical range.", "INVALID_INPUT");
                    continue;
                }
                l.Status = "CALCULATED_UNDER_NOTE_ASSUMPTIONS";
                if (l == r.P5)
                {
                    l.PerComponentLoadN = force;
                    l.GroupLoadN = force * l.ComponentCount.Value;
                    l.Details.Add("P5 = "
                        + l.ParticipatingMassKg
                        + " kg × 10 N/kg = "
                        + force
                        + " N PER CAR GUIDE. Already per guide; no second division by guide count. Sum over two guides = "
                        + l.GroupLoadN
                        + " N.");
                }
                else
                {
                    l.GroupLoadN = force;
                    l.PerComponentLoadN = force / l.ComponentCount.Value;
                    l.Details.Add(l.Id
                        + " group = "
                        + l.ParticipatingMassKg
                        + " kg × max(40 N/kg, 4 × 9.81 N/kg) = "
                        + force
                        + " N; coefficient = "
                        + coefficient
                        + " N/kg. Per buffer = "
                        + l.PerComponentLoadN
                        + " N under "
                        + (l.ComponentCount == 1 ? "one physical buffer" : "explicit equal sharing")
                        + ".");
                }
                l.Details.Add(
                    "Adopted note coefficients: safety 10 N/kg is distinct from physical g = 9.81 m/s²; buffer factor 4 is the note's "
                    + "adopted value, not a supplied manufacturer rating. No chassis reactions or rope-tension/roping-ratio factors "
                    + "added.");
            }
            return r;
        }
    }
}
