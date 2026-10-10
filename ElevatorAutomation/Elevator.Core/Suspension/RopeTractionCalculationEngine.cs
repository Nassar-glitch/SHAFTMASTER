using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Suspension
{
    /// <summary>
    /// Rope and traction checks: rope diameter and count, D/d, safety factor and the traction conditions (EN 81-20, EN 81-50).
    /// </summary>
    public sealed class RopeTractionCalculationEngine
    {
        /// <summary>Rule version stored in results and fingerprints.</summary>
        public const string RuleVersion = "EN81-20-5.5_EN81-50-5.11-5.12_RopeTraction-v1";
        /// <summary>Standard acceleration of free fall gn, m/s² (EN 81-50 5.11.3).</summary>
        public const double Gn = 9.81; // EN 81-50:2014 5.11.3: gn standard acceleration of free fall (m/s²)
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string F(double v)
        {
            return v.ToString("0.###", Inv);
        }

        static string F2(double v)
        {
            return v.ToString("0.##", Inv);
        }

        static bool Positive(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value) && v.Value > 0;
        }

        // ---------- shared geometry ----------
        /// <summary>The deflector pitch diameter selected from TZ, or null when there is none.</summary>
        public static double? DeflectorPitch(ElevatorProject p, DesignContext c)
        {
            try
            {
                if (p == null
                    || p.Shaft == null
                    || c == null
                    || c.Equipment == null
                    || !Positive(p.Shaft.WidthMm)
                    || !Positive(p.Shaft.DepthMm))
                    return null;
                var plan = DesignRules.Calculate(
                    p.Shaft.WidthMm,
                    p.Shaft.DepthMm,
                    c.Equipment,
                    c.CounterweightDepthMm,
                    Doors.DoorInput.From(p, c.Doors));
                var d = MachineRoomRules.SelectDeflector(plan.TZ);
                return d.IsRequired ? d.PitchDiameterMm : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        // The design engine run behind the geometry is the most expensive step of a rope trial and depends only on the project and the
        // context: results are kept by project fingerprint (bounded, locked), valid for the same context object.
        const int GeometryCacheLimit = 256;
        static readonly object geometryGate = new object();
        static readonly Dictionary<string, KeyValuePair<DesignContext, RopeTractionGeometry>> geometryCache =
            new Dictionary<string, KeyValuePair<DesignContext, RopeTractionGeometry>>();

        /// <summary>The shared geometry for the rope and traction checks.</summary>
        public static RopeTractionGeometry Geometry(ElevatorProject p, DesignContext c)
        {
            string key = p == null || c == null ? null : CalculationFingerprint.Hash(p);
            if (key != null)
            {
                lock (geometryGate)
                {
                    KeyValuePair<DesignContext, RopeTractionGeometry> entry;
                    if (geometryCache.TryGetValue(key, out entry) && Object.ReferenceEquals(c, entry.Key))
                        return entry.Value.Copy();
                }
            }
            var g = ComputeGeometry(p, c);
            if (key != null)
            {
                lock (geometryGate)
                {
                    if (geometryCache.Count >= GeometryCacheLimit)
                        geometryCache.Clear();
                    geometryCache[key] = new KeyValuePair<DesignContext, RopeTractionGeometry>(c, g.Copy());
                }
            }
            return g;
        }

        static RopeTractionGeometry ComputeGeometry(ElevatorProject p, DesignContext c)
        {
            var g = new RopeTractionGeometry();
            try
            {
                if (p == null || p.Building == null)
                    throw new ArgumentException("Floors are missing.");
                var served = ProjectFloors.Elevations(p.Building).Where(f => f.IsServed).ToList();
                if (served.Count < 2)
                    throw new ArgumentException("At least two served floors are required for the travel height.");
                g.TravelMm = served.Last().ElevationMm - served.First().ElevationMm;
                var design = new ElevatorDesignEngine().Calculate(p, c, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB);
                if (design.Plan == null || design.Plan.MachineRoom == null || design.Plan.MachineRoomSection == null)
                {
                    throw new ArgumentException(design.Validation.Where(v => v.BlocksGeneration).Select(v => v.Message).FirstOrDefault()
                                            ?? "Shared machine-room geometry unavailable.");
                }
                g.TzMm = design.Plan.TZ;
                g.CarWidthMm = design.Plan.CarWidth;
                g.CarDepthMm = design.Plan.CarDepth;
                var selection = design.Plan.MachineRoom.Deflector;
                var s = design.Plan.MachineRoomSection;
                g.DeflectorPresent = s.DeflectorVisible;
                if (g.DeflectorPresent)
                {
                    // Pitch only when selected by the project deflector rule; a retained template size is not a selection.
                    g.DeflectorPitchMm = selection.PitchDiameterMm;
                    g.DeflectorPathValid = s.HasCalculatedPath
                        && selection.PitchDiameterMm.HasValue
                        && s.Arrangement != null
                        && s.Arrangement.CalculatedWrapAngleDeg.HasValue;
                    if (g.DeflectorPathValid)
                        g.WrapAngleDeg = s.Arrangement.CalculatedWrapAngleDeg.Value;
                }
                else
                    g.WrapAngleDeg = 180; // Planner decision A3: direct roping, α = 180°.
                g.Available = true;
            }
            catch (ArgumentException e)
            {
                g.Problem = e.Message;
            }
            catch (InvalidOperationException e)
            {
                g.Problem = e.Message;
            }
            return g;
        }

        // ---------- effective inputs (presets / locks) ----------
        /// <summary>The inputs used by the checks: project values with the presets applied.</summary>
        public static RopeTractionEffectiveInputs Effective(ElevatorProject p, PresetLibrary library, double? deflectorPitchMm)
        {
            var ctx = new PresetContext { Project = p, Library = library, DeflectorPitchMm = deflectorPitchMm };
            Func<PresetField, double?> num = f =>
            {
                var v = library == null && !f.IsEdited(p) ? null : f.Effective(ctx);
                return v == null
                ? (double?)null
                : Convert.ToDouble(v, Inv);
            };
            Func<PresetField, string> txt = f => (library == null && !f.IsEdited(p) ? null : f.Effective(ctx)) as string;
            var c = p.Calculations ?? new ChassisCalculationInputs();
            Func<double?, double?, double?> add = (a, b) => !a.HasValue ? (double?)null : a.Value + (Positive(b) ? b.Value : 0);
            return new RopeTractionEffectiveInputs
            {
                RopeDesignation = txt(PresetFields.RopeDesignation),
                MinimumBreakingLoadKn = num(PresetFields.MinimumBreakingLoad),
                RopeMassKgPerM = num(PresetFields.RopeMass),
                GrooveType = txt(PresetFields.GrooveType),
                UndercutAngleDeg = num(PresetFields.UndercutAngle),
                GrooveAngleDeg = num(PresetFields.GrooveAngle),
                TravellingCableKgPerM = num(PresetFields.TravellingCable),
                BrakingRetardationMps2 = num(PresetFields.BrakingRetardation),
                DeflectorMassKg = num(PresetFields.DeflectorMass),
                TractionLimitingMethod = txt(PresetFields.TractionLimitingMethod),
                RopeDiameterMm = p.Suspension == null ? null : p.Suspension.RopeDiameterMm,
                RopeCount = p.Suspension == null ? null : p.Suspension.RopeCount,
                RopingRatio = p.Suspension == null ? null : p.Suspension.RopingRatio,
                SheavePitchDiameterMm = p.Machine == null ? null : p.Machine.TractionSheavePitchDiameterMm,
                SpeedMps = p.Elevator == null ? null : p.Elevator.SpeedMetresPerSecond,
                CapacityKg = p.Elevator == null ? null : p.Elevator.CapacityKg,
                // Empty car P and counterweight include the optional suspended additions, as in the rope tensions section.
                CarMassKg = add(c.CarMassKg, c.CarSideAdditionalSuspendedMassKg),
                CounterweightMassKg = add(c.CounterweightMassKg, c.CounterweightSideAdditionalSuspendedMassKg),
                OverheadMm = p.Shaft == null ? null : p.Shaft.OverheadMm
            };
        }

        // ---------- currency ----------
        static string Fingerprint(ElevatorProject p, DesignContext c, PresetLibrary library)
        {
            if (p == null)
                return null;
            var rt = p.Calculations == null ? null : p.Calculations.RopeTraction;
            var locks = p.UserEditedFields == null ? new string[0] : p.UserEditedFields
                .Where(k => k.StartsWith("Calculations.RopeTraction."))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            var floors = p.Building == null || p.Building.Floors == null
                ? null
                : p.Building.Floors.Select(f => f == null ? null : (object)new { f.HeightToNextMm, f.IsServed }).ToArray();
            var cc = p.Calculations;
            string hash = CalculationFingerprint.Hash(new
            {
                Rule = RuleVersion,
                Library = library == null ? null : library.Hash(),
                Shaft = p.Shaft,
                p.Elevator,
                p.Suspension,
                p.Machine,
                p.MachineRoom,
                Floors = floors,
                Standards = p.Standards == null ? null : p.Standards.RuleSetId,
                Masses = cc == null ? null : new
                {
                    cc.CarMassKg,
                    cc.CounterweightMassKg,
                    cc.CarSideAdditionalSuspendedMassKg,
                    cc.CounterweightSideAdditionalSuspendedMassKg
                },
                RopeTraction = rt,
                Locks = locks,
                Context = c == null ? null : new
                {
                    c.Equipment,
                    c.CounterweightDepthMm,
                    c.RoomExtensions,
                    c.SectionReference,
                    c.FrontSupportDepthMm,
                    c.RearSupportOffsetMm,
                    c.RetainedDeflectorPitchMm,
                    c.RetainedDeflectorVisible
                }
            });
            return Doors.DoorFingerprint.Extend(hash, p, c);
        }

        /// <summary>True when the result matches the project, the context and the presets.</summary>
        public static bool IsCurrent(RopeTractionResult r, ElevatorProject p, DesignContext c, PresetLibrary library)
        {
            return r != null
                && r.RuleVersion == RuleVersion
                && r.InputFingerprint != null
                && r.InputFingerprint == Fingerprint(p, c, library);
        }

        // ---------- EN 81-50:2014 5.12 ----------
        // Table 2: Nequiv(t). V-grooves by γ, U-undercut grooves by β; U without undercut = 1. Linear interpolation within the
        // table (5.12.2.2). Planner decision A5: outside the table on the conservative side the edge value is used
        // (U β < 75° → value at 75°; V γ > 50° → value at 50°); the unsafe side is rejected by input validation.
        static readonly double[,] TableV = { { 35, 18.5 }, { 36, 16 }, { 38, 12 }, { 40, 10 }, { 42, 8 }, { 45, 6.5 }, { 50, 5 } };

        static readonly double[,] TableU =
            { { 75, 2.5 }, { 80, 3.0 }, { 85, 3.8 }, { 90, 5.0 }, { 95, 6.7 }, { 100, 10.0 }, { 105, 15.2 } };

        static double Interp(double[,] t, double x)
        {
            int n = t.GetLength(0);
            if (x <= t[0, 0])
                return t[0, 1];
            if (x >= t[n - 1, 0])
                return t[n - 1, 1];
            for (int i = 0; i + 1 < n; i++)
            {
                if (x <= t[i + 1, 0])
                    return t[i, 1] + (t[i + 1, 1] - t[i, 1]) * (x - t[i, 0]) / (t[i + 1, 0] - t[i, 0]);
            }
            return t[n - 1, 1];
        }

        /// <summary>Nequiv(t) of the groove (EN 81-50 Table 2).</summary>
        public static double NequivTraction(string groove, double? betaDeg, double? gammaDeg)
        {
            if (groove == GrooveTypes.UndercutU)
            {
                if (betaDeg.Value <= 0)
                    return 1; // 5.12.2.2: U-grooves without undercut
                return Interp(TableU, Math.Max(75, betaDeg.Value));
            }
            if (gammaDeg.Value < 35)
                throw new ArgumentException("V-groove angle below 35° is outside EN 81-50 Table 2.");
            return Interp(TableV, Math.Min(50, gammaDeg.Value));
        }

        /// <summary>5.12.2.3: Nequiv(p) = Kp·(Nps + 4·Npr), Kp = (Dt/Dp)^4.</summary>
        public static double KpFactor(double dt, double dp)
        {
            return Math.Pow(dt / dp, 4);
        }

        /// <summary>5.12.3, formula of Figure 10 (log = log10).</summary>
        public static double SafetyFactor8150(double dtOverDr, double nequiv)
        {
            double num = Math.Log10(695.85e6 * nequiv / Math.Pow(dtOverDr, 8.567));
            double den = Math.Log10(77.09 * Math.Pow(dtOverDr, -2.894));
            return Math.Pow(10, 2.6834 - num / den);
        }

        // ---------- EN 81-50:2014 5.11.2.3 friction factor ----------
        /// <summary>The traction cases of EN 81-50 5.11.2.2: loading, braking and stalled.</summary>
        public enum TractionCase
        {
            Loading,
            Braking,
            Stalled
        }

        /// <summary>The groove friction factor f for a traction case (EN 81-50 5.11.2.3.1).</summary>
        public static double FrictionFactor(string groove, TractionCase k, double mu, double? betaDeg, double? gammaDeg)
        {
            double beta = (betaDeg ?? 0) * Math.PI / 180, gamma = (gammaDeg ?? 0) * Math.PI / 180;
            if (groove == GrooveTypes.UndercutU) // 5.11.2.3.1.1
                return mu * 4 * (Math.Cos(gamma / 2) - Math.Sin(beta / 2)) / (Math.PI - beta - gamma - Math.Sin(beta) + Math.Sin(gamma));
            if (groove == GrooveTypes.VHardened || k == TractionCase.Stalled) // 5.11.2.3.1.2: hardened, and every V-groove when stalled
                return mu / Math.Sin(gamma / 2);
            return mu * 4 * (1 - Math.Sin(beta / 2)) / (Math.PI - beta - Math.Sin(beta)); // V-groove unhardened, loading / braking
        }

        static string FEquation(string groove, TractionCase k)
        {
            if (groove == GrooveTypes.UndercutU)
                return "f = μ · 4 (cos(γ/2) − sin(β/2)) / (π − β − γ − sin β + sin γ)";
            if (groove == GrooveTypes.VHardened || k == TractionCase.Stalled)
                return "f = μ / sin(γ/2)";
            return "f = μ · 4 (1 − sin(β/2)) / (π − β − sin β)";
        }

        /// <summary>5.11.2.3.2 friction coefficients.</summary>
        public static double Mu(TractionCase k, double v)
        {
            return k == TractionCase.Loading ? 0.1 : k == TractionCase.Braking ? 0.1 / (1 + v / 10) : 0.2;
        }

        // ---------- calculation ----------
        void Omit(RopeTractionResult r, string item, string kind, string reason)
        {
            r.Omissions.Add(new RopeTractionOmission { Item = item, Kind = kind, Reason = reason });
        }

        /// <summary>Runs the rope and traction checks.</summary>
        public RopeTractionResult Calculate(ElevatorProject p, DesignContext c, PresetLibrary library)
        {
            return Calculate(p, c, library, null);
        }

        /// <summary>
        /// Same calculation with the shared geometry already computed for this project and context (Geometry(p, c)); used by the preset
        /// selection to avoid recomputing an identical geometry for every rope trial. Null computes it here.
        /// </summary>
        public RopeTractionResult Calculate(ElevatorProject p, DesignContext c, PresetLibrary library, RopeTractionGeometry geometry)
        {
            var r = new RopeTractionResult
            {
                InputFingerprint = Fingerprint(p, c, library),
                LibraryHash = library == null ? null : library.Hash()
            };
            if (p == null)
            {
                r.Omissions.Add(new RopeTractionOmission { Item = "Ropes and traction", Kind = "MISSING_INPUT", Reason = "No project." });
                return r;
            }
            var g = r.Geometry = geometry ?? Geometry(p, c);
            var x = r.Inputs = Effective(p, library, g.DeflectorPresent ? g.DeflectorPitchMm : null);
            // Input validation (values that are present but not usable). Missing values make checks unavailable, never zero.
            string grooveProblem =
                x.GrooveType == null ? null : GrooveTypes.AngleProblem(x.GrooveType, x.UndercutAngleDeg, x.GrooveAngleDeg);
            if (x.BrakingRetardationMps2.HasValue && x.BrakingRetardationMps2.Value < TractionRules.MinimumBrakingRetardationMps2)
                Omit(r, "Emergency braking", "INVALID_INPUT", "Braking retardation a must be at least 0.5 m/s² (EN 81-50 5.11.2.2.2).");
            if (x.RopingRatio.HasValue && Math.Abs(x.RopingRatio.Value - 1) > 1e-9)
            {
                Omit(r, "Ropes and traction", "INVALID_INPUT", "Only 1:1 roping is implemented.");
                return Finish(r);
            }
            double rr = 1;
            double? d = Positive(x.RopeDiameterMm) ? x.RopeDiameterMm : null;
            int? n = x.RopeCount.HasValue && x.RopeCount.Value > 0 ? x.RopeCount : null;
            double? dt = Positive(x.SheavePitchDiameterMm) ? x.SheavePitchDiameterMm : null;
            // EN 81-20 5.5.1.2 a) nominal rope diameter.
            AddRopeDiameterCheck(r, d);
            // EN 81-20 5.5.1.3 number of ropes.
            AddRopeCountCheck(r, n);
            // EN 81-20 5.5.2.1 D/d ≥ 40 for the traction sheave (a check). The deflector D/d is a value only (DESIGN-FLOW 11a).
            AddSheaveRatioCheck(r, d, dt);
            bool deflectorKnown = CheckSharedGeometry(r, g, d);
            // Masses and lengths. Planner decision A6: hanging rope length H = travel + overhead; travelling cable over the travel.
            double? P = Positive(x.CarMassKg) ? x.CarMassKg : null, Q =
                Positive(x.CapacityKg) ? x.CapacityKg : null, M = Positive(x.CounterweightMassKg) ? x.CounterweightMassKg : null;
            double? w = Positive(x.RopeMassKgPerM) ? x.RopeMassKgPerM : null, mbl =
                Positive(x.MinimumBreakingLoadKn) ? x.MinimumBreakingLoadKn : null;
            SetRopeLengths(r, g, x);
            // EN 81-20 5.5.2.2 safety factor; EN 81-50 5.12 minimum.
            AddSafetyFactorCheck(r, g, x, grooveProblem, rr, d, n, dt, deflectorKnown, P, Q, w, mbl);
            // EN 81-20 5.5.2.3 terminations: computed requirement.
            AddTerminationCheck(r, mbl);
            Traction(r, g, x, P, Q, M, w, n, grooveProblem);
            return Finish(r);
        }

        private void AddRopeDiameterCheck(RopeTractionResult r, double? d)
        {
            if (d.HasValue)
            {
                r.Checks.Add(new RopeCheck
                {
                    Id = "rope-diameter",
                    Title = "Nominal rope diameter",
                    Clause = "EN 81-20 5.5.1.2 a)",
                    Equation = "d ≥ 8 mm",
                    Substitution = "d = " + F(d.Value) + " mm",
                    Required = "≥ 8 mm",
                    Actual = F(d.Value) + " mm",
                    RequiredValue = 8,
                    ActualValue = d,
                    Unit = "mm",
                    Ok = d.Value >= 8 - 1e-9
                });
            }
            else
                Omit(r, "Nominal rope diameter", "MISSING_INPUT", "Rope diameter not provided.");
        }

        private void AddRopeCountCheck(RopeTractionResult r, int? n)
        {
            if (n.HasValue)
            {
                r.Checks.Add(new RopeCheck
                {
                    Id = "rope-count",
                    Title = "Number of ropes",
                    Clause = "EN 81-20 5.5.1.3",
                    Equation = "n ≥ 2",
                    Substitution = "n = " + n.Value,
                    Required = "≥ 2",
                    Actual = n.Value.ToString(Inv),
                    RequiredValue = 2,
                    ActualValue = n.Value,
                    Ok = n.Value >= 2
                });
            }
            else
                Omit(r, "Number of ropes", "MISSING_INPUT", "Rope count not provided.");
        }

        private void AddSheaveRatioCheck(RopeTractionResult r, double? d, double? dt)
        {
            if (dt.HasValue && d.HasValue)
            {
                r.DtOverDr = dt.Value / d.Value;
                r.Checks.Add(new RopeCheck
                {
                    Id = "dd-sheave",
                    Title = "Traction sheave D/d",
                    Clause = "EN 81-20 5.5.2.1",
                    Equation = "Dt / d ≥ 40",
                    Substitution = F(dt.Value) + " / " + F(d.Value) + " = " + F2(r.DtOverDr.Value),
                    Required = "≥ 40",
                    Actual = F2(r.DtOverDr.Value),
                    RequiredValue = 40,
                    ActualValue = r.DtOverDr,
                    Ok = r.DtOverDr.Value >= 40 - 1e-9
                });
            }
            else
            {
                Omit(
                                    r,
                                    "Traction sheave D/d",
                                    "MISSING_INPUT",
                                    dt.HasValue ? "Rope diameter not provided." : "Traction sheave pitch diameter not provided.");
            }
        }

        private bool CheckSharedGeometry(RopeTractionResult r, RopeTractionGeometry g, double? d)
        {
            bool deflectorKnown = g.Available && (!g.DeflectorPresent || g.DeflectorPitchMm.HasValue);
            if (!g.Available)
                Omit(r, "Safety factor and traction", "MISSING_INPUT", "Shared geometry unavailable: " + g.Problem);
            else if (g.DeflectorPresent && !g.DeflectorPitchMm.HasValue)
            {
                Omit(
                                    r,
                                    "EN 81-50 safety factor",
                                    "INVALID_GEOMETRY",
                                    "TZ "
                                        + F(g.TzMm ?? 0)
                                        + " mm is outside the deflector selection range; no deflector size is selected.");
            }
            if (g.Available && g.DeflectorPresent && g.DeflectorPitchMm.HasValue)
            {
                if (d.HasValue)
                    r.DpOverDr = g.DeflectorPitchMm.Value / d.Value;
            }
            return deflectorKnown;
        }

        private void SetRopeLengths(RopeTractionResult r, RopeTractionGeometry g, RopeTractionEffectiveInputs x)
        {
            if (g.Available && g.TravelMm.HasValue)
                r.TravelM = g.TravelMm.Value / 1000;
            if (g.Available && g.TravelMm.HasValue && Positive(x.OverheadMm))
                r.HangingLengthM = (g.TravelMm.Value + x.OverheadMm.Value) / 1000;
        }

        private void AddSafetyFactorCheck(
            RopeTractionResult r,
            RopeTractionGeometry g,
            RopeTractionEffectiveInputs x,
            string grooveProblem,
            double rr,
            double? d,
            int? n,
            double? dt,
            bool deflectorKnown,
            double? P,
            double? Q,
            double? w,
            double? mbl)
        {
            var sfMissing = new List<string>();
            if (!mbl.HasValue)
                sfMissing.Add("rope MBL");
            if (!w.HasValue)
                sfMissing.Add("rope mass per metre");
            if (!n.HasValue)
                sfMissing.Add("rope count");
            if (!d.HasValue)
                sfMissing.Add("rope diameter");
            if (!dt.HasValue)
                sfMissing.Add("sheave pitch diameter");
            if (!P.HasValue)
                sfMissing.Add("car mass");
            if (!Q.HasValue)
                sfMissing.Add("rated load");
            if (!r.HangingLengthM.HasValue)
                sfMissing.Add("travel and overhead");
            if (x.GrooveType == null)
                sfMissing.Add("groove type");
            if (grooveProblem != null)
                Omit(r, "Safety factor and traction", "INVALID_INPUT", "Groove: " + grooveProblem + ".");
            else if (sfMissing.Count > 0)
                Omit(r, "Rope safety factor", "MISSING_INPUT", "Not provided: " + String.Join(", ", sfMissing) + ".");
            else if (deflectorKnown)
            {
                r.SfMinimum8120 = n.Value >= 3 ? 12 : 16; // EN 81-20 5.5.2.2 a) / b)
                r.NequivT = NequivTraction(x.GrooveType, x.UndercutAngleDeg, x.GrooveAngleDeg);
                r.Kp = g.DeflectorPresent ? KpFactor(dt.Value, g.DeflectorPitchMm.Value) : 1;
                // 1:1 reeving: the deflector carries one simple bend (external tangent, same bending direction; EN 81-50 Annex E, Figure
                // E.2).
                int nps = g.DeflectorPresent ? 1 : 0, npr = 0;
                r.NequivP = g.DeflectorPresent ? r.Kp.Value * (nps + 4 * npr) : 0;
                r.Nequiv = r.NequivT.Value + r.NequivP.Value;
                r.Sf8150 = SafetyFactor8150(r.DtOverDr.Value, r.Nequiv.Value);
                r.SfRequired = Math.Max(r.SfMinimum8120.Value, r.Sf8150.Value);
                // EN 81-20 5.5.2.2: force in one rope, car stationary at the lowest landing with rated load.
                r.TmaxN = ((P.Value + Q.Value) / (n.Value * rr) + r.HangingLengthM.Value * w.Value) * Gn;
                r.SfActual = mbl.Value * 1000 / r.TmaxN.Value;
                r.Checks.Add(new RopeCheck
                {
                    Id = "safety-factor",
                    Title = "Rope safety factor",
                    Clause = "EN 81-20 5.5.2.2; EN 81-50 5.12",
                    Equation = "Sf = MBL / Tmax ≥ max(" + F(r.SfMinimum8120.Value) + "; Sf(EN 81-50))",
                    Substitution = "Tmax = (("
                        + F(P.Value)
                        + " + "
                        + F(Q.Value)
                        + ") / "
                        + n.Value
                        + " + "
                        + F(r.HangingLengthM.Value)
                        + " × "
                        + F(w.Value)
                        + ") × 9.81 = "
                        + F2(r.TmaxN.Value)
                        + " N; Sf = "
                        + F(mbl.Value * 1000)
                        + " / "
                        + F2(r.TmaxN.Value)
                        + " = "
                        + F2(r.SfActual.Value),
                    Required = "≥ " + F2(r.SfRequired.Value),
                    Actual = F2(r.SfActual.Value),
                    RequiredValue = r.SfRequired,
                    ActualValue = r.SfActual,
                    Ok = r.SfActual.Value >= r.SfRequired.Value * (1 - 1e-12)
                });
            }
        }

        private void AddTerminationCheck(RopeTractionResult r, double? mbl)
        {
            if (mbl.HasValue)
            {
                r.Checks.Add(new RopeCheck
                {
                    Id = "termination",
                    Title = "Rope termination strength",
                    Clause = "EN 81-20 5.5.2.3",
                    Equation = "Ftermination ≥ 0.8 × MBL",
                    Substitution = "0.8 × " + F(mbl.Value) + " = " + F2(0.8 * mbl.Value) + " kN",
                    Required = "≥ " + F2(0.8 * mbl.Value) + " kN",
                    Actual = null,
                    RequiredValue = 0.8 * mbl.Value,
                    Unit = "kN",
                    Ok = null
                });
            }
            else
                Omit(r, "Rope termination strength", "MISSING_INPUT", "Rope MBL not provided.");
        }

        void Traction(
            RopeTractionResult r,
            RopeTractionGeometry g,
            RopeTractionEffectiveInputs x,
            double? P,
            double? Q,
            double? M,
            double? w,
            int? n,
            string grooveProblem)
        {
            if (!g.Available)
                return;
            if (g.DeflectorPresent && !g.DeflectorPathValid)
            {
                Omit(
                    r,
                    "Traction",
                    "INVALID_GEOMETRY",
                    "Deflector without a valid rope path (TZ " + F(g.TzMm ?? 0) + " mm); the wrap angle α cannot be determined.");
                return;
            }
            if (grooveProblem != null)
                return;
            var missing = MissingTractionInputs(r, x, P, Q, M, w, n);
            if (missing.Count > 0)
            {
                Omit(r, "Traction", "MISSING_INPUT", "Not provided: " + String.Join(", ", missing) + ".");
                return;
            }
            double alphaDeg = g.WrapAngleDeg.Value, alpha =
                alphaDeg * Math.PI / 180, H = r.HangingLengthM.Value, Htr = r.TravelM.Value, wt = x.TravellingCableKgPerM.Value;
            int ns = n.Value;
            double rr = 1;
            // EN 81-50 5.11.3 a), machinery above, r = 1, no compensation, no tensioning device, FR = 0 (Planner A4).
            // MSR = ns·(0.5H ± y)·w; MTrav = (0.25H ± 0.5y)·nt·w (H: hanging rope length for MSR, travel for MTrav; Annex D positions).
            double msrCarLow = ns * H * w.Value, msrCwtHigh = ns * H * w.Value, mtravHigh = 0.5 * Htr * wt;
            Func<TractionCase, double> f =
                k => FrictionFactor(x.GrooveType, k, Mu(k, x.SpeedMps ?? 0), x.UndercutAngleDeg, x.GrooveAngleDeg);
            Func<TractionCase, string, string, string, string, TractionCaseResult> make = (k, id, title, clause, cond) =>
            {
                double mu = Mu(k, x.SpeedMps ?? 0), ff = f(k);
                return new TractionCaseResult
                {
                    Id = id,
                    Title = title,
                    Clause = clause,
                    Condition = cond,
                    Mu = mu,
                    F = ff,
                    AlphaDeg = alphaDeg,
                    EFAlpha = Math.Exp(ff * alpha),
                    FEquation = FEquation(x.GrooveType, k)
                };
            };
            // 1) Car loading, 125 % Q at the lowest landing (5.11.2.2.1; Annex D).
            AddLoadingCase(r, P, Q, M, rr, msrCarLow, make);
            // 2) Emergency braking (5.11.2.2.2): rated load at the lowest landing retarding downwards; empty car at the highest landing
            // retarding upwards.
            var brakingMissing = MissingBrakingInputs(g, x);
            if (x.BrakingRetardationMps2.HasValue && x.BrakingRetardationMps2.Value < TractionRules.MinimumBrakingRetardationMps2)
            {
            }
            else if (brakingMissing.Count > 0)
                Omit(r, "Emergency braking", "MISSING_INPUT", "Not provided: " + String.Join(", ", brakingMissing) + ".");
            else
            {
                AddBrakingCase(r, g, x, P, Q, M, rr, msrCarLow, msrCwtHigh, mtravHigh, make);
            }
            // 3) Stalled: counterweight on its buffers, empty car at the highest position (5.11.2.2.3; Annex D). Only with rope slip.
            if (x.TractionLimitingMethod == TractionLimitingMethods.ElectricSafetyDevice)
            {
                Omit(
                                    r,
                                    "Car stalled (counterweight on buffers)",
                                    "NOT_APPLICABLE",
                                    "Traction limiting by an electric safety device (EN 81-20 5.5.3 c) 2)).");
            }
            else if (x.TractionLimitingMethod == null)
                Omit(r, "Car stalled (counterweight on buffers)", "MISSING_INPUT", "Traction limiting method not provided.");
            else
            {
                AddStalledCase(r, P, rr, msrCwtHigh, mtravHigh, make);
            }
        }

        static List<string> MissingTractionInputs(
            RopeTractionResult r,
            RopeTractionEffectiveInputs x,
            double? P,
            double? Q,
            double? M,
            double? w,
            int? n)
        {
            var missing = new List<string>();
            if (!P.HasValue)
                missing.Add("car mass");
            if (!Q.HasValue)
                missing.Add("rated load");
            if (!M.HasValue)
                missing.Add("counterweight mass");
            if (!w.HasValue)
                missing.Add("rope mass per metre");
            if (!n.HasValue)
                missing.Add("rope count");
            if (!r.HangingLengthM.HasValue)
                missing.Add("travel and overhead");
            if (x.GrooveType == null)
                missing.Add("groove type");
            if (!Positive(x.TravellingCableKgPerM))
                missing.Add("travelling cable mass");
            return missing;
        }

        static List<string> MissingBrakingInputs(RopeTractionGeometry g, RopeTractionEffectiveInputs x)
        {
            var missing = new List<string>();
            if (!Positive(x.SpeedMps))
                missing.Add("rated speed");
            if (!x.BrakingRetardationMps2.HasValue)
                missing.Add("braking retardation");
            if (g.DeflectorPresent && !Positive(x.DeflectorMassKg))
                missing.Add("deflector mass");
            return missing;
        }

        private void AddLoadingCase(
            RopeTractionResult r,
            double? P,
            double? Q,
            double? M,
            double rr,
            double msrCarLow,
            Func<TractionCase, string, string, string, string, TractionCaseResult> make)
        {
            var t = make(TractionCase.Loading, "loading", "Car loading (125 % Q)", "EN 81-50 5.11.2.2.1", "≤");
            t.MuEquation = "μ = 0.1";
            double t1 = (P.Value + 1.25 * Q.Value) / rr * Gn + msrCarLow * Gn, t2 = M.Value / rr * Gn;
            t.Positions.Add(new TractionPosition
            {
                Name = "Car at the lowest landing",
                T1N = t1,
                T2N = t2,
                Ratio = t1 / t2,
                T1Equation = "T1 = (P + 1.25 Q) · gn + MSRcar · gn = ("
                    + F(P.Value)
                    + " + 1.25 × "
                    + F(Q.Value)
                    + ") × 9.81 + "
                    + F(msrCarLow)
                    + " × 9.81 = "
                    + F2(t1)
                    + " N",
                T2Equation = "T2 = Mcwt · gn = " + F(M.Value) + " × 9.81 = " + F2(t2) + " N"
            });
            Close(t);
            r.Traction.Add(t);
        }

        private void AddBrakingCase(
            RopeTractionResult r,
            RopeTractionGeometry g,
            RopeTractionEffectiveInputs x,
            double? P,
            double? Q,
            double? M,
            double rr,
            double msrCarLow,
            double msrCwtHigh,
            double mtravHigh,
            Func<TractionCase, string, string, string, string, TractionCaseResult> make)
        {
            double a = x.BrakingRetardationMps2.Value;
            // Planner A2: mDP = J/R² · (v_pulley/v)², J = ½·m·R² (solid sheave); 1:1 → mDP = m/2. Counterweight side (term II).
            double mdp = g.DeflectorPresent ? x.DeflectorMassKg.Value / 2 : 0;
            r.MdpKg = g.DeflectorPresent ? mdp : (double?)null;
            var t = make(TractionCase.Braking, "braking", "Emergency braking", "EN 81-50 5.11.2.2.2", "≤");
            t.MuEquation = "μ = 0.1 / (1 + v/10) = 0.1 / (1 + " + F(x.SpeedMps.Value) + "/10) = " + t.Mu.ToString("0.####", Inv);
            double t1 = (P.Value + Q.Value) / rr * (Gn + a) + msrCarLow * (Gn + a), t2 = M.Value / rr * (Gn - a) - mdp * a / rr;
            t.Positions.Add(new TractionPosition
            {
                Name = "Rated load, lowest landing, retarding downwards",
                T1N = t1,
                T2N = t2,
                Ratio = t1 / t2,
                T1Equation = "T1 = (P + Q) · (gn + a) + MSRcar · (gn + a) = ("
                    + F(P.Value)
                    + " + "
                    + F(Q.Value)
                    + ") × (9.81 + "
                    + F(a)
                    + ") + "
                    + F(msrCarLow)
                    + " × (9.81 + "
                    + F(a)
                    + ") = "
                    + F2(t1)
                    + " N",
                T2Equation = "T2 = Mcwt · (gn − a) − mDP · a = "
                    + F(M.Value)
                    + " × (9.81 − "
                    + F(a)
                    + ") − "
                    + F(mdp)
                    + " × "
                    + F(a)
                    + " = "
                    + F2(t2)
                    + " N"
            });
            // Empty car (Note 1: delete Q; T1 and T2 exchange): counterweight side is T1.
            double e1 = M.Value / rr * (Gn + a) + msrCwtHigh * (Gn + a) + mdp * a / rr, e2 = (P.Value + mtravHigh) / rr * (Gn - a);
            t.Positions.Add(new TractionPosition
            {
                Name = "Empty car, highest landing, retarding upwards",
                T1N = e1,
                T2N = e2,
                Ratio = e1 / e2,
                T1Equation = "T1 = Mcwt · (gn + a) + MSRcwt · (gn + a) + mDP · a = "
                    + F(M.Value)
                    + " × (9.81 + "
                    + F(a)
                    + ") + "
                    + F(msrCwtHigh)
                    + " × (9.81 + "
                    + F(a)
                    + ") + "
                    + F(mdp)
                    + " × "
                    + F(a)
                    + " = "
                    + F2(e1)
                    + " N",
                T2Equation = "T2 = (P + MTrav) · (gn − a) = ("
                    + F(P.Value)
                    + " + "
                    + F(mtravHigh)
                    + ") × (9.81 − "
                    + F(a)
                    + ") = "
                    + F2(e2)
                    + " N"
            });
            Close(t);
            r.Traction.Add(t);
        }

        private void AddStalledCase(
            RopeTractionResult r,
            double? P,
            double rr,
            double msrCwtHigh,
            double mtravHigh,
            Func<TractionCase, string, string, string, string, TractionCaseResult> make)
        {
            var t = make(TractionCase.Stalled, "stalled", "Car stalled (counterweight on buffers)", "EN 81-50 5.11.2.2.3", "≥");
            t.MuEquation = "μ = 0.2";
            double t1 = (P.Value + mtravHigh) / rr * Gn, t2 = msrCwtHigh * Gn;
            t.Positions.Add(new TractionPosition
            {
                Name = "Empty car at the highest position",
                T1N = t1,
                T2N = t2,
                Ratio = t1 / t2,
                T1Equation = "T1 = (P + MTrav) · gn = (" + F(P.Value) + " + " + F(mtravHigh) + ") × 9.81 = " + F2(t1) + " N",
                T2Equation = "T2 = MSRcwt · gn = " + F(msrCwtHigh) + " × 9.81 = " + F2(t2) + " N"
            });
            Close(t);
            r.Traction.Add(t);
        }

        static void Close(TractionCaseResult t)
        {
            // Governing position: largest ratio for "≤" cases, smallest for "≥".
            var gov = t.Condition == "≤" ? t.Positions.OrderByDescending(p => p.Ratio).First() : t.Positions.OrderBy(p => p.Ratio).First();
            t.T1N = gov.T1N;
            t.T2N = gov.T2N;
            t.Ratio = gov.Ratio;
            t.Ok = t.Condition == "≤"
                ? t.Positions.All(p => p.Ratio <= t.EFAlpha * (1 + 1e-12))
                : t.Positions.All(p => p.Ratio >= t.EFAlpha * (1 - 1e-12));
        }

        static RopeTractionResult Finish(RopeTractionResult r)
        {
            r.Status = !r.HasResults
                ? (r.Omissions.Any(o => o.Kind == "INVALID_INPUT") ? "INVALID_INPUT" : "MISSING_INPUT")
                : r.AnyNotOk ? "CALCULATED_NOT_OK" : "CALCULATED_OK";
            return r;
        }
    }
}
