using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;

namespace Elevator.Core.Application
{
    // Live calculation (ARCHITECTURE-PLAN P4): the current results, the text of invalid entries, and what is recalculated
    // when an input changes. UI-free: the application schedules Recalculate (a 300 ms timer) and shows the results.
    // Invalid entries keep their field message and the dependent result shows nothing.
    public sealed class LiveCalculation
    {
        public const string GuideShoeSpacingKey = "Calculations.CarGuideForces.GuideShoeSpacingMm";
        readonly DesignContext context;
        readonly PresetFlow presets;

        public LiveCalculation(DesignContext context, PresetFlow presets)
        {
            this.context = context;
            this.presets = presets;
            Chassis = new CalculationWorkspace();
        }

        public CalculationWorkspace Chassis { get; private set; }
        public RopeTractionResult RopeTraction { get; set; }
        public SafetyPitCalculationResult SafetyPit { get; set; }
        public CarGuideForceGeometry CarGuideForceGeometry { get; private set; }
        public CarGuideForceResult CarGuideForces { get; set; }

        // The validation / generation design of the current inputs (null when an input changed).
        public ElevatorDesignResult Design { get; set; }

        // Typed text of the chassis inputs and the messages of the invalid ones (path → text / message).
        public readonly Dictionary<string, string> ChassisDrafts = new Dictionary<string, string>();
        public readonly Dictionary<string, string> ChassisDraftErrors = new Dictionary<string, string>();
        public readonly Dictionary<string, string> SafetyPitDrafts = new Dictionary<string, string>();
        public readonly Dictionary<string, string> SafetyPitDraftErrors = new Dictionary<string, string>();

        public string CarGuideForceDraft { get; set; }
        public string CarGuideForceDraftError { get; set; }

        // True while Recalculate runs (a scheduled run is skipped then).
        public bool Running { get; private set; }

        // ---------- what an input change makes outdated ----------
        public static InputEffect EffectOf(string path)
        {
            if (path.StartsWith("Report."))
                return InputEffect.None; // sheet metadata: no calculation or drawing input
            var effect = InputEffect.Design | InputEffect.Chassis | InputEffect.RopeTraction;
            if (path == "Elevator.CapacityKg" || path == "Shaft.WidthMm" || path == "Shaft.DepthMm")
                effect |= InputEffect.CarGuideForces;
            return effect;
        }

        public bool ChassisStale(ElevatorProject project)
        {
            var r = Chassis.Result;
            return r != null && r.Reactions != null && !Chassis.IsCurrent(project, context);
        }

        public bool SafetyPitStale(ElevatorProject project)
        {
            return SafetyPit != null && !SafetyPitCalculationEngine.IsCurrent(SafetyPit, project);
        }

        public bool CarGuideForcesStale(ElevatorProject project)
        {
            return CarGuideForces != null && !CarGuideForceCalculationEngine.IsCurrent(CarGuideForces, project, context);
        }

        public bool CarGuideForceGeometryCurrent(ElevatorProject project)
        {
            return CarGuideForceCalculationEngine.IsGeometryCurrent(CarGuideForceGeometry, project, context);
        }

        // ---------- resets ----------
        public void Reset()
        {
            Chassis = new CalculationWorkspace();
            ChassisDrafts.Clear();
            ChassisDraftErrors.Clear();
            ResetSafetyPit();
            ResetRopeTraction();
            ResetCarGuideForces();
        }

        public void ResetSafetyPit()
        {
            SafetyPit = null;
            SafetyPitDraftErrors.Clear();
            SafetyPitDrafts.Clear();
        }

        public void ResetRopeTraction()
        {
            RopeTraction = null;
        }

        public void ResetCarGuideForces()
        {
            CarGuideForceGeometry = null;
            CarGuideForces = null;
            CarGuideForceDraft = null;
            CarGuideForceDraftError = null;
        }

        // After Fill blanks: every result is outdated and the typed text is replaced.
        public void InputsReplaced()
        {
            Design = null;
            Chassis.Invalidate();
            ResetSafetyPit();
            ResetRopeTraction();
            ResetCarGuideForces();
            ChassisDrafts.Clear();
            ChassisDraftErrors.Clear();
        }

        // After Reset to preset of a field: its typed text is dropped.
        public void ClearDraft(string key)
        {
            ChassisDrafts.Remove(key);
            ChassisDraftErrors.Remove(key);
            if (key == GuideShoeSpacingKey)
            {
                CarGuideForceDraft = null;
                CarGuideForceDraftError = null;
            }
        }

        // ---------- typed text ----------
        // A chassis input: invalid text clears the canonical value instead of leaving an old hidden number active.
        public object EnterChassisText(string path, string label, string text, bool numeric)
        {
            ChassisDrafts[path] = text;
            ChassisDraftErrors.Remove(path);
            object parsed = null;
            if (!String.IsNullOrWhiteSpace(text))
            {
                if (!numeric)
                    parsed = text;
                else
                {
                    double d;
                    if (Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                        && !Double.IsNaN(d)
                        && !Double.IsInfinity(d))
                        parsed = d;
                    else
                        ChassisDraftErrors[path] = label + ": finite number required (mm or kg as labelled).";
                }
            }
            return parsed;
        }

        public double? EnterSafetyPitPercentage(string path, string label, string text)
        {
            SafetyPitDrafts[path] = text;
            SafetyPitDraftErrors.Remove(path);
            double number;
            double? parsed = null;
            if (!String.IsNullOrWhiteSpace(text))
            {
                if (Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                    && ChassisReactionSolver.Finite(number))
                    parsed = number;
                else
                    SafetyPitDraftErrors[path] = label + ": finite percentage required.";
            }
            return parsed;
        }

        public double? EnterGuideShoeSpacing(string label, string text)
        {
            CarGuideForceDraft = text;
            CarGuideForceDraftError = null;
            double value;
            double? parsed = null;
            if (!String.IsNullOrWhiteSpace(text))
            {
                if (Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && ChassisReactionSolver.Finite(value))
                    parsed = value;
                else
                    CarGuideForceDraftError =
                        label + ": invalid text; enter a finite positive number. Malformed text is not a missing value.";
            }
            return parsed;
        }

        // The invalid chassis entries that block the current mode (Basic: only the masses it uses).
        public Dictionary<string, string> RelevantDraftErrors(ElevatorProject project)
        {
            if (CalculationModes.Resolve(project.Calculations) != "Basic")
                return ChassisDraftErrors;
            var c = project.Calculations;
            var fields = new[] { "CarMassKg",
                "CounterweightMassKg",
                "MachineMassKg",
                "CarSideAdditionalSuspendedMassKg",
                "CounterweightSideAdditionalSuspendedMassKg" }.Select(f => "Calculations." + f).ToList();
            if (c.IncludeChassisSelfWeight == true)
                fields.Add("Calculations.ChassisMassKg");
            if (c.IncludeHangerAssemblyWeight == true)
                fields.Add("Calculations.HangerAssemblyMassKg");
            return ChassisDraftErrors.Where(pair => fields.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public Dictionary<string, string> RelevantSafetyPitDraftErrors(ElevatorProject project)
        {
            var errors = new Dictionary<string, string>();
            foreach (string field in new[] { "CarMassKg",
                "CounterweightMassKg",
                "CarSideAdditionalSuspendedMassKg",
                "CounterweightSideAdditionalSuspendedMassKg" })
            {
                string path = "Calculations." + field, message;
                if (ChassisDraftErrors.TryGetValue(path, out message))
                    errors[path] = message + " Malformed text is not an omitted mass.";
            }
            var c = project.Calculations;
            var s = c == null ? null : c.SafetyPit;
            if (s != null)
                foreach (var pair in SafetyPitDraftErrors)
                {
                    bool car = pair.Key.EndsWith("CarEventAdditionalMassPercent");
                    if (car
                        ? s.CarAdditionalMassAllocation == "FractionParticipates" && c.CarSideAdditionalSuspendedMassKg > 0
                        : s.CounterweightAdditionalMassAllocation == "FractionParticipates"
                            && c.CounterweightSideAdditionalSuspendedMassKg > 0)
                        errors[pair.Key] = pair.Value;
                }
            return errors;
        }

        public void ApplySafetyPitDraftValidation(ElevatorProject project, SafetyPitCalculationResult result)
        {
            if (result == null)
                return;
            foreach (var error in RelevantSafetyPitDraftErrors(project))
            {
                bool cw = error.Key == "Calculations.CounterweightMassKg"
                    || error.Key == "Calculations.CounterweightSideAdditionalSuspendedMassKg"
                    || error.Key.EndsWith("CounterweightEventAdditionalMassPercent");
                foreach (var load in cw ? new[] { result.P7 } : new[] { result.P5, result.P6 })
                {
                    load.Status = "INVALID_INPUT";
                    load.GroupLoadN = null;
                    load.PerComponentLoadN = null;
                    load.ParticipatingMassKg = null;
                    load.Details.Clear();
                    load.Issues.RemoveAll(issue => issue.Field == error.Key);
                    load.Issues.Add(new CalculationIssue(error.Key, error.Value));
                }
            }
        }

        public void ApplyCarGuideForceDraftValidation()
        {
            if (CarGuideForces == null || CarGuideForceDraftError == null)
                return;
            var r = CarGuideForces;
            r.Status = "INVALID_INPUT";
            r.FF1BaseN = null;
            r.FF1DesignN = null;
            r.FF2BaseN = null;
            r.FF2DesignN = null;
            r.Details.Clear();
            r.Issues.RemoveAll(issue => issue.Field == GuideShoeSpacingKey);
            r.Issues.Add(new CalculationIssue(GuideShoeSpacingKey, CarGuideForceDraftError));
        }

        public void EnsureCarGuideForceGeometry(ElevatorProject project)
        {
            if (!CarGuideForceCalculationEngine.IsGeometryCurrent(CarGuideForceGeometry, project, context))
                CarGuideForceGeometry = new ElevatorDesignEngine().CalculateCarGuideForceGeometry(project, context);
        }

        // ---------- calculation ----------
        // Validation of the current inputs for the selected drawing views.
        public ElevatorDesignResult Validate(ProjectSession session)
        {
            if (session.Views == 0)
                throw new ArgumentException("Select at least one view.");
            Design = new ElevatorDesignEngine().Calculate(session.Project, context, session.Views);
            return Design;
        }

        public void Recalculate(ElevatorProject project)
        {
            Recalculate(project, () => presets.Apply(project, true));
        }

        // Applies the presets (applyPresets; its errors are ignored), then recalculates only the results whose inputs changed;
        // independent current results stay as they are.
        public void Recalculate(ElevatorProject project, Action applyPresets)
        {
            if (Running)
                return;
            Running = true;
            try
            {
                var p = project;
                if (p.Calculations == null)
                    p.Calculations = new ChassisCalculationInputs();
                try
                {
                    applyPresets();
                }
                catch (Exception) { }
                try
                {
                    if (RelevantDraftErrors(p).Count > 0)
                        Chassis.Invalidate();
                    else if (Chassis.Result == null || !Chassis.IsCurrent(p, context))
                        Chassis.Calculate(p, context);
                }
                catch (Exception) { Chassis.Invalidate(); }
                try
                {
                    if (!RopeTractionCalculationEngine.IsCurrent(RopeTraction, p, context, presets.Library))
                        RopeTraction = new RopeTractionCalculationEngine().Calculate(p, context, presets.Library);
                }
                catch (Exception) { RopeTraction = null; }
                try
                {
                    if (!SafetyPitCalculationEngine.IsCurrent(SafetyPit, p))
                    {
                        SafetyPit = new SafetyPitCalculationEngine().Calculate(p);
                        ApplySafetyPitDraftValidation(p, SafetyPit);
                    }
                }
                catch (Exception) { SafetyPit = null; }
                try
                {
                    if (CarGuideForceDraftError != null || !CarGuideForceCalculationEngine.IsCurrent(CarGuideForces, p, context))
                    {
                        EnsureCarGuideForceGeometry(p);
                        CarGuideForces = new CarGuideForceCalculationEngine().Calculate(p, CarGuideForceGeometry, context);
                        ApplyCarGuideForceDraftValidation();
                    }
                }
                catch (Exception) { CarGuideForces = null; }
            }
            finally { Running = false; }
        }
    }
}
