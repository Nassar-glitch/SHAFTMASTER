using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Doors;
using Elevator.Core.MachineRoom;
using Elevator.Core.Serialization;
using Elevator.Core.Suspension;

namespace Elevator.Core.Presets
{
    /// <summary>
    /// Editable preset library (config/presets.json, shipped beside the application). Company practice changes here, not in code. Values
    /// are design defaults approved in DESIGN-FLOW.md; they are never standard factors.
    /// </summary>
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

        /// <summary>The door catalog (DESIGN-FLOW "Doors"); null in an older library: the shipped default catalog applies.</summary>
        public DoorCatalog Doors;

        /// <summary>
        /// The company name used by the drawing title block and the file names (DESIGN-FLOW "Layouts and PDF"); null in an older library:
        /// the shipped default applies. Not a calculation input: it never enters a fingerprint.
        /// </summary>
        public string CompanyName;

        /// <summary>The title-block presets (control, supply); null in an older library: the shipped defaults apply.</summary>
        public TitleBlockPreset TitleBlock;

        /// <summary>The shipped company name.</summary>
        public const string DefaultCompanyName = "ELEVATORS COMPANY";

        /// <summary>The company name of the library, or the shipped default.</summary>
        public string CompanyNameOrDefault()
        {
            return CompanyName ?? DefaultCompanyName;
        }

        /// <summary>The title-block presets of the library, or the shipped defaults.</summary>
        public TitleBlockPreset TitleBlockOrDefault()
        {
            return TitleBlock ?? TitleBlockPreset.Default();
        }

        /// <summary>The door catalog of the library, or the shipped default.</summary>
        public DoorCatalog DoorsOrDefault()
        {
            return Doors ?? DoorCatalog.Default();
        }

        /// <summary>Reads and validates the preset library from a file.</summary>
        public static PresetLibrary Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Preset library not found: " + path, path);
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Reads and validates the preset library from JSON text.</summary>
        public static PresetLibrary Parse(string json)
        {
            PresetLibrary library;
            try
            {
                library = CoreJson.CreateWithDefaultLimit().Deserialize<PresetLibrary>(json);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Preset library is not valid JSON: " + e.Message);
            }
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

        /// <summary>The problems of the library; empty when it is valid.</summary>
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
                {
                    if (r == null || !Positive(r.DiameterMm) || !Positive(r.MinimumBreakingLoadKn) || !Positive(r.MassKgPerM))
                        p.Add("Ropes.Table rows need positive DiameterMm, MinimumBreakingLoadKn and MassKgPerM");
                }
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
            if (Doors != null)
                p.AddRange(Doors.Validate());
            if (CompanyName != null && String.IsNullOrWhiteSpace(CompanyName))
                p.Add("CompanyName must not be blank");
            if (TitleBlock != null && (TitleBlock.Control == null || TitleBlock.Supply == null))
                p.Add("TitleBlock needs Control and Supply");
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
            ValidateNewProject(p);
            ValidateCapacityAndBalance(p);
            ValidateRopeLadder(p);
            ValidateSheaveSteps(p);
            ValidateUndercutSteps(p);
            ValidateBalanceSteps(p);
            ValidateGuidesAndBuffers(p);
            ValidateAdvanced(p);
        }

        private void ValidateNewProject(List<string> p)
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
        }

        private void ValidateCapacityAndBalance(List<string> p)
        {
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
        }

        private void ValidateRopeLadder(List<string> p)
        {
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
        }

        private void ValidateSheaveSteps(List<string> p)
        {
            if (!Positive(TractionSheavePitchDiameterMm))
                p.Add("TractionSheavePitchDiameterMm must be positive");
            if (TractionSheavePitchSteps == null || TractionSheavePitchSteps.Count == 0 || TractionSheavePitchSteps.Any(v => !Positive(v)))
                p.Add("TractionSheavePitchSteps must be positive");
            else if (TractionSheavePitchSteps.Zip(TractionSheavePitchSteps.Skip(1), (a, b) => b > a).Any(ok => !ok))
                p.Add("TractionSheavePitchSteps must increase");
            else if (TractionSheavePitchDiameterMm.HasValue && !TractionSheavePitchSteps.Contains(TractionSheavePitchDiameterMm.Value))
                p.Add("TractionSheavePitchSteps must include TractionSheavePitchDiameterMm");
        }

        private void ValidateUndercutSteps(List<string> p)
        {
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
        }

        private void ValidateBalanceSteps(List<string> p)
        {
            if (BalanceFactorSteps == null || BalanceFactorSteps.Count == 0)
                p.Add("BalanceFactorSteps is required");
            else if (BalanceFactorSteps.Any(v => Double.IsNaN(v) || v <= 0 || v > 1))
                p.Add("BalanceFactorSteps values must be in (0, 1]");
            else if (CounterweightBalanceFactor.HasValue
                && !BalanceFactorSteps.Any(v => Math.Abs(v - CounterweightBalanceFactor.Value) < 1e-12))
                p.Add("BalanceFactorSteps must include CounterweightBalanceFactor");
            else if (BalanceFactorSteps.Distinct().Count() != BalanceFactorSteps.Count)
                p.Add("BalanceFactorSteps must be unique");
        }

        private void ValidateGuidesAndBuffers(List<string> p)
        {
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
        }

        private void ValidateAdvanced(List<string> p)
        {
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
                if (adv.TransferModel != ChassisReactionSolver.Model)
                    p.Add("Advanced.TransferModel must be " + ChassisReactionSolver.Model);
                if (adv.LoadCase != "StaticRatedLoad1To1")
                    p.Add("Advanced.LoadCase must be StaticRatedLoad1To1");
                if (String.IsNullOrWhiteSpace(adv.MachineCgBasis)
                    || String.IsNullOrWhiteSpace(adv.TransferBasis)
                    || String.IsNullOrWhiteSpace(adv.HangerAttachmentBasis)
                    || String.IsNullOrWhiteSpace(adv.ExcludedWeightBasis))
                    p.Add("Advanced basis texts are required");
            }
        }

        /// <summary>Car and machine mass from the capacity table: linear interpolation, clamped to the end rows.</summary>
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
            {
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
            }
            return null;
        }

        /// <summary>The rope preset row for a rated load.</summary>
        public RopeLadderRow RopesFor(double? capacityKg)
        {
            if (!capacityKg.HasValue || RopeLadder == null || RopeLadder.Count == 0)
                return null;
            return RopeLadder.FirstOrDefault(r => !r.MaxCapacityKg.HasValue || capacityKg.Value <= r.MaxCapacityKg.Value + 1e-9);
        }

        /// <summary>The number of persons for a rated load.</summary>
        public int? PersonsFor(double? capacityKg)
        {
            return capacityKg.HasValue && Positive(PersonMassKg) && capacityKg.Value >= 0
                ? (int?)(int)Math.Floor(capacityKg.Value / PersonMassKg.Value + 1e-9)
                : null;
        }

        /// <summary>The counterweight mass from the car mass and the rated load.</summary>
        public double? CounterweightFor(double? carMassKg, double? capacityKg)
        {
            return carMassKg.HasValue && capacityKg.HasValue && CounterweightBalanceFactor.HasValue
                ? carMassKg.Value + CounterweightBalanceFactor.Value * capacityKg.Value
                : (double?)null;
        }

        // The library is read-only after it is loaded; every rope trial and every fingerprint asks for its hash.
        string hash;

        /// <summary>Fingerprint of the library (computed once per loaded library).</summary>
        public string Hash()
        {
            var value = hash;
            if (value == null)
            {
                value = CalculationFingerprint.HashWithout(this, "Doors", "CompanyName", "TitleBlock");
                hash = value;
            }
            return value;
        }

        /// <summary>Rope data follow the rope diameter by exact table match only; an unlisted diameter has no preset.</summary>
        public RopeRow Rope(double? diameterMm)
        {
            if (!diameterMm.HasValue || Ropes == null || Ropes.Table == null)
                return null;
            return Ropes.Table.FirstOrDefault(r => Math.Abs(r.DiameterMm - diameterMm.Value) < 1e-9);
        }

        /// <summary>Linear interpolation in the deflector table; outside the table the nearest end value is used.</summary>
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
            {
                if (x <= rows[i + 1].PitchDiameterMm)
                {
                    var a = rows[i];
                    var b = rows[i + 1];
                    return a.MassKg + (b.MassKg - a.MassKg) * (x - a.PitchDiameterMm) / (b.PitchDiameterMm - a.PitchDiameterMm);
                }
            }
            return null;
        }
    }
}
