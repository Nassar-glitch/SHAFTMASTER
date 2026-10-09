using System;
using System.Collections.Generic;
using System.Globalization;
using Elevator.Core.Project;

namespace Elevator.Core.MachineRoom
{
    public static class MachineRoomRuleRegistry
    {
        private static EngineeringRule Code(string id, string description, string clause, string required, string missing)
        {
            return new EngineeringRule
            {
                RuleId = id,
                Description = description,
                RequirementType = RequirementType.Code,
                SourceDocument = "EN 81-20",
                Edition = "2014",
                Clause = clause,
                RequiredValue = required,
                Status = "NOT YET EVALUABLE",
                EngineeringNote = missing,
                InputParameters = new Dictionary<string, object>()
            };
        }

        public static List<EngineeringRule> Evaluate(ProjectInput input, ElevatorParameters p, MachineRoomParameters mr)
        {
            var rules = new List<EngineeringRule>();
            var ropeMinimum = Code(
                "MR-ROPE-MIN-001",
                "Suspension rope nominal diameter",
                "5.5.1.2(a)",
                "Nominal rope diameter >=8 mm",
                "Unknown rope diameter must not be assumed. This check applies even when the deflector is absent.");
            ropeMinimum.InputParameters["SuspensionRopeDiameterMm"] = input.SuspensionRopeDiameterMm;
            if (input.SuspensionRopeDiameterMm.HasValue)
            {
                ropeMinimum.CalculatedValue = input.SuspensionRopeDiameterMm.Value.ToString(CultureInfo.InvariantCulture);
                ropeMinimum.Status = input.SuspensionRopeDiameterMm.Value >= 8 ? "PASS - NUMERIC CHECK ONLY" : "FAIL";
            }
            rules.Add(ropeMinimum);
            rules.Add(new EngineeringRule
            {
                RuleId = "MR-GOV-OFFSET-001",
                Description = "Car-guide axis to start of governor sheave diameter",
                RequirementType = RequirementType.Project,
                SourceDocument = "Engineer clarification",
                InputParameters = new Dictionary<string, object> { { "TZ", p.TZ } },
                CalculatedValue = mr.Governor.OffsetFromCarGuideAxisMm.HasValue
                    ? mr.Governor.OffsetFromCarGuideAxisMm.Value.ToString(CultureInfo.InvariantCulture)
                    : "Retain current offset pending review",
                Status = mr.Governor.SelectionStatus,
                EngineeringNote = mr.Governor.RuleSource + " Governor diameter remains separate from the handling sheave."
            });
            rules.Add(new EngineeringRule
            {
                RuleId = "MR-DS-001",
                Description = "Deflector sheave selection from shared TZ",
                RequirementType = RequirementType.Project,
                SourceDocument = "Engineer-supplied project instruction",
                CalculatedValue = mr.Deflector.PitchDiameterMm.HasValue
                    ? mr.Deflector.PitchDiameterMm.Value.ToString(CultureInfo.InvariantCulture)
                    : mr.Deflector.SelectionStatus,
                RequiredValue = "<520 none; 520..800:250; >800..1100:400; >1100 review",
                Status = mr.Deflector.SelectionStatus,
                InputParameters = new Dictionary<string, object> { { "TZ", p.TZ } },
                EngineeringNote = "Not an EN 81-20 sheave-selection rule. No extrapolation above 1100 mm."
            });
            rules.Add(new EngineeringRule
            {
                RuleId = "MR-WRAP-001",
                Description = "Traction wrap target",
                RequirementType = RequirementType.Project,
                SourceDocument = "Engineer-supplied project instruction",
                RequiredValue = "152 degrees target",
                Status = mr.Reeving.WrapAngleStatus,
                CalculatedValue = mr.Reeving.CalculatedWrapAngleDeg.HasValue
                    ? mr.Reeving.CalculatedWrapAngleDeg.Value.ToString(CultureInfo.InvariantCulture)
                    : null,
                EngineeringNote =
                    "Future optimization changes vertical center offset/chassis elevation with the selected 250/400 mm diameter fixed. No "
                    + "vertical distance or actual wrap is inferred from the plan. A wrap pass does not resolve a diameter-ratio conflict "
                    + "or prove traction compliance."
            });
            var ratio = Code(
                "MR-ROPE-001",
                "Suspension rope/sheave diameter compatibility",
                "5.5.1.2(a); 5.5.2.1",
                "Rope >=8 mm; pitch diameter / rope diameter >=40",
                "Applies to the selected deflector when used with suspension ropes. Rope specification and manufacturer confirmation of "
                + "pitch diameter remain required.");
            ratio.InputParameters["DeflectorPitchDiameterMm"] = mr.Deflector.PitchDiameterMm;
            ratio.InputParameters["SuspensionRopeDiameterMm"] = input.SuspensionRopeDiameterMm;
            if (mr.Deflector.PitchDiameterMm.HasValue)
            {
                double diameter = mr.Deflector.PitchDiameterMm.Value;
                if (diameter < 320)
                {
                    ratio.Status = "FAIL";
                    ratio.CalculatedValue = diameter + " mm < 40 x 8 mm = 320 mm";
                    ratio.EngineeringNote =
                        "The 250 mm project selection cannot satisfy both clauses for suspension ropes; this conflict is not resolved by "
                        + "this layout.";
                }
                else if (input.SuspensionRopeDiameterMm.HasValue)
                {
                    double rope = input.SuspensionRopeDiameterMm.Value;
                    if (Double.IsNaN(rope) || Double.IsInfinity(rope) || rope <= 0)
                        throw new ArgumentException("Suspension rope diameter must be finite and positive.");
                    ratio.CalculatedValue = (diameter / rope).ToString("0.###", CultureInfo.InvariantCulture);
                    ratio.Status = rope >= 8 && diameter / rope >= 40 ? "PASS - NUMERIC CHECK ONLY" : "FAIL";
                }
            }
            else if (!mr.Deflector.IsRequired)
                ratio.Status = "NOT APPLICABLE TO ABSENT DEFLECTOR";
            rules.Add(ratio);
            rules.Add(Code(
                "MR-ROOM-001",
                "Control equipment working space",
                "5.2.6.3.2.1(a)",
                "Depth >=700 mm; width >=max(500 mm,panel width); working height >=2100 mm",
                "Confirm cabinet operating face, actual unobstructed zone, service envelope and headroom; room extensions alone do not "
                + "demonstrate compliance."));
            rules.Add(Code(
                "MR-ROOM-002",
                "Moving-parts maintenance area",
                "5.2.6.3.2.1(b)",
                "At least 500 x 600 mm at required maintenance/inspection/emergency-operation points",
                "Manufacturer service points, access zones and obstructions required."));
            rules.Add(Code(
                "MR-ROOM-003",
                "Access routes and rotating-parts headroom",
                "5.2.6.3.2.2; 5.2.6.3.2.3",
                "Route width >=500 mm (400 mm only under specified conditions); movement height >=1800 mm; >=300 mm above unprotected "
                + "rotating parts",
                "Continuous route, hot/moving parts, ceiling and equipment elevations missing."));
            rules.Add(Code(
                "MR-OPEN-001",
                "Floor openings",
                "5.2.6.3.3",
                "Minimum necessary openings; ferrules >=50 mm above slab/finished floor",
                "Rope swept envelopes, installation tolerances and ferrule heights missing."));
            rules.Add(Code(
                "MR-ACCESS-001",
                "Machine room access door",
                "5.2.3.2(a); 5.2.3.3(a)",
                "Clear height >=2000 mm; width >=600 mm; not inward opening",
                "Verify finished clear dimensions, leaf operation, locks and access route; drawn nominal opening is not a finished "
                + "clearance certificate."));
            rules.Add(Code(
                "MR-TRACTION-001",
                "Rope traction and rope safety factor",
                "5.5.3; 5.5.2.2",
                "EN 81-50:2014 5.11 and 5.12 calculations plus applicable EN 81-20 conditions",
                "Need EN 81-50, masses/loads, rope construction, breaking loads, reeving, groove/friction data and actual rope path."));
            rules.Add(Code(
                "MR-GUARD-001",
                "Protection for rotating/sheave equipment",
                "5.5.7.1/Table 10; 5.5.7.2; 5.9.1.2",
                "Applicable nip guards, retention and protection against objects; maintain inspection access",
                "Guard geometry, rope entry directions, access and EN ISO 13857:2008 Table 4 verification required."));
            rules.Add(Code(
                "MR-GOV-001",
                "Governor rope, accessibility and verification",
                "5.6.2.2.1.3; 5.6.2.2.1.4; 5.6.2.2.1.7",
                "Governor rope pitch ratio >=30 plus remaining applicable governor requirements",
                "Governor rope diameter, certified component, tripping data and reachable service area missing; do not confuse governor "
                + "with deflector."));
            rules.Add(Code(
                "MR-VENT-001",
                "Ventilation",
                "5.2.1.3",
                "Suitable ventilation; do not ventilate unrelated rooms through lift spaces",
                "Heat losses, ambient conditions and building ventilation design required. Annex E.3.3 is informative, not a fixed "
                + "opening-area formula."));
            rules.Add(new EngineeringRule
            {
                RuleId = "MR-STRUCT-001",
                Description = "Retained machine/chassis/support geometry",
                RequirementType = RequirementType.EngineeringAssumption,
                SourceDocument = "Supplied machine-room DWG",
                Status = "REQUIRES ENGINEERING REVIEW",
                EngineeringNote =
                    "Retain native machine, chassis and support details. No machine selection, P1-P4 loads, beam sizing or reinforcement "
                    + "calculation is inferred."
            });
            return rules;
        }
    }
}
