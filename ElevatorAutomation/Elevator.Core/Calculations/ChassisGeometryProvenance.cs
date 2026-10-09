using System;
using System.Collections.Generic;
using Elevator.Core.Serialization;
using Elevator.Core.Project;
namespace Elevator.Core.Calculations
{
    internal sealed class GeometrySourceSnapshot
    {
        internal readonly string InputFingerprint;
        internal readonly string ContextFingerprint;
        internal readonly DesignContext OriginContext;
        internal GeometrySourceSnapshot(ElevatorProject project, DesignContext context)
        {
            InputFingerprint = ChassisGeometryProvenance.InputHash(project);
            ContextFingerprint = CalculationFingerprint.Hash(context);
            OriginContext = context;
        }
    }
    public sealed class GeometryProvenanceValidation
    {
        public string Status;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public bool IsValid
        {
            get
            {
                return Issues.Count == 0;
            }
        }
    }
    // Issued only by the shared design engine. Digests are immutable snapshots taken
    // before generation, not a stamp made from the calculation caller's current project.
    public sealed class ChassisGeometryProvenance
    {
        public const string Version = "ChassisGeometrySource-v1";
        readonly string inputFingerprint;
        readonly string contextFingerprint;
        readonly string geometryFingerprint;
        readonly DesignContext originContext;
        public string SourceVersion
        {
            get
            {
                return Version;
            }
        }
        public string SourceInputFingerprint
        {
            get
            {
                return inputFingerprint;
            }
        }
        public string SourceContextFingerprint
        {
            get
            {
                return contextFingerprint;
            }
        }
        public string GeneratedGeometryFingerprint
        {
            get
            {
                return geometryFingerprint;
            }
        }
        internal ChassisGeometryProvenance(GeometrySourceSnapshot source, ChassisCalculationGeometry geometry)
        {
            inputFingerprint = source.InputFingerprint;
            contextFingerprint = source.ContextFingerprint;
            originContext = source.OriginContext;
            geometryFingerprint = GeometryHash(geometry);
        }
        internal static string InputHash(ElevatorProject p)
        {
            if (p == null
                || p.Shaft == null
                || p.Elevator == null
                || p.Machine == null
                || p.MachineRoom == null
                || p.Suspension == null
                || p.Standards == null
                || p.Counterweight == null)
                return null;
            // Chassis/view geometry drivers and equipment/rule identity, not load-only CG/masses.
            return CalculationFingerprint.Hash(new
            {
                Version = Version,
                SW = p.Shaft.WidthMm,
                SD = p.Shaft.DepthMm,
                Capacity = p.Elevator.CapacityKg,
                p.Elevator.SystemId,
                p.Elevator.LegacyFamily,
                p.Elevator.TemplateId,
                Machine = p.Machine,
                MachineRoom = p.MachineRoom,
                RopingRatio = p.Suspension.RopingRatio,
                CounterweightPosition = p.Counterweight.Position,
                p.Standards.RuleSetId
            });
        }
        static string GeometryHash(ChassisCalculationGeometry g)
        {
            if (g == null)
                return null;
            var serializer = CoreJson.Create();
            try
            {
                var payload = (Dictionary<string, object>)serializer.DeserializeObject(serializer.Serialize(g));
                payload.Remove("Provenance"); // Hash all payload fields, without a self-referential digest.
                return CalculationFingerprint.Hash(payload);
            }
            catch (ArgumentException) { return null; }
        }
        public static GeometryProvenanceValidation Validate(
            ElevatorProject p,
            ChassisCalculationGeometry g,
            DesignContext currentContext = null)
        {
            var result = new GeometryProvenanceValidation();
            Action<string, string, string> reject =
                (status, field, message) => { result.Status = status; result.Issues.Add(new CalculationIssue(field, message)); };
            if (p == null || p.Machine == null)
            {
                reject("MISSING_INPUT", "Machine", "Canonical machine inputs unavailable.");
                return result;
            }
            var diameter = p.Machine.TractionSheavePitchDiameterMm;
            if (diameter.HasValue && (!ChassisReactionSolver.Finite(diameter.Value) || Math.Abs(diameter.Value - 520) > .02))
            {
                reject(
                    "INVALID_INPUT",
                    "Machine.TractionSheavePitchDiameterMm",
                    "Only the reviewed 520 mm traction machine is supported; independent calculation API validation requires finite 520 mm "
                    + "input when supplied.");
                return result;
            }
            if (g == null)
            {
                reject("MISSING_INPUT", "Geometry", "Calculated native geometry unavailable.");
                return result;
            }
            var source = g.Provenance;
            if (source == null)
            {
                reject(
                    "STALE_GEOMETRY",
                    "Geometry.Provenance",
                    "Source provenance unavailable. Generate chassis geometry through ElevatorDesignEngine; raw/older geometry cannot be "
                    + "paired with current inputs.");
                return result;
            }
            string currentInputs = InputHash(p);
            if (currentInputs == null || source.inputFingerprint == null || currentInputs != source.inputFingerprint)
            {
                reject(
                    "STALE_GEOMETRY",
                    "Geometry.SourceInputs",
                    "Geometry-driving project inputs differ from the inputs captured before shared drawing generation. Regenerate geometry "
                    + "first.");
                return result;
            }
            // The two-argument API also detects mutations of its originating live context.
            // Supply currentContext when replacing that context with another instance.
            string context = CalculationFingerprint.Hash(currentContext ?? source.originContext);
            if (context == null || source.contextFingerprint == null || context != source.contextFingerprint)
            {
                reject(
                    "STALE_GEOMETRY",
                    "Geometry.SourceContext",
                    "Equipment profile, native evidence or section/reference context differs from the generation snapshot. Regenerate "
                    + "geometry first.");
                return result;
            }
            string payload = GeometryHash(g);
            if (payload == null || source.geometryFingerprint == null || payload != source.geometryFingerprint)
            {
                reject(
                    "STALE_GEOMETRY",
                    "Geometry.GeneratedPayload",
                    "Generated geometry was changed after generation. Regenerate through the shared design engine; do not restamp edited "
                    + "or retained geometry.");
                return result;
            }
            if (g.Recipe == "Basic")
            {
                if (g.Section == null || g.Section.Reference == null || !ChassisReactionSolver.Finite(g.TractionCentre))
                    reject(
                        "INVALID_GEOMETRY_OR_LOAD",
                        "Geometry.MachineDatum",
                        "Calculated native traction datum/reference is unavailable.");
                return result; // Basic does not use rope tangencies or deflector actions.
            }
            if (g.Section == null
                || g.Section.Arrangement == null
                || g.Section.Arrangement.Reeving == null
                || !ChassisReactionSolver.Finite(g.Section.Arrangement.Reeving.TractionSheavePitchDiameterMm)
                || Math.Abs(g.Section.Arrangement.Reeving.TractionSheavePitchDiameterMm - 520) > .02)
            {
                reject(
                    "INVALID_GEOMETRY_OR_LOAD",
                    "Geometry.TractionSheavePitchDiameterMm",
                    "Calculated geometry does not contain the reviewed 520 mm traction sheave.");
            }
            return result;
        }
    }
}
