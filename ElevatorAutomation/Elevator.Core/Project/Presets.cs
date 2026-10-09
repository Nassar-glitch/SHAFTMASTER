using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Elevator.Core.Serialization;

namespace Elevator.Core.Project
{
    // Editable preset library (config/presets.json, shipped beside the application). Company practice changes here,
    // not in code. Values are design defaults approved in DESIGN-FLOW.md; they are never standard factors.
    public sealed class PresetLibrary
    {
        public int Version;
        public RopePresetTable Ropes;
        public GroovePreset Groove;
        public double? TravellingCableKgPerM;
        public double? BrakingRetardationMps2;
        public List<DeflectorMassRow> DeflectorMass;
        public string TractionLimitingMethod;

        // Project presets (DESIGN-FLOW stage 2): a new project opens complete; unlocked fields follow these values.
        public NewProjectPreset NewProject;
        public double? PersonMassKg;
        public double? CounterweightBalanceFactor;
        public double? TractionSheavePitchDiameterMm;
        public double? GuideShoeSpacingMm;
        public double? BracketMaximumSpacingMm;
        public List<CapacityPresetRow> CapacityTable;
        public List<RopeLadderRow> RopeLadder;
        public int? MaximumRopeCount;
        public List<double> TractionSheavePitchSteps;
        public List<double> UndercutAngleStepsDeg;
        public List<double> BalanceFactorSteps;
        public SafetyGearPreset SafetyGear;
        public BufferPreset Buffers;
        public AdvancedPreset Advanced;

        public static PresetLibrary Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Preset library not found: " + path, path);
            return Parse(File.ReadAllText(path));
        }

        public static PresetLibrary Parse(string json)
        {
            PresetLibrary library;
            try
            {
                library = CoreJson.CreateWithDefaultLimit().Deserialize<PresetLibrary>(json);
            }
            catch (Exception e) { throw new ArgumentException("Preset library is not valid JSON: " + e.Message); }
            if (library == null)
                throw new ArgumentException("Preset library is empty.");
            var problems = library.Validate();
            if (problems.Count > 0)
                throw new ArgumentException("Preset library is invalid: " + String.Join("; ", problems));
            return library;
        }

        static bool Positive(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v) && v > 0;
        }

        public List<string> Validate()
        {
            var p = new List<string>();
            if (Version != 1)
                p.Add("Version must be 1");
            if (Ropes == null || Ropes.Table == null || Ropes.Table.Count == 0)
                p.Add("Ropes.Table is required");
            else
            {
                if (String.IsNullOrWhiteSpace(Ropes.Designation))
                    p.Add("Ropes.Designation is required");
                foreach (var r in Ropes.Table)
                    if (r == null || !Positive(r.DiameterMm) || !Positive(r.MinimumBreakingLoadKn) || !Positive(r.MassKgPerM))
                        p.Add("Ropes.Table rows need positive DiameterMm, MinimumBreakingLoadKn and MassKgPerM");
                if (Ropes.Table.Where(r => r != null).GroupBy(r => r.DiameterMm).Any(g => g.Count() > 1))
                    p.Add("Ropes.Table diameters must be unique");
            }
            if (Groove == null)
                p.Add("Groove is required");
            else
            {
                var problem = GrooveTypes.AngleProblem(Groove.Type, Groove.UndercutAngleDeg, Groove.GrooveAngleDeg);
                if (problem != null)
                    p.Add("Groove: " + problem);
            }
            if (!TravellingCableKgPerM.HasValue || !Positive(TravellingCableKgPerM.Value))
                p.Add("TravellingCableKgPerM must be positive");
            if (!BrakingRetardationMps2.HasValue
                || Double.IsNaN(BrakingRetardationMps2.Value)
                || BrakingRetardationMps2.Value < TractionRules.MinimumBrakingRetardationMps2)
                p.Add("BrakingRetardationMps2 must be at least 0.5 (EN 81-50 5.11.2.2.2)");
            if (DeflectorMass == null || DeflectorMass.Count == 0)
                p.Add("DeflectorMass table is required");
            else
            {
                if (DeflectorMass.Any(r => r == null || !Positive(r.PitchDiameterMm) || !Positive(r.MassKg)))
                    p.Add("DeflectorMass rows need positive PitchDiameterMm and MassKg");
                else if (DeflectorMass.Select(r => r.PitchDiameterMm).Distinct().Count() != DeflectorMass.Count)
                    p.Add("DeflectorMass pitch diameters must be unique");
            }
            if (!TractionLimitingMethods.All.Contains(TractionLimitingMethod))
                p.Add("TractionLimitingMethod must be one of " + String.Join(", ", TractionLimitingMethods.All));
            ValidateProject(p);
            return p;
        }

        static bool Positive(double? v)
        {
            return v.HasValue && Positive(v.Value);
        }

        static bool Finite(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value);
        }

        void ValidateProject(List<string> p)
        {
            var n = NewProject;
            if (n == null)
                p.Add("NewProject is required");
            else
            {
                if (!Positive(n.ShaftWidthMm) || !Positive(n.ShaftDepthMm) || !Positive(n.PitDepthMm) || !Positive(n.OverheadMm))
                    p.Add("NewProject shaft dimensions must be positive");
                if (!n.Stops.HasValue || n.Stops.Value < 2)
                    p.Add("NewProject.Stops must be at least 2");
                if (!Positive(n.FloorHeightMm))
                    p.Add("NewProject.FloorHeightMm must be positive");
                if (!Positive(n.CapacityKg))
                    p.Add("NewProject.CapacityKg must be positive");
                if (!Positive(n.SpeedMps))
                    p.Add("NewProject.SpeedMps must be positive");
            }
            if (!Positive(PersonMassKg))
                p.Add("PersonMassKg must be positive");
            if (!Finite(CounterweightBalanceFactor) || CounterweightBalanceFactor.Value < 0 || CounterweightBalanceFactor.Value > 1)
                p.Add("CounterweightBalanceFactor must be 0–1");
            if (CapacityTable == null || CapacityTable.Count < 2)
                p.Add("CapacityTable needs at least two rows");
            else if (CapacityTable.Any(r => r == null || !Positive(r.CapacityKg) || !Positive(r.CarMassKg) || !Positive(r.MachineMassKg)))
                p.Add("CapacityTable rows need positive CapacityKg, CarMassKg and MachineMassKg");
            else if (CapacityTable.Select(r => r.CapacityKg).Distinct().Count() != CapacityTable.Count)
                p.Add("CapacityTable capacities must be unique");
            if (!MaximumRopeCount.HasValue || MaximumRopeCount.Value < 4 || MaximumRopeCount.Value > 7)
                p.Add("MaximumRopeCount must be 4–7 (project rope rule)");
            if (RopeLadder == null || RopeLadder.Count == 0)
                p.Add("RopeLadder is required");
            else if (RopeLadder.Any(r => r == null))
                p.Add("RopeLadder rows are required");
            else
            {
                for (int i = 0; i < RopeLadder.Count; i++)
                {
                    var r = RopeLadder[i];
                    bool last = i == RopeLadder.Count - 1;
                    if (last ? r.MaxCapacityKg.HasValue : !Positive(r.MaxCapacityKg))
                        p.Add("RopeLadder: every row except the last needs a positive MaxCapacityKg; the last row has none");
                    if (i > 0
                        && !last
                        && RopeLadder[i - 1].MaxCapacityKg.HasValue
                        && r.MaxCapacityKg.HasValue
                        && r.MaxCapacityKg.Value <= RopeLadder[i - 1].MaxCapacityKg.Value)
                        p.Add("RopeLadder MaxCapacityKg must increase");
                    if (r.RopeCount < 4 || (MaximumRopeCount.HasValue && r.RopeCount > MaximumRopeCount.Value))
                        p.Add("RopeLadder RopeCount must be 4 to MaximumRopeCount");
                    if (Rope(r.DiameterMm) == null)
                        p.Add("RopeLadder diameter " + r.DiameterMm.ToString(CultureInfo.InvariantCulture) + " mm is not in Ropes.Table");
                }
            }
            if (!Positive(TractionSheavePitchDiameterMm))
                p.Add("TractionSheavePitchDiameterMm must be positive");
            if (TractionSheavePitchSteps == null || TractionSheavePitchSteps.Count == 0 || TractionSheavePitchSteps.Any(v => !Positive(v)))
                p.Add("TractionSheavePitchSteps must be positive");
            else if (TractionSheavePitchSteps.Zip(TractionSheavePitchSteps.Skip(1), (a, b) => b > a).Any(ok => !ok))
                p.Add("TractionSheavePitchSteps must increase");
            else if (TractionSheavePitchDiameterMm.HasValue && !TractionSheavePitchSteps.Contains(TractionSheavePitchDiameterMm.Value))
                p.Add("TractionSheavePitchSteps must include TractionSheavePitchDiameterMm");
            if (UndercutAngleStepsDeg == null || UndercutAngleStepsDeg.Count == 0)
                p.Add("UndercutAngleStepsDeg is required");
            else if (UndercutAngleStepsDeg.Zip(UndercutAngleStepsDeg.Skip(1), (x, y) => y > x).Any(ok => !ok))
                p.Add("UndercutAngleStepsDeg must increase");
            else if (Groove != null
                && (!Groove.UndercutAngleDeg.HasValue || !UndercutAngleStepsDeg.Contains(Groove.UndercutAngleDeg.Value)))
                p.Add("UndercutAngleStepsDeg must include the groove undercut angle");
            else if (Groove != null
                && UndercutAngleStepsDeg.Any(v => GrooveTypes.AngleProblem(Groove.Type, v, Groove.GrooveAngleDeg) != null))
                p.Add("UndercutAngleStepsDeg values must be valid undercut angles (0–105°)");
            if (BalanceFactorSteps == null || BalanceFactorSteps.Count == 0)
                p.Add("BalanceFactorSteps is required");
            else if (BalanceFactorSteps.Any(v => Double.IsNaN(v) || v <= 0 || v > 1))
                p.Add("BalanceFactorSteps values must be in (0, 1]");
            else if (CounterweightBalanceFactor.HasValue
                && !BalanceFactorSteps.Any(v => Math.Abs(v - CounterweightBalanceFactor.Value) < 1e-12))
                p.Add("BalanceFactorSteps must include CounterweightBalanceFactor");
            else if (BalanceFactorSteps.Distinct().Count() != BalanceFactorSteps.Count)
                p.Add("BalanceFactorSteps must be unique");
            if (!Positive(GuideShoeSpacingMm))
                p.Add("GuideShoeSpacingMm must be positive");
            if (!Positive(BracketMaximumSpacingMm))
                p.Add("BracketMaximumSpacingMm must be positive");
            if (SafetyGear == null || !Positive(SafetyGear.ProgressiveAboveSpeedMps) || SafetyGear.CarGuideCount != 2)
                p.Add("SafetyGear needs a positive ProgressiveAboveSpeedMps and CarGuideCount 2");
            if (Buffers == null
                || !Positive(Buffers.EnergyAccumulationUpToSpeedMps)
                || Buffers.CarCount < 1
                || Buffers.CarCount > 4
                || Buffers.CounterweightCount < 1
                || Buffers.CounterweightCount > 4
                || Buffers.Sharing != "EqualSharing")
                p.Add("Buffers need a positive EnergyAccumulationUpToSpeedMps, counts 1–4 and Sharing EqualSharing");
            var adv = Advanced;
            if (adv == null)
                p.Add("Advanced is required");
            else
            {
                if (!Finite(adv.MachineCgOffsetXMm)
                    || !Finite(adv.MachineCgOffsetYMm)
                    || !Finite(adv.MachineCgOffsetZMm)
                    || !Finite(adv.HangerAttachmentOffsetXMm)
                    || !Finite(adv.HangerAttachmentOffsetYMm))
                    p.Add("Advanced offsets must be finite");
                if (!Finite(adv.AdditionalMassKg) || adv.AdditionalMassKg.Value < 0)
                    p.Add("Advanced.AdditionalMassKg must be zero or positive");
                if (adv.TransferModel != Elevator.Core.Calculations.ChassisReactionSolver.Model)
                    p.Add("Advanced.TransferModel must be " + Elevator.Core.Calculations.ChassisReactionSolver.Model);
                if (adv.LoadCase != "StaticRatedLoad1To1")
                    p.Add("Advanced.LoadCase must be StaticRatedLoad1To1");
                if (String.IsNullOrWhiteSpace(adv.MachineCgBasis)
                    || String.IsNullOrWhiteSpace(adv.TransferBasis)
                    || String.IsNullOrWhiteSpace(adv.HangerAttachmentBasis)
                    || String.IsNullOrWhiteSpace(adv.ExcludedWeightBasis))
                    p.Add("Advanced basis texts are required");
            }
        }

        // Car and machine mass from the capacity table: linear interpolation, clamped to the end rows.
        public CapacityPresetRow MassesFor(double? capacityKg)
        {
            if (!capacityKg.HasValue || CapacityTable == null || CapacityTable.Count == 0)
                return null;
            var rows = CapacityTable.OrderBy(r => r.CapacityKg).ToList();
            double q = capacityKg.Value;
            if (q <= rows[0].CapacityKg)
                return new CapacityPresetRow { CapacityKg = q, CarMassKg = rows[0].CarMassKg, MachineMassKg = rows[0].MachineMassKg };
            var top = rows[rows.Count - 1];
            if (q >= top.CapacityKg)
                return new CapacityPresetRow { CapacityKg = q, CarMassKg = top.CarMassKg, MachineMassKg = top.MachineMassKg };
            for (int i = 0; i + 1 < rows.Count; i++)
                if (q <= rows[i + 1].CapacityKg)
                {
                    var a = rows[i];
                    var b = rows[i + 1];
                    double t = (q - a.CapacityKg) / (b.CapacityKg - a.CapacityKg);
                    return new CapacityPresetRow
                    {
                        CapacityKg = q,
                        CarMassKg = a.CarMassKg + (b.CarMassKg - a.CarMassKg) * t,
                        MachineMassKg = a.MachineMassKg + (b.MachineMassKg - a.MachineMassKg) * t
                    };
                }
            return null;
        }

        public RopeLadderRow RopesFor(double? capacityKg)
        {
            if (!capacityKg.HasValue || RopeLadder == null || RopeLadder.Count == 0)
                return null;
            return RopeLadder.FirstOrDefault(r => !r.MaxCapacityKg.HasValue || capacityKg.Value <= r.MaxCapacityKg.Value + 1e-9);
        }

        public int? PersonsFor(double? capacityKg)
        {
            return capacityKg.HasValue && Positive(PersonMassKg) && capacityKg.Value >= 0
                ? (int?)(int)Math.Floor(capacityKg.Value / PersonMassKg.Value + 1e-9)
                : null;
        }

        public double? CounterweightFor(double? carMassKg, double? capacityKg)
        {
            return carMassKg.HasValue && capacityKg.HasValue && CounterweightBalanceFactor.HasValue
                ? carMassKg.Value + CounterweightBalanceFactor.Value * capacityKg.Value
                : (double?)null;
        }

        public string Hash()
        {
            return Elevator.Core.Calculations.CalculationFingerprint.Hash(this);
        }

        // Rope data follow the rope diameter by exact table match only; an unlisted diameter has no preset.
        public RopeRow Rope(double? diameterMm)
        {
            if (!diameterMm.HasValue || Ropes == null || Ropes.Table == null)
                return null;
            return Ropes.Table.FirstOrDefault(r => Math.Abs(r.DiameterMm - diameterMm.Value) < 1e-9);
        }

        // Linear interpolation in the deflector table; outside the table the nearest end value is used.
        public double? DeflectorMassFor(double? pitchMm)
        {
            if (!pitchMm.HasValue || DeflectorMass == null || DeflectorMass.Count == 0)
                return null;
            var rows = DeflectorMass.OrderBy(r => r.PitchDiameterMm).ToList();
            double x = pitchMm.Value;
            if (x <= rows[0].PitchDiameterMm)
                return rows[0].MassKg;
            if (x >= rows[rows.Count - 1].PitchDiameterMm)
                return rows[rows.Count - 1].MassKg;
            for (int i = 0; i + 1 < rows.Count; i++)
                if (x <= rows[i + 1].PitchDiameterMm)
                {
                    var a = rows[i];
                    var b = rows[i + 1];
                    return a.MassKg + (b.MassKg - a.MassKg) * (x - a.PitchDiameterMm) / (b.PitchDiameterMm - a.PitchDiameterMm);
                }
            return null;
        }
    }

    public sealed class RopePresetTable
    {
        public string Designation;
        public List<RopeRow> Table;
    }

    public sealed class RopeRow
    {
        public double DiameterMm;
        public double MinimumBreakingLoadKn;
        public double MassKgPerM;
    }

    public sealed class GroovePreset
    {
        public string Type;
        public double? UndercutAngleDeg;
        public double? GrooveAngleDeg;
    }

    public sealed class DeflectorMassRow
    {
        public double PitchDiameterMm;
        public double MassKg;
    }

    public sealed class NewProjectPreset
    {
        public double? ShaftWidthMm;
        public double? ShaftDepthMm;
        public double? PitDepthMm;
        public double? OverheadMm;
        public double? FloorHeightMm;
        public double? CapacityKg;
        public double? SpeedMps;
        public int? Stops;
    }

    public sealed class CapacityPresetRow
    {
        public double CapacityKg;
        public double CarMassKg;
        public double MachineMassKg;
    }

    public sealed class RopeLadderRow
    {
        public double? MaxCapacityKg;
        public int RopeCount;
        public double DiameterMm;
    }

    public sealed class SafetyGearPreset
    {
        public double? ProgressiveAboveSpeedMps;
        public int CarGuideCount;
    }

    public sealed class BufferPreset
    {
        public double? EnergyAccumulationUpToSpeedMps;
        public int CarCount;
        public int CounterweightCount;
        public string Sharing;
    }

    public sealed class AdvancedPreset
    {
        public double? MachineCgOffsetXMm;
        public double? MachineCgOffsetYMm;
        public double? MachineCgOffsetZMm;
        public double? HangerAttachmentOffsetXMm;
        public double? HangerAttachmentOffsetYMm;
        public double? AdditionalMassKg;
        public string LoadCase;
        public string TransferModel;
        public string MachineCgBasis;
        public string TransferBasis;
        public string HangerAttachmentBasis;
        public string ExcludedWeightBasis;
    }

    // Traction sheave groove types of EN 81-50:2014 5.11.2.3.1.
    public static class GrooveTypes
    {
        public const string UndercutU = "UndercutU";
        public const string VHardened = "VHardened";
        public const string VUnhardened = "VUnhardened";
        public static readonly string[] All = { UndercutU, VHardened, VUnhardened };

        public static string Label(string type)
        {
            return type == UndercutU
                ? "Semi-circular / undercut"
                : type == VHardened ? "V-groove, hardened" : type == VUnhardened ? "V-groove, unhardened" : type;
        }

        public static bool NeedsUndercut(string type)
        {
            return type == UndercutU || type == VUnhardened;
        }

        public static bool NeedsGrooveAngle(string type)
        {
            return type == UndercutU || type == VHardened || type == VUnhardened;
        }

        // Limits of EN 81-50:2014 5.11.2.3.1: β ≤ 105°; semi-circular γ ≥ 25°; V-groove γ ≥ 35° for lifts.
        public static string AngleProblem(string type, double? beta, double? gamma)
        {
            if (!All.Contains(type))
                return "groove type must be one of " + String.Join(", ", All);
            if (NeedsUndercut(type))
            {
                if (!beta.HasValue)
                    return "undercut angle β required";
                if (Double.IsNaN(beta.Value) || beta.Value < 0 || beta.Value > 105)
                    return "undercut angle β must be 0–105°";
                if (type == VUnhardened && beta.Value <= 0)
                    return "an unhardened V-groove needs an undercut (β > 0)";
            }
            if (NeedsGrooveAngle(type))
            {
                if (!gamma.HasValue)
                    return "groove angle γ required";
                double min = type == UndercutU ? 25 : 35;
                if (Double.IsNaN(gamma.Value) || gamma.Value < min || gamma.Value >= 180)
                    return "groove angle γ must be at least " + min.ToString(CultureInfo.InvariantCulture) + "° and below 180°";
            }
            return null;
        }
    }

    // EN 81-20:2014 5.5.3 c): stalled-condition protection by rope slip (1) or by an electric safety device (2).
    public static class TractionLimitingMethods
    {
        public const string RopesSlip = "RopesSlip";
        public const string ElectricSafetyDevice = "ElectricSafetyDevice";
        public static readonly string[] All = { RopesSlip, ElectricSafetyDevice };

        public static string Label(string m)
        {
            return m == RopesSlip
                ? "Ropes slip (EN 81-20 5.5.3 c) 1))"
                : m == ElectricSafetyDevice ? "Electric safety device (EN 81-20 5.5.3 c) 2))" : m;
        }
    }

    public static class TractionRules
    {
        public const double MinimumBrakingRetardationMps2 = 0.5;
    }

    // Generic preset / lock / follow mechanism (DESIGN-FLOW principles 2–4). A field is locked once the user edits it
    // (its key is listed in ElevatorProject.UserEditedFields); an unlocked field always takes its preset, which may
    // follow an upstream driver. Reset removes the lock. Older projects have no list: every field is unlocked.
    public sealed class PresetContext
    {
        public ElevatorProject Project;
        public PresetLibrary Library;
        public double? DeflectorPitchMm; // driver for the deflector mass preset (from the shared deflector rule)
    }

    public sealed class PresetField
    {
        public string Key;
        public string Label;
        public Func<PresetContext, object> Preset;
        public Func<ElevatorProject, object> Stored;
        public Action<ElevatorProject, object> Store;

        // Materialized fields (stage 2) are ordinary project values that ProjectPresets.Apply writes while unlocked.
        // A project without FollowsPresets (older files) treats every materialized field as edited: it opens unchanged.
        public bool Materialized;
        public string Page;

        public bool IsEdited(ElevatorProject p)
        {
            if (p == null)
                return false;
            if (Materialized && p.FollowsPresets != true)
                return true;
            return p.UserEditedFields != null && p.UserEditedFields.Contains(Key);
        }

        public bool IsBlank(ElevatorProject p)
        {
            var v = Stored(p);
            if (v == null)
                return true;
            if (v is string)
                return String.IsNullOrWhiteSpace((string)v);
            if (v is double && (Key == "Shaft.WidthMm" || Key == "Shaft.DepthMm"))
                return (double)v <= 0; // non-nullable shaft size: 0 is blank
            return false;
        }

        public object PresetValue(PresetContext c)
        {
            return c == null || c.Library == null ? null : Preset(c);
        }

        public object Effective(PresetContext c)
        {
            return IsEdited(c.Project) ? Stored(c.Project) : PresetValue(c);
        }

        public void Edit(ElevatorProject p, object value)
        {
            Store(p, value);
            if (p.UserEditedFields == null)
                p.UserEditedFields = new List<string>();
            if (!p.UserEditedFields.Contains(Key))
                p.UserEditedFields.Add(Key);
        }

        // Materialized fields: the caller runs ProjectPresets.Apply afterwards to write the preset.
        public void Reset(ElevatorProject p)
        {
            if (Materialized)
            {
                ProjectPresets.Unlock(p, new[] { Key });
                return;
            }
            if (p.UserEditedFields != null)
            {
                p.UserEditedFields.Remove(Key);
                if (p.UserEditedFields.Count == 0)
                    p.UserEditedFields = null;
            }
            Store(p, null);
        }
    }

    public static class PresetFields
    {
        static Elevator.Core.Calculations.RopeTractionInputs Group(ElevatorProject p)
        {
            if (p.Calculations == null)
                p.Calculations = new Elevator.Core.Calculations.ChassisCalculationInputs();
            if (p.Calculations.RopeTraction == null)
                p.Calculations.RopeTraction = new Elevator.Core.Calculations.RopeTractionInputs();
            return p.Calculations.RopeTraction;
        }

        static Elevator.Core.Calculations.RopeTractionInputs Read(ElevatorProject p)
        {
            return p == null || p.Calculations == null ? null : p.Calculations.RopeTraction;
        }

        static PresetField Number(
            string name,
            string label,
            Func<PresetContext, double?> preset,
            Func<Elevator.Core.Calculations.RopeTractionInputs, double?> get,
            Action<Elevator.Core.Calculations.RopeTractionInputs, double?> set)
        {
            return new PresetField
            {
                Key = "Calculations.RopeTraction." + name,
                Label = label,
                Preset = c => preset(c),
                Stored = p => { var g = Read(p); return g == null ? null : get(g); },
                Store = (p, v) => set(Group(p), v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture))
            };
        }

        static PresetField Text(
            string name,
            string label,
            Func<PresetContext, string> preset,
            Func<Elevator.Core.Calculations.RopeTractionInputs, string> get,
            Action<Elevator.Core.Calculations.RopeTractionInputs, string> set)
        {
            return new PresetField
            {
                Key = "Calculations.RopeTraction." + name,
                Label = label,
                Preset = c => preset(c),
                Stored = p => { var g = Read(p); return g == null ? null : get(g); },
                Store = (p, v) => set(Group(p), v as string)
            };
        }

        static double? RopeDiameter(PresetContext c)
        {
            return c.Project == null || c.Project.Suspension == null ? null : c.Project.Suspension.RopeDiameterMm;
        }

        public static readonly PresetField RopeDesignation = Text(
            "RopeDesignation",
            "Rope designation",
            c => c.Library.Ropes.Designation,
            g => g.RopeDesignation,
            (g, v) => g.RopeDesignation = v);

        public static readonly PresetField MinimumBreakingLoad = Number(
            "MinimumBreakingLoadKn",
            "Rope minimum breaking load MBL per rope (kN)",
            c => { var r = c.Library.Rope(RopeDiameter(c)); return r == null ? (double?)null : r.MinimumBreakingLoadKn; },
            g => g.MinimumBreakingLoadKn,
            (g, v) => g.MinimumBreakingLoadKn = v);

        public static readonly PresetField RopeMass = Number(
            "RopeMassKgPerM",
            "Rope mass per metre (kg/m)",
            c => { var r = c.Library.Rope(RopeDiameter(c)); return r == null ? (double?)null : r.MassKgPerM; },
            g => g.RopeMassKgPerM,
            (g, v) => g.RopeMassKgPerM = v);

        public static readonly PresetField GrooveType =
            Text("GrooveType", "Traction sheave groove", c => c.Library.Groove.Type, g => g.GrooveType, (g, v) => g.GrooveType = v);

        public static readonly PresetField UndercutAngle = Number(
            "UndercutAngleDeg",
            "Undercut angle β (°)",
            c =>
            {
                var g = Read(c.Project);
                return g != null && g.SelectedUndercutAngleDeg.HasValue
                ? g.SelectedUndercutAngleDeg
                : c.Library.Groove.UndercutAngleDeg;
            },
            g => g.UndercutAngleDeg,
            (g, v) => g.UndercutAngleDeg = v);

        public static readonly PresetField GrooveAngle = Number(
            "GrooveAngleDeg",
            "Groove angle γ (°)",
            c => c.Library.Groove.GrooveAngleDeg,
            g => g.GrooveAngleDeg,
            (g, v) => g.GrooveAngleDeg = v);

        public static readonly PresetField TravellingCable = Number(
            "TravellingCableKgPerM",
            "Travelling cable mass per metre, all cables (kg/m)",
            c => c.Library.TravellingCableKgPerM,
            g => g.TravellingCableKgPerM,
            (g, v) => g.TravellingCableKgPerM = v);

        public static readonly PresetField BrakingRetardation = Number(
            "BrakingRetardationMps2",
            "Emergency braking retardation a (m/s²)",
            c => c.Library.BrakingRetardationMps2,
            g => g.BrakingRetardationMps2,
            (g, v) => g.BrakingRetardationMps2 = v);

        public static readonly PresetField DeflectorMass = Number(
            "DeflectorMassKg",
            "Deflector sheave mass (kg)",
            c => c.Library.DeflectorMassFor(c.DeflectorPitchMm),
            g => g.DeflectorMassKg,
            (g, v) => g.DeflectorMassKg = v);

        public static readonly PresetField TractionLimitingMethod = Text(
            "TractionLimitingMethod",
            "Traction limiting method",
            c => c.Library.TractionLimitingMethod,
            g => g.TractionLimitingMethod,
            (g, v) => g.TractionLimitingMethod = v);

        public static readonly PresetField[] RopeTraction = { RopeDesignation,
            MinimumBreakingLoad,
            RopeMass,
            GrooveType,
            UndercutAngle,
            GrooveAngle,
            TravellingCable,
            BrakingRetardation,
            DeflectorMass,
            TractionLimitingMethod };

        public static PresetField Find(string key)
        {
            return RopeTraction.FirstOrDefault(f => f.Key == key);
        }
    }
}
