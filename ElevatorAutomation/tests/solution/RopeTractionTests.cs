using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.GuideRails;
using Elevator.Core.Car;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.Suspension;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

// Rope / traction Core tests (EN 81-20 5.5, EN 81-50 5.11/5.12) on the packaged candidate. Engine inputs and outputs of each
// fixture are written to hand-check-fixtures.json for the independent Python re-computation (verify_rope_traction.py).
public static class RopeTractionTests
{
    static int count; static readonly List<string> names = new List<string>();
    static string bin, output; static DesignContext ctx; static PresetLibrary lib;
    static readonly List<object> fixtures = new List<object>();
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; names.Add(name); }
    static bool Near(double? a, double b, double tol = 1e-9) { return a.HasValue && Math.Abs(a.Value - b) <= tol * Math.Max(1, Math.Abs(b)); }
    static ElevatorProject Example()
    {
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        p.Info.Name = "SYNTHETIC ROPE / TRACTION TEST — NOT PROJECT DEFAULTS";
        p.Elevator.CapacityKg = 480; p.Elevator.SpeedMetresPerSecond = 1.0;
        p.Shaft.WidthMm = 1500; p.Shaft.DepthMm = 2100; p.Machine.TractionSheavePitchDiameterMm = 520;
        p.Suspension.RopeDiameterMm = 10; p.Suspension.RopeCount = 4;
        p.Calculations = new ChassisCalculationInputs { Mode = "Basic", CarMassKg = 500, CounterweightMassKg = 740, MachineMassKg = 300 };
        return p;
    }
    static RopeTractionResult Run(ElevatorProject p) { return new RopeTractionCalculationEngine().Calculate(p, ctx, lib); }
    static RopeCheck C(RopeTractionResult r, string id) { return r.Checks.FirstOrDefault(c => c.Id == id); }
    static TractionCaseResult T(RopeTractionResult r, string id) { return r.Traction.FirstOrDefault(c => c.Id == id); }
    static void Export(string name, RopeTractionResult r)
    {
        var x = r.Inputs; var g = r.Geometry;
        fixtures.Add(new
        {
            Name = name,
            Inputs = new { P = x.CarMassKg, Q = x.CapacityKg, M = x.CounterweightMassKg, n = x.RopeCount, d = x.RopeDiameterMm, Dt = x.SheavePitchDiameterMm, Dp = g.DeflectorPresent ? g.DeflectorPitchMm : null, Deflector = g.DeflectorPresent, Alpha = g.WrapAngleDeg, H = r.HangingLengthM, Htr = r.TravelM, w = x.RopeMassKgPerM, wt = x.TravellingCableKgPerM, MBL = x.MinimumBreakingLoadKn, Groove = x.GrooveType, Beta = x.UndercutAngleDeg, Gamma = x.GrooveAngleDeg, v = x.SpeedMps, a = x.BrakingRetardationMps2, mDef = x.DeflectorMassKg, Method = x.TractionLimitingMethod },
            Engine = new
            {
                r.NequivT, r.Kp, r.NequivP, r.Nequiv, r.Sf8150, r.SfMinimum8120, r.SfRequired, r.TmaxN, r.SfActual, r.MdpKg,
                Checks = r.Checks.Select(c => new { c.Id, c.RequiredValue, c.ActualValue, c.Ok }).ToList(),
                Traction = r.Traction.Select(t => new { t.Id, t.Mu, t.F, t.AlphaDeg, t.EFAlpha, t.Condition, t.Ok, Positions = t.Positions.Select(q => new { q.Name, q.T1N, q.T2N, q.Ratio }).ToList() }).ToList(),
                Omissions = r.Omissions.Select(o => new { o.Item, o.Kind }).ToList()
            }
        });
    }
    static void Lock(ElevatorProject p, PresetField f, object v) { f.Edit(p, v); }
    public static int Main(string[] args)
    {
        bin = Path.GetFullPath(args[0]); output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; }
        Directory.CreateDirectory(output); int exit = 1;
        try
        {
            ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json")); ctx.ChassisEvidence = Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin, "reviewed-chassis-evidence.json"));
            Presets(); Fixtures(); Rules(); Table6(); Independence();
            exit = 0;
        }
        catch (Exception e) { Wire.Write(Path.Combine(output, "rope-traction-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); }
        finally
        {
            Wire.Write(Path.Combine(output, "hand-check-fixtures.json"), fixtures);
            Wire.Write(Path.Combine(output, "rope-traction-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names, Method = "RopeTractionCalculationEngine on the packaged Core with the shipped presets.json and reviewed context; synthetic fixtures. Independent formula re-computation: verify_rope_traction.py." });
        }
        Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + count + " rope / traction assertions.");
        return exit;
    }
    // ---------- presets: load, follow, lock, reset, round trip, older projects ----------
    static void Presets()
    {
        lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
        Check(lib.Ropes.Designation == "8×19 Seale FC 1370/1770" && lib.Ropes.Table.Count == 5 && lib.Rope(10).MinimumBreakingLoadKn == 44.0 && lib.Rope(10).MassKgPerM == 0.340 && lib.Rope(6.5).MinimumBreakingLoadKn == 18.5 && lib.Rope(12).MassKgPerM == 0.490, "Shipped preset library loads: rope table 8×19 Seale FC 1370/1770");
        Check(lib.Groove.Type == GrooveTypes.UndercutU && lib.Groove.UndercutAngleDeg == 95 && lib.Groove.GrooveAngleDeg == 30 && lib.TravellingCableKgPerM == 0.5 && lib.BrakingRetardationMps2 == 0.5 && lib.TractionLimitingMethod == TractionLimitingMethods.RopesSlip, "Groove, travelling cable, a and traction-limiting presets");
        Check(lib.DeflectorMassFor(400) == 45 && lib.DeflectorMassFor(520) == 70 && lib.DeflectorMassFor(620) == 95 && Near(lib.DeflectorMassFor(460), 57.5) && lib.DeflectorMassFor(250) == 45, "Deflector mass preset interpolated (460 → 57.5 kg); nearest end outside the table (250 → 45 kg)");
        Check(lib.Rope(9) == null, "No rope preset for an unlisted diameter (no interpolation of rope data)");
        foreach (var bad in new[] { "{\"Version\":1}", "{bad json", File.ReadAllText(Path.Combine(bin, "presets.json")).Replace("\"BrakingRetardationMps2\": 0.5", "\"BrakingRetardationMps2\": 0.3"), File.ReadAllText(Path.Combine(bin, "presets.json")).Replace("\"UndercutAngleDeg\": 95", "\"UndercutAngleDeg\": 110") })
        { bool threw = false; try { PresetLibrary.Parse(bad); } catch (ArgumentException) { threw = true; } Check(threw, "Invalid preset library rejected by the Core loader"); }
        var p = Example(); var c = new PresetContext { Project = p, Library = lib };
        Check(Convert.ToDouble(PresetFields.MinimumBreakingLoad.Effective(c)) == 44.0 && Convert.ToDouble(PresetFields.RopeMass.Effective(c)) == 0.340 && p.Calculations.RopeTraction == null && p.UserEditedFields == null, "Unlocked MBL / kg/m take the preset for d = 10 without writing to the project");
        p.Suspension.RopeDiameterMm = 12;
        Check(Convert.ToDouble(PresetFields.MinimumBreakingLoad.Effective(c)) == 63.3 && Convert.ToDouble(PresetFields.RopeMass.Effective(c)) == 0.490, "MBL and kg/m follow a diameter change (12 mm → 63.3 kN, 0.490 kg/m)");
        Lock(p, PresetFields.MinimumBreakingLoad, 70.0); p.Suspension.RopeDiameterMm = 8;
        Check(Convert.ToDouble(PresetFields.MinimumBreakingLoad.Effective(c)) == 70.0 && Convert.ToDouble(PresetFields.RopeMass.Effective(c)) == 0.218 && PresetFields.MinimumBreakingLoad.IsEdited(p) && !PresetFields.RopeMass.IsEdited(p), "Edited MBL stays locked (70 kN) while unlocked kg/m follows the diameter");
        var round = ProjectJson.Deserialize(ProjectJson.Serialize(p));
        Check(round.UserEditedFields != null && round.UserEditedFields.SequenceEqual(new[] { "Calculations.RopeTraction.MinimumBreakingLoadKn" }) && round.Calculations.RopeTraction.MinimumBreakingLoadKn == 70.0 && round.Calculations.RopeTraction.RopeMassKgPerM == null, "Edited flags and locked values round-trip through the project file");
        PresetFields.MinimumBreakingLoad.Reset(p);
        Check(Convert.ToDouble(PresetFields.MinimumBreakingLoad.Effective(c)) == 28.1 && !PresetFields.MinimumBreakingLoad.IsEdited(p) && p.UserEditedFields == null && p.Calculations.RopeTraction.MinimumBreakingLoadKn == null, "Reset to preset unlocks the field and it follows again (8 mm → 28.1 kN)");
        Lock(p, PresetFields.TravellingCable, null);
        Check(PresetFields.TravellingCable.Effective(c) == null, "A field the user cleared stays blank (missing), not the preset");
        PresetFields.TravellingCable.Reset(p);
        // Older project: no RopeTraction group, no flags; loads unchanged and resolves to presets.
        string json = File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json"));
        var old = ProjectJson.Deserialize(json);
        Check(old.Calculations == null || old.Calculations.RopeTraction == null, "Older project has no rope / traction group");
        Check(old.UserEditedFields == null && ProjectJson.Serialize(ProjectJson.Deserialize(ProjectJson.Serialize(old))) == ProjectJson.Serialize(old), "Older project loads unchanged (no flags added; stable round trip)");
        var x = RopeTractionCalculationEngine.Effective(old, lib, null);
        Check(x.RopeDesignation == "8×19 Seale FC 1370/1770" && x.MinimumBreakingLoadKn == 28.1 && x.GrooveType == GrooveTypes.UndercutU && x.BrakingRetardationMps2 == 0.5, "Older project resolves rope / traction inputs from presets (d = 8 mm → 28.1 kN)");
    }
    // ---------- engine fixtures ----------
    static void Fixtures()
    {
        // A: 1500 × 2100 → TZ 1000, 400 mm deflector, α = 152° from the reeving geometry; 4 × 10 mm; presets.
        var p = Example(); var r = Run(p); Export("A-deflector-400-presets", r);
        Check(r.Geometry.Available && r.Geometry.DeflectorPresent && r.Geometry.DeflectorPitchMm == 400 && r.Geometry.DeflectorPathValid && Near(r.Geometry.WrapAngleDeg, 152, 1e-6), "Fixture A geometry: 400 mm deflector with α = 152° from the reeving path");
        Check(Near(r.TravelM, 12.5) && Near(r.HangingLengthM, 16.7), "H = travel 12.5 m + overhead 4.2 m = 16.7 m (rope-mass length)");
        Check(r.Inputs.MinimumBreakingLoadKn == 44.0 && r.Inputs.RopeMassKgPerM == 0.340 && r.Inputs.DeflectorMassKg == 45 && Near(r.MdpKg, 22.5), "Presets flow into the calculation: MBL 44 kN, 0.34 kg/m, deflector 45 kg, mDP = m/2 = 22.5 kg");
        Check(C(r, "dd-deflector") == null && Near(r.DpOverDr, 40), "Boundary: deflector D/d = 400/10 = 40 is a value only, no check row (DESIGN-FLOW 11a)");
        Check(Near(C(r, "dd-sheave").ActualValue, 52) && C(r, "dd-sheave").Ok == true, "Traction sheave D/d = 520/10 = 52 OK");
        Check(r.NequivT == 6.7 && Near(r.Kp, Math.Pow(520.0 / 400, 4)) && Near(r.Nequiv, 6.7 + Math.Pow(1.3, 4)), "Nequiv = Nequiv(t) 6.7 (U β 95°) + Kp·(1 + 0), Kp = (520/400)^4");
        Check(r.SfMinimum8120 == 12 && r.SfRequired >= 12 && C(r, "safety-factor") != null, "Required Sf = max(12 for ≥ 3 ropes, EN 81-50 value)");
        Check(Near(r.TmaxN, ((500 + 480) / 4.0 + 16.7 * 0.34) * 9.81) && Near(r.SfActual, 44000 / r.TmaxN.Value), "Tmax = ((P + Q)/n + H·w)·gn; Sf = MBL / Tmax");
        Check(T(r, "loading") != null && T(r, "braking") != null && T(r, "stalled") != null && T(r, "braking").Positions.Count == 2, "Three traction cases; braking evaluated loaded (lowest) and empty (highest)");
        Check(Near(T(r, "loading").Mu, 0.1) && Near(T(r, "braking").Mu, 0.1 / 1.1) && Near(T(r, "stalled").Mu, 0.2), "μ: loading 0.1, braking 0.1/(1 + v/10), stalled 0.2");
        Check(C(r, "termination").Ok == null && Near(C(r, "termination").RequiredValue, 35.2), "Termination: computed requirement 0.8 × 44 = 35.2 kN");
        // B: direct roping, SD 1100 → TZ 500, no deflector, α = 180°.
        p = Example(); p.Shaft.DepthMm = 1100; r = Run(p); Export("B-direct-alpha-180", r);
        Check(r.Geometry.Available && !r.Geometry.DeflectorPresent && r.Geometry.WrapAngleDeg == 180 && r.Kp == 1 && r.NequivP == 0 && C(r, "dd-deflector") == null && r.MdpKg == null, "Fixture B direct roping: α = 180°, Nequiv(p) = 0, no deflector check, no mDP");
        // C–F: groove variants, including the clamped edges.
        foreach (var gv in new[] { new { N = "C-V-hardened-40", G = GrooveTypes.VHardened, B = (double?)null, Gm = (double?)40, Nt = 10.0 }, new { N = "D-V-unhardened-b90-g40", G = GrooveTypes.VUnhardened, B = (double?)90, Gm = (double?)40, Nt = 10.0 }, new { N = "E-U-b60-clamped", G = GrooveTypes.UndercutU, B = (double?)60, Gm = (double?)30, Nt = 2.5 }, new { N = "F-V-hardened-g55-clamped", G = GrooveTypes.VHardened, B = (double?)null, Gm = (double?)55, Nt = 5.0 }, new { N = "G-U-no-undercut", G = GrooveTypes.UndercutU, B = (double?)0, Gm = (double?)30, Nt = 1.0 } })
        {
            p = Example(); Lock(p, PresetFields.GrooveType, gv.G); Lock(p, PresetFields.UndercutAngle, gv.B); Lock(p, PresetFields.GrooveAngle, gv.Gm);
            r = Run(p); Export(gv.N, r);
            Check(r.NequivT == gv.Nt && T(r, "loading") != null && T(r, "stalled") != null, "Groove " + gv.N + ": Nequiv(t) = " + gv.Nt + " and traction computed");
        }
        // Unsafe side rejected: V γ < 35°, U β > 105°.
        foreach (var bad in new[] { new { G = GrooveTypes.VHardened, B = (double?)null, Gm = (double?)30 }, new { G = GrooveTypes.UndercutU, B = (double?)110, Gm = (double?)30 } })
        {
            p = Example(); Lock(p, PresetFields.GrooveType, bad.G); Lock(p, PresetFields.UndercutAngle, bad.B); Lock(p, PresetFields.GrooveAngle, bad.Gm); r = Run(p);
            Check(C(r, "safety-factor") == null && r.Traction.Count == 0 && r.Omissions.Any(o => o.Kind == "INVALID_INPUT"), "Groove outside the standard on the unsafe side is rejected (" + bad.G + ")");
            Check(GrooveTypes.AngleProblem(bad.G, bad.B, bad.Gm) != null, "Editor validation message for " + bad.G);
        }
        // H: NOT OK cases — 8 mm ropes on the 250 mm deflector (SD 1600 → TZ 750), D/d 31.25; low MBL.
        p = Example(); p.Shaft.DepthMm = 1600; p.Suspension.RopeDiameterMm = 8; p.Suspension.RopeCount = 4; Lock(p, PresetFields.MinimumBreakingLoad, 20.0);
        r = Run(p); Export("H-not-ok", r);
        Check(C(r, "dd-deflector") == null && Near(r.DpOverDr, 31.25) && r.SfMinimum8120 == 12 && C(r, "safety-factor").Ok == false && r.AnyNotOk && r.Status == "CALCULATED_NOT_OK", "NOT OK results are produced: Sf below the required value; deflector D/d 31.25 is a value only (DESIGN-FLOW 11a)");
        p = Example(); p.Suspension.RopeCount = 2; r = Run(p);
        Check(!r.Geometry.Available && C(r, "rope-count").Ok == true && C(r, "safety-factor") == null && r.Omissions.Any(o => o.Kind == "MISSING_INPUT" && o.Reason.Contains("Shared geometry unavailable")), "Two ropes: the existing project rope rule (4–7) blocks the shared geometry, so geometry-dependent checks are omitted, not zero");
        p = Example(); p.Shaft.DepthMm = 1600; p.Suspension.RopeDiameterMm = 8; r = Run(p);
        var hDesign = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB);
        Check(Near(r.Geometry.WrapAngleDeg, hDesign.Plan.MachineRoomSection.Arrangement.CalculatedWrapAngleDeg.Value, 1e-12) && r.Geometry.DeflectorPitchMm == 250, "Fixture H: 250 mm deflector, α equals the reeving-geometry wrap angle (" + r.Geometry.WrapAngleDeg.Value.ToString("0.###", CultureInfo.InvariantCulture) + "°)");
        // I: Sf exactly at the limit.
        p = Example(); var probe = Run(p); double mblLimit = probe.SfRequired.Value * probe.TmaxN.Value / 1000;
        Lock(p, PresetFields.MinimumBreakingLoad, mblLimit); r = Run(p); Export("I-sf-at-limit", r);
        Check(Near(r.SfActual, r.SfRequired.Value, 1e-12) && C(r, "safety-factor").Ok == true, "Boundary: Sf exactly at the required value is OK");
        Lock(p, PresetFields.MinimumBreakingLoad, mblLimit * 0.999); r = Run(p);
        Check(C(r, "safety-factor").Ok == false, "Sf 0.1 % below the required value is NOT OK");
        // J: electric safety device → stalled not applicable.
        p = Example(); Lock(p, PresetFields.TractionLimitingMethod, TractionLimitingMethods.ElectricSafetyDevice); r = Run(p);
        Check(T(r, "stalled") == null && r.Omissions.Any(o => o.Kind == "NOT_APPLICABLE") && T(r, "loading") != null && T(r, "braking") != null, "Electric safety device: stalled check not applicable and not shown");
        // K: invalid deflector path (SD 2500 → TZ 1200) → traction and EN 81-50 Sf omitted for invalid geometry.
        p = Example(); p.Shaft.DepthMm = 2500; r = Run(p); Export("K-invalid-path", r);
        Check(r.Traction.Count == 0 && C(r, "safety-factor") == null && r.Omissions.Count(o => o.Kind == "INVALID_GEOMETRY") == 2 && C(r, "rope-diameter") != null && C(r, "dd-sheave") != null, "Deflector without a valid path: traction and Sf omitted as invalid geometry; diameter and sheave checks kept");
        // L: missing inputs are omitted, never zero.
        p = Example(); p.Elevator.SpeedMetresPerSecond = null; Lock(p, PresetFields.MinimumBreakingLoad, null); r = Run(p);
        Check(C(r, "safety-factor") == null && C(r, "termination") == null && T(r, "braking") == null && T(r, "loading") != null && r.Omissions.Any(o => o.Item == "Emergency braking" && o.Kind == "MISSING_INPUT"), "Missing MBL / speed: Sf, termination and braking omitted, loading kept");
        p = Example(); p.Machine.TractionSheavePitchDiameterMm = null; p.Suspension.RopeDiameterMm = 9; r = Run(p);
        Check(C(r, "dd-sheave") == null && C(r, "safety-factor") == null && r.Inputs.MinimumBreakingLoadKn == null, "No sheave diameter / unlisted rope diameter: dependent checks omitted (no zero, no invented MBL)");
        p = Example(); Lock(p, PresetFields.BrakingRetardation, 0.4); r = Run(p);
        Check(T(r, "braking") == null && r.Omissions.Any(o => o.Item == "Emergency braking" && o.Kind == "INVALID_INPUT"), "Braking retardation below 0.5 m/s² is rejected");
        // Currency.
        p = Example(); r = Run(p);
        Check(RopeTractionCalculationEngine.IsCurrent(r, p, ctx, lib), "Fresh result is current");
        p.Suspension.RopeDiameterMm = 12; Check(!RopeTractionCalculationEngine.IsCurrent(r, p, ctx, lib), "Diameter change makes the result stale");
        p = Example(); r = Run(p); Lock(p, PresetFields.GrooveAngle, 32.0); Check(!RopeTractionCalculationEngine.IsCurrent(r, p, ctx, lib), "Rope / traction input edit makes the result stale");
        p = Example(); r = Run(p); p.Calculations.CarGuideForces = new CarGuideForceInputs { GuideShoeSpacingMm = 1300 }; Check(RopeTractionCalculationEngine.IsCurrent(r, p, ctx, lib), "FF input edit does not stale the rope / traction result (own fingerprint)");
    }
    // ---------- MR rule connection ----------
    static void Rules()
    {
        var p = Example(); var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB);
        var rules = design.Plan.MachineRoom.Rules;
        Check(rules.First(x => x.RuleId == "MR-TRACTION-001").Status == "NOT YET EVALUABLE", "MR-TRACTION-001 is NOT YET EVALUABLE before the rope / traction result");
        var r = Run(p); RopeTractionRules.Apply(rules, r);
        string expected = C(r, "safety-factor").Ok == true && r.Traction.All(t => t.Ok) ? "PASS" : "FAIL";
        Check(rules.First(x => x.RuleId == "MR-TRACTION-001").Status == expected && rules.First(x => x.RuleId == "MR-TRACTION-001").CalculatedValue.Contains("Sf"), "MR-TRACTION-001 evaluated from the results (" + expected + ")");
        Check(rules.First(x => x.RuleId == "MR-ROPE-MIN-001").Status == "PASS" && rules.First(x => x.RuleId == "MR-ROPE-001").Status == "PASS", "Rope rules evaluated: d ≥ 8 and D/d ≥ 40 (sheave and deflector) PASS");
        p.Shaft.DepthMm = 1600; p.Suspension.RopeDiameterMm = 8; design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB); rules = design.Plan.MachineRoom.Rules;
        RopeTractionRules.Apply(rules, Run(p));
        Check(rules.First(x => x.RuleId == "MR-ROPE-001").Status == "FAIL", "MR-ROPE-001 FAIL for the 250 mm deflector with 8 mm ropes");
    }
    static void Table6()
    {
        Check(Near(CarAreaRules.MaximumAvailableAreaM2(480), 1.36) && Near(CarAreaRules.MaximumAvailableAreaM2(450), 1.30) && Near(CarAreaRules.MaximumAvailableAreaM2(2600), 5.16) && CarAreaRules.MaximumAvailableAreaM2(80) == null, "EN 81-20 Table 6 with linear interpolation (480 kg → 1.36 m²; 2600 kg → 5.16 m²)");
        Check(1.1 * 2.1 > CarAreaRules.MaximumAvailableAreaM2(480).Value, "480 kg with 1100 × 2100 car (2.31 m²) exceeds Table 6 (NOT OK)");
    }
    // Other calculations keep their own currency when rope / traction inputs change.
    static void Independence()
    {
        var p = Example(); p.Calculations.CarGuideForces = new CarGuideForceInputs { GuideShoeSpacingMm = 1200 };
        p.Calculations.SafetyPit = new SafetyPitInputs { SafetyGearType = "Progressive", CarGuideCount = 2, CarBufferCount = 1, CounterweightBufferCount = 1 };
        var geometry = new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, ctx); var ff = new CarGuideForceCalculationEngine().Calculate(p, geometry, ctx);
        var sp = new SafetyPitCalculationEngine().Calculate(p);
        var ws = new Elevator.Core.Application.CalculationWorkspace(); var chassis = ws.Calculate(p, ctx);
        Check(Near(ff.FF1DesignN, 2 * 480 * 10 * geometry.InternalWidthMm.Value / (4 * 1200.0)) && Near(sp.P5.PerComponentLoadN, 9800), "FF and P5 values unchanged by the new module (FF1 per note rule; P5 9800 N)");
        Lock(p, PresetFields.MinimumBreakingLoad, 50.0); Lock(p, PresetFields.GrooveAngle, 33.0);
        Check(CarGuideForceCalculationEngine.IsCurrent(ff, p, ctx) && SafetyPitCalculationEngine.IsCurrent(sp, p) && ws.IsCurrent(p, ctx), "Rope / traction edits leave P1–P4, P5–P7 and FF results current (independent fingerprints)");
    }
}
