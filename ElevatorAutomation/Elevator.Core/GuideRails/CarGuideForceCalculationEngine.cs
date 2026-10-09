using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;

namespace Elevator.Core.GuideRails
{
    public sealed class CarGuideForceCalculationEngine
    {
        public const string RuleVersion = "GearboxNoteCarGuideForces-v1";
        public const string SourceDocumentSha256 = SafetyPitCalculationEngine.SourceDocumentSha256;
        public const double AdoptedCoefficientNPerKg = 10;
        public const double DesignMultiplier = 2;

        public const string RuleBasis =
            "Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx, Third: irregular load distribution inside car, zero-based "
            + "descendant paragraphs 77–87. FF1 = Q × 10 × internal width / (4h); FF2 = Q × 10 × internal depth / (8h); design multiplier "
            + "2 applied once. Adopted note rules, not independent standards/manufacturer certification.";

        public const string Definitions =
            "Q: rated capacity kg; internal cabin width/depth: shared Plan.Cabin.Inner faces in mm; h: the note's Vertical Guide Shoe "
            + "Distance = vertical centre-to-centre distance between the upper and lower car guide shoes in mm (sliding shoes: mid-length "
            + "of the guiding insert; roller guides: roller axle centre). Manual input with no default; the application has no "
            + "car-frame/shoe geometry.";

        // Text only: it changes no equation, coefficient or factor (RuleVersion unchanged).
        public const string ApprovedInterpretation =
            "User-approved design interpretation (2026-10-07; basis EN 81-50:2014 Annex C.2.1, informative; not certification): FF1 = Fy, "
            + "along the car-rail line (project car-width direction), on the rail blade tip, bending the rail about x-x. FF2 = Fx, "
            + "perpendicular to the rail line (car-depth direction), on the blade side, bending the rail about y-y. Each value is the "
            + "horizontal force per guide rail at one guide-shoe level (equal and opposite at the upper and lower shoes): no division by "
            + "rail or shoe count and no group sum. Valid for two car guide rails with a centrally guided/suspended car. Not covered: "
            + "normal running (k2), car-sill loading (Fs), guide-rail stress/deflection/buckling check.";

        internal static string GeometryInputHash(ElevatorProject p)
        {
            return p == null || p.Shaft == null || p.Elevator == null || p.Standards == null ? null : CalculationFingerprint.Hash(new
            {
                p.Shaft.WidthMm,
                p.Shaft.DepthMm,
                p.Elevator.SystemId,
                p.Elevator.LegacyFamily,
                p.Elevator.TemplateId,
                p.Standards.RuleSetId
            });
        }

        internal static string GeometryContextHash(DesignContext c)
        {
            return c == null ? null : CalculationFingerprint.Hash(new
            {
                c.Equipment,
                c.CounterweightDepthMm
            });
        }

        internal static List<CalculationIssue> GeometryInputIssues(ElevatorProject p, DesignContext c)
        {
            var issues = new List<CalculationIssue>();
            if (p == null || p.Shaft == null || p.Elevator == null || p.Standards == null)
            {
                issues.Add(new CalculationIssue("Project", "Project shaft/template/rule context unavailable."));
                return issues;
            }
            if (!Positive(p.Shaft.WidthMm))
                issues.Add(new CalculationIssue("Shaft.WidthMm", "Shaft width must be finite positive mm."));
            if (!Positive(p.Shaft.DepthMm))
                issues.Add(new CalculationIssue("Shaft.DepthMm", "Shaft depth must be finite positive mm."));
            if (p.Elevator.SystemId != "traction-geared"
                || p.Elevator.LegacyFamily != ElevatorType.ExistingPlan
                || p.Elevator.TemplateId != "existing-plan-v1")
            {
                issues.Add(
                                    new CalculationIssue(
                                        "Elevator.TemplateId",
                                        "No registered shared cabin geometry recipe for this system/template."));
            }
            if (p.Standards.RuleSetId != "accepted-project-rules-v1")
                issues.Add(new CalculationIssue("Standards.RuleSetId", "Unregistered cabin geometry rule set."));
            if (c == null || c.Equipment == null)
            {
                issues.Add(
                    new CalculationIssue("DesignContext.Equipment", "Reviewed equipment profile required for internal cabin dimensions."));
                return issues;
            }
            if (!Positive(c.CounterweightDepthMm))
            {
                issues.Add(new CalculationIssue(
                                    "DesignContext.CounterweightDepthMm",
                                    "Finite positive depth required by the shared plan recipe."));
            }
            foreach (var f in typeof(EquipmentProfile).GetProperties().Where(f => f.PropertyType == typeof(double)))
            {
                var v = (double)f.GetValue(c.Equipment, null);
                if (!ChassisReactionSolver.Finite(v) || v < 0)
                {
                    issues.Add(
                                            new CalculationIssue(
                                                "DesignContext.Equipment." + f.Name,
                                                "Profile distance must be finite nonnegative mm."));
                }
            }
            foreach (var f in typeof(EquipmentProfile).GetProperties().Where(f => f.PropertyType == typeof(double[])))
            {
                var values = (double[])f.GetValue(c.Equipment, null);
                if (values == null || values.Length == 0 || values.Any(v => !Positive(v)))
                {
                    issues.Add(
                                            new CalculationIssue(
                                                "DesignContext.Equipment." + f.Name,
                                                "Shared plan size choices must be finite positive mm."));
                }
            }
            return issues;
        }

        static bool Positive(double v)
        {
            return ChassisReactionSolver.Finite(v) && v > 0;
        }

        public static bool IsGeometryCurrent(CarGuideForceGeometry g, ElevatorProject p, DesignContext c)
        {
            return g != null
                && g.Status == "SOURCE_BOUND"
                && GeometryInputIssues(p, c).Count == 0
                && g.SourceInputHash != null
                && g.SourceContextHash != null
                && g.SourceInputHash == GeometryInputHash(p)
                && g.SourceContextHash == GeometryContextHash(c);
        }

        static string Fingerprint(ElevatorProject p, DesignContext c)
        {
            return p == null || p.Elevator == null ? null : CalculationFingerprint.Hash(new
            {
                Rule = RuleVersion,
                GeometryInputs = GeometryInputHash(p),
                GeometryContext = GeometryContextHash(c),
                p.Elevator.CapacityKg,
                GuideShoeSpacingMm = p.Calculations == null || p.Calculations.CarGuideForces == null
                    ? (double?)null
                    : p.Calculations.CarGuideForces.GuideShoeSpacingMm
            });
        }

        public static bool IsCurrent(CarGuideForceResult r, ElevatorProject p, DesignContext c)
        {
            return r != null
                && r.RuleVersion == RuleVersion
                && r.InputFingerprint != null
                && r.InputFingerprint == Fingerprint(p, c)
                && IsGeometryCurrent(r.Geometry, p, c);
        }

        // Independent scalar rule evaluator for consistent length units and note
        // fixtures. This creates no provenance and is not the live project workflow.
        public static double[] EvaluateNoteRule(double capacityKg, double width, double depth, double h)
        {
            if (!Positive(capacityKg) || !Positive(width) || !Positive(depth) || !Positive(h))
                throw new ArgumentException("Capacity and consistent length dimensions must be finite positive values.");
            double ff1 =
                capacityKg * AdoptedCoefficientNPerKg * (width / h) / 4, ff2 = capacityKg * AdoptedCoefficientNPerKg * (depth / h) / 8;
            var values = new[] { ff1, ff1 * DesignMultiplier, ff2, ff2 * DesignMultiplier };
            if (values.Any(v => !Positive(v)))
                throw new ArgumentException("FF force exceeds finite numerical range.");
            return values;
        }

        static void Input(CarGuideForceResult r, double? v, string field, string label)
        {
            if (!v.HasValue)
            {
                r.Issues.Add(new CalculationIssue(field, label + " NOT PROVIDED; finite positive value required."));
            }
            else if (!Positive(v.Value))
            {
                r.Status = "INVALID_INPUT";
                r.Issues.Add(new CalculationIssue(field, label + " must be finite and positive."));
            }
        }

        public CarGuideForceResult Calculate(ElevatorProject p, CarGuideForceGeometry g, DesignContext c)
        {
            var r = new CarGuideForceResult
            {
                Geometry = g,
                InputFingerprint = Fingerprint(p, c),
                CapacityKg = p == null || p.Elevator == null ? (double?)null : p.Elevator.CapacityKg,
                GuideShoeSpacingMm = p == null || p.Calculations == null || p.Calculations.CarGuideForces == null
                    ? (double?)null
                    : p.Calculations.CarGuideForces.GuideShoeSpacingMm
            };
            var sourceIssues = GeometryInputIssues(p, c);
            if (sourceIssues.Count > 0)
            {
                r.Status = "INVALID_INPUT";
                r.Issues.AddRange(sourceIssues);
                return r;
            }
            if (g == null)
            {
                r.Issues.Add(new CalculationIssue(
                    "Cabin.Geometry",
                    "Shared source-bound internal cabin dimensions unavailable; regenerate through ElevatorDesignEngine."));
                return r;
            }
            if (g.Status != "SOURCE_BOUND")
            {
                r.Status = g.Status;
                r.Issues.AddRange(g.Issues);
                return r;
            }
            if (!IsGeometryCurrent(g, p, c))
            {
                r.Status = "STALE_GEOMETRY";
                r.Issues.Add(new CalculationIssue(
                    "Cabin.Geometry",
                    "Geometry-driving project/profile inputs changed; regenerate internal cabin dimensions before calculation."));
                return r;
            }
            r.InternalWidthMm = g.InternalWidthMm;
            r.InternalDepthMm = g.InternalDepthMm;
            Input(r, r.CapacityKg, "Elevator.CapacityKg", "Rated capacity (kg)");
            Input(
                r,
                r.GuideShoeSpacingMm,
                "Calculations.CarGuideForces.GuideShoeSpacingMm",
                "Vertical upper-to-lower car guide-shoe spacing h (mm)");
            if (r.Issues.Count > 0)
                return r;
            try
            {
                var f = EvaluateNoteRule(r.CapacityKg.Value, r.InternalWidthMm.Value, r.InternalDepthMm.Value, r.GuideShoeSpacingMm.Value);
                r.FF1BaseN = f[0];
                r.FF1DesignN = f[1];
                r.FF2BaseN = f[2];
                r.FF2DesignN = f[3];
                r.Status = "CALCULATED_UNDER_NOTE_ASSUMPTIONS";
            }
            catch (ArgumentException e)
            {
                r.Status =
                "INVALID_INPUT";
                r.Issues.Add(new CalculationIssue("FF.NumericalRange", e.Message));
                return r;
            }
            string q = r.CapacityKg.Value.ToString("0.###", CultureInfo.InvariantCulture), w =
                r.InternalWidthMm.Value.ToString("0.###", CultureInfo.InvariantCulture), d =
                r.InternalDepthMm.Value.ToString("0.###", CultureInfo.InvariantCulture), h =
                    r.GuideShoeSpacingMm.Value.ToString("0.###", CultureInfo.InvariantCulture);
            r.Details.Add("FF1 base = "
                + q
                + " kg × 10 N/kg × "
                + w
                + " mm / (4 × "
                + h
                + " mm) = "
                + r.FF1BaseN
                + " N; design = base × 2 = "
                + r.FF1DesignN
                + " N.");
            r.Details.Add("FF2 base = "
                + q
                + " kg × 10 N/kg × "
                + d
                + " mm / (8 × "
                + h
                + " mm) = "
                + r.FF2BaseN
                + " N; design = base × 2 = "
                + r.FF2DesignN
                + " N.");
            r.Details.Add(Definitions);
            r.Details.Add(ApprovedInterpretation);
            r.Details.Add(
                "Irregular rated-load distribution inside car. No car/CW/machine mass, rope tension, roping ratio or P1–P7 load added. The "
                + "adopted 10 N/kg is not replaced with g = 9.81. No guide strength or connection verification.");
            return r;
        }
    }
}
