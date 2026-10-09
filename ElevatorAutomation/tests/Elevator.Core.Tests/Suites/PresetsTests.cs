using System;
using System.Threading.Tasks;
using Xunit;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
using System.Text;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Core.Tests
{
    // DESIGN-FLOW stage 2 ("Flow foundation"), Core level: loader validation, capacity-table interpolation, CW = P + q·Q,
    // rope ladder, safety-gear / buffer rules, a new project computed with no input, lock / follow / Reset, rope auto-upsize,
    // older projects and "Fill blanks", and identical numbers for identical inputs. Shipped presets.json and reviewed context.
    public class PresetsTests
    {
        static string bin, output; static DesignContext ctx; static PresetLibrary lib;
        static int count; static readonly List<string> names = new List<string>();
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); lock (names) { ++count; names.Add(name); } Suite.Count("presets-core"); }
        static bool Near(double? a, double b) { return a.HasValue && Math.Abs(a.Value - b) < 1e-9; }
        static ElevatorProject New() { var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json"))); return ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource); }
        static void SetQ(ElevatorProject p, double? q) { ProjectPresets.Lock(p, "Elevator.CapacityKg"); p.Elevator.CapacityKg = q; ProjectPresets.Apply(p, lib, ctx); }
        static PresetLibrary Variant(Func<string, string> change) { return PresetLibrary.Parse(change(File.ReadAllText(Path.Combine(bin, "presets.json")))); }
        static RopeTractionResult Rope(ElevatorProject p, PresetLibrary l) { return new RopeTractionCalculationEngine().Calculate(p, ctx, l ?? lib); }
        static object Numbers(ElevatorProject p)
        {
            var w = new CalculationWorkspace(); var basic = w.Calculate(p, ctx);
            var pit = new SafetyPitCalculationEngine().Calculate(p);
            var ff = new CarGuideForceCalculationEngine().Calculate(p, new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, ctx), ctx);
            var rope = Rope(p, null);
            return new
            {
                Basic = basic.Reactions == null ? null : basic.Reactions.OrderBy(r => r.Id).Select(r => r.Id + "=" + r.UpwardN.ToString("R")).ToArray(),
                basic.CarBundleTensionN, basic.CounterweightBundleTensionN, basic.CarPerRopeTensionN,
                P5 = pit.P5.PerComponentLoadN, P6 = pit.P6.GroupLoadN, P7 = pit.P7.GroupLoadN,
                ff.FF1BaseN, ff.FF1DesignN, ff.FF2BaseN, ff.FF2DesignN,
                rope.TmaxN, rope.SfActual, rope.SfRequired, rope.Nequiv,
                Checks = rope.Checks.Select(c => c.Id + "=" + c.Actual + "/" + c.Ok).ToArray(),
                Traction = rope.Traction.Select(t => t.Id + "=" + t.Ratio.ToString("R") + "/" + t.EFAlpha.ToString("R") + "/" + t.Ok).ToArray()
            };
        }

        static void Init()
        {
            bin = TestData.Bin; output = TestData.Folder("presets-core"); ctx = TestData.Context(); lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
        }
        [Fact]
        public void Loader_ladder_rules_lock_follow_and_selection()
        {
            Init();
            int before = count;
            Run();
            Assert.Equal(125, count - before);
        }
        // The seven capacities on the survey shaft and on a Table 6 shaft, each against the independent brute-force search; the 14 cases run in parallel.
        [Fact]
        public void Q_sweep_survey_and_Table_6_shafts()
        {
            Init();
            int before = count;
            Sweep();
            Assert.Equal(35, count - before);
        }
        static void Run()
        {
            // ---------- 1. loader ----------
            Check(lib.Validate().Count == 0, "Shipped presets.json loads and validates");
            Wire.Write(Path.Combine(output, "preset-table.json"), new { lib.NewProject, lib.PersonMassKg, lib.CapacityTable, lib.CounterweightBalanceFactor, lib.RopeLadder, lib.MaximumRopeCount, lib.TractionSheavePitchDiameterMm, lib.TractionSheavePitchSteps, lib.GuideShoeSpacingMm, lib.SafetyGear, lib.Buffers, lib.BracketMaximumSpacingMm, lib.Advanced, lib.Ropes, lib.Groove, lib.TravellingCableKgPerM, lib.BrakingRetardationMps2, lib.DeflectorMass, lib.TractionLimitingMethod });
            var bad = new Dictionary<string, Func<string, string>>
            {
                { "no NewProject", s => s.Replace("\"NewProject\"", "\"NewProjectX\"") },
                { "one capacity row", s => System.Text.RegularExpressions.Regex.Replace(s, "\"CapacityTable\": \\[[^\\]]*\\]", "\"CapacityTable\": [ { \"CapacityKg\": 630, \"CarMassKg\": 650, \"MachineMassKg\": 350 } ]") },
                { "duplicate capacity", s => s.Replace("\"CapacityKg\": 800, \"CarMassKg\": 850", "\"CapacityKg\": 630, \"CarMassKg\": 850") },
                { "ladder diameter not in rope table", s => s.Replace("\"RopeCount\": 5, \"DiameterMm\": 11", "\"RopeCount\": 5, \"DiameterMm\": 9") },
                { "ladder count above 7", s => s.Replace("\"MaxCapacityKg\": null, \"RopeCount\": 7", "\"MaxCapacityKg\": null, \"RopeCount\": 8") },
                { "ladder capacities not increasing", s => s.Replace("\"MaxCapacityKg\": 800,", "\"MaxCapacityKg\": 400,") },
                { "last ladder row bounded", s => s.Replace("\"MaxCapacityKg\": null,", "\"MaxCapacityKg\": 2000,") },
                { "sheave steps without 520", s => s.Replace("[ 520, 560, 620 ]", "[ 560, 620 ]") },
                { "balance factor 1.5", s => s.Replace("\"CounterweightBalanceFactor\": 0.5", "\"CounterweightBalanceFactor\": 1.5") },
                { "buffer sharing unresolved", s => s.Replace("\"Sharing\": \"EqualSharing\"", "\"Sharing\": \"Unresolved\"") },
                { "three car guides", s => s.Replace("\"CarGuideCount\": 2", "\"CarGuideCount\": 3") },
                { "wrong transfer model", s => s.Replace("\"TransferModel\": \"ReleasedTwoStageWithRigidSpreader-v1\"", "\"TransferModel\": \"Other\"") },
                { "negative floor height", s => s.Replace("\"FloorHeightMm\": 3000", "\"FloorHeightMm\": -3000") },
                { "undercut step above 105°", s => s.Replace("[ 95, 100, 105 ]", "[ 95, 100, 110 ]") },
                { "balance steps without the preset q", s => s.Replace("[ 0.5, 0.45 ]", "[ 0.45 ]") },
                { "balance step above 1", s => s.Replace("[ 0.5, 0.45 ]", "[ 0.5, 1.2 ]") },
                { "undercut steps without the preset β", s => s.Replace("[ 95, 100, 105 ]", "[ 100, 105 ]") },
                { "undercut steps not increasing", s => s.Replace("[ 95, 100, 105 ]", "[ 95, 105, 100 ]") },
                { "one stop", s => s.Replace("\"Stops\": 5", "\"Stops\": 1") },
            };
            foreach (var b in bad)
            {
                var text = b.Value(File.ReadAllText(Path.Combine(bin, "presets.json")));
                Check(text != File.ReadAllText(Path.Combine(bin, "presets.json")), "Loader fixture changes the file: " + b.Key);
                bool threw = false; try { PresetLibrary.Parse(text); } catch (ArgumentException) { threw = true; }
                Check(threw, "Loader rejects: " + b.Key);
            }

            // ---------- 2. interpolation (at and between table points, clamped) ----------
            foreach (var row in lib.CapacityTable) { var m = lib.MassesFor(row.CapacityKg); Check(Near(m.CarMassKg, row.CarMassKg) && Near(m.MachineMassKg, row.MachineMassKg), "Table point Q " + row.CapacityKg + " → P " + row.CarMassKg + " / machine " + row.MachineMassKg); }
            foreach (var t in new[] { new double[] { 400, 500, 275 }, new double[] { 550, 620, 323.33333333333331 }, new double[] { 715, 775, 375 }, new double[] { 900, 975, 425 }, new double[] { 1137.5, 1225, 500 }, new double[] { 1437.5, 1500, 600 }, new double[] { 200, 450, 250 }, new double[] { 2000, 1650, 650 } })
            { var m = lib.MassesFor(t[0]); Check(Math.Abs(m.CarMassKg - t[1]) < 1e-9 && Math.Abs(m.MachineMassKg - t[2]) < 1e-9, "Q " + t[0] + " kg → P " + t[1] + ", machine " + t[2] + (t[0] < 320 || t[0] > 1600 ? " (clamped)" : " (interpolated)")); }
            Check(lib.MassesFor(null) == null, "No capacity → no mass preset");

            // ---------- 3. CW = P + q·Q, persons, rope ladder, gear / buffer rules ----------
            Check(lib.CounterweightFor(700, 630) == 1015 && lib.CounterweightFor(531.25, 450) == 756.25 && lib.CounterweightFor(1000, 1000) == 1500, "CW = P + 0.5·Q (700 + 315 = 1015; 531.25 + 225 = 756.25; 1000 + 500 = 1500)");
            Check(lib.CapacityTable.Select(r => r.CarMassKg).SequenceEqual(new double[] { 450, 550, 700, 850, 1100, 1350, 1650 }) && lib.CapacityTable.Select(r => r.MachineMassKg).SequenceEqual(new double[] { 250, 300, 350, 400, 450, 550, 650 }) && lib.CounterweightBalanceFactor == 0.5, "Capacity table: P 450 / 550 / 700 / 850 / 1100 / 1350 / 1650 kg; machine masses unchanged; q = 0.5");
            Check(lib.BalanceFactorSteps.SequenceEqual(new double[] { 0.5, 0.45 }), "Balance factor steps 0.50 → 0.45");
            Check(lib.UndercutAngleStepsDeg.SequenceEqual(new double[] { 95, 100, 105 }) && lib.Groove.UndercutAngleDeg == 95, "Undercut angle steps 95 → 100 → 105°, starting at the groove preset 95°");
            Check(lib.PersonsFor(630) == 8 && lib.PersonsFor(75) == 1 && lib.PersonsFor(74) == 0 && lib.PersonsFor(1000) == 13 && lib.PersonsFor(null) == null, "Persons = floor(Q / 75): 630 → 8, 75 → 1, 74 → 0, 1000 → 13");
            foreach (var t in new[] { new[] { 320, 4, 10 }, new[] { 480, 4, 10 }, new[] { 481, 5, 11 }, new[] { 800, 5, 11 }, new[] { 801, 6, 12 }, new[] { 1275, 6, 12 }, new[] { 1276, 7, 12 }, new[] { 1600, 7, 12 } })
            { var r = lib.RopesFor(t[0]); Check(r.RopeCount == t[1] && r.DiameterMm == t[2], "Rope ladder Q " + t[0] + " → " + t[1] + " × " + t[2] + " mm"); }
            Func<double, string[]> rules = v => { var p = New(); ProjectPresets.Lock(p, "Elevator.SpeedMetresPerSecond"); p.Elevator.SpeedMetresPerSecond = v; ProjectPresets.Apply(p, lib, null); return new[] { p.Calculations.SafetyPit.SafetyGearType, p.Calculations.SafetyPit.BufferType }; };
            var r063 = rules(0.63); var r064 = rules(0.64); var r100 = rules(1.0); var r101 = rules(1.01);
            Check(r063[0] == "Instantaneous" && r064[0] == "Progressive" && r100[0] == "Progressive", "Safety gear: v 0.63 → instantaneous; v 0.64 and 1.0 → progressive");
            Check(r063[1] == "EnergyAccumulation" && r100[1] == "EnergyAccumulation" && r101[1] == "EnergyDissipation", "Buffers: v ≤ 1.0 → energy accumulation; v 1.01 → energy dissipation");
            var locked = New(); var gear = ProjectPresets.Find("Calculations.SafetyPit.SafetyGearType"); gear.Edit(locked, "Instantaneous"); ProjectPresets.Apply(locked, lib, null);
            Check(locked.Calculations.SafetyPit.SafetyGearType == "Instantaneous", "A locked gear choice stays (instantaneous at v 1.0)");

            // ---------- 4. new project, complete with no input ----------
            var n = New(); var c = n.Calculations; var sp = c.SafetyPit;
            File.WriteAllText(Path.Combine(output, "new-project.elevator.json"), ProjectJson.Serialize(n));
            Check(n.FollowsPresets == true && n.UserEditedFields == null && c.Mode == "Basic", "New project follows presets, nothing locked, Basic mode");
            Check(n.Shaft.WidthMm == 1500 && n.Shaft.DepthMm == 1800 && n.Shaft.PitDepthMm == 1300 && n.Shaft.OverheadMm == 4200, "Survey example shaft: SW 1500, SD 1800, pit 1300, overhead 4200");
            Check(n.Building.Floors.Count == 5 && n.Building.Floors.Select(f => f.Name).SequenceEqual(new[] { "G", "1", "2", "3", "4" }) && n.Building.Floors.Take(4).All(f => f.HeightToNextMm == 3000) && n.Building.Floors[4].HeightToNextMm == null && n.Car.DisplayFloorId == n.Building.Floors[0].FloorId, "5 stops G + 4 at 3000 mm; last floor has no interval");
            Check(ProjectPresets.NewFloorHeight(lib) == 3000, "New floors added by the user get 3000 mm");
            Check(n.Elevator.CapacityKg == 630 && n.Elevator.SpeedMetresPerSecond == 1.0 && lib.PersonsFor(n.Elevator.CapacityKg) == 8, "Q 630 kg (8 persons), v 1.0 m/s");
            Check(c.CarMassKg == 700 && c.MachineMassKg == 350 && c.CounterweightMassKg == 1015, "Q 630 → P 700, machine 350, CW 1015");
            var expectNew = Brute(n, lib);
            Check(expectNew != null && n.Suspension.RopeCount == expectNew.Item1 && n.Suspension.RopeDiameterMm == expectNew.Item2 && n.Machine.TractionSheavePitchDiameterMm == 520 && n.Suspension.RopingRatio == 1, "New project ropes = the smallest passing combination found by an independent brute-force search: " + n.Suspension.RopeCount + " × " + n.Suspension.RopeDiameterMm + " mm, β " + (expectNew == null ? "-" : expectNew.Item3.ToString()) + "°, sheave 520 mm");
            Check(c.CarGuideForces.GuideShoeSpacingMm == 2800, "h = 2800 mm");
            Check(sp.SafetyGearType == "Progressive" && sp.BufferType == "EnergyAccumulation" && sp.CarGuideCount == 2 && sp.CarBufferCount == 1 && sp.CounterweightBufferCount == 1 && sp.CarBufferSharing == "EqualSharing" && sp.CounterweightBufferSharing == "EqualSharing", "Progressive gear, energy-accumulation buffers, 2 car guide rails, 1 car / 1 CW buffer, equal sharing");
            Check(n.Rails.BracketMaximumSpacingMm == 1500, "Bracket spacing 1500 mm");
            Check(c.IncludeChassisSelfWeight == false && c.IncludeHangerAssemblyWeight == false && c.CarSideAdditionalSuspendedMassKg == null && c.CounterweightSideAdditionalSuspendedMassKg == null, "Basic: optional additions and chassis / hanger excluded");
            Check(c.MachineCgOffsetXMm == 0 && c.MachineCgOffsetYMm == 0 && c.MachineCgOffsetZMm == 0 && c.HangerAttachmentOffsetXMm == 0 && c.HangerAttachmentOffsetYMm == 0 && c.TransferModel == ChassisReactionSolver.Model && c.LoadCase == "StaticRatedLoad1To1" && !String.IsNullOrWhiteSpace(c.MachineCgSource) && !String.IsNullOrWhiteSpace(c.TransferAssumptionSource), "Advanced presets: CG and hanger offsets 0, default transfer model and load case, basis texts kept in the model");
            Check(n.Counterweight.CalibrationStatus == "PROVISIONAL_SOURCE_REFERENCE_POSE" && n.Counterweight.ReferenceBottomElevationMm.HasValue && n.MachineRoom.LeftExtensionMm == null && n.Drawing.ViewMarginMm == 1000, "Existing counterweight reference pose, machine-room extensions and drawing options kept");
            var design = new ElevatorDesignEngine().Calculate(n, ctx, DrawingViews.All);
            Check(design.CanGenerate && design.Plan != null && design.SectionAA != null, "Shared design (all four views) valid for the new project");
            var w = new CalculationWorkspace(); var basic = w.Calculate(n, ctx);
            Check(basic.Reactions != null && basic.Status == "CALCULATED_UNDER_ASSUMPTIONS" && basic.CarPerRopeTensionN.HasValue, "Basic P1–P4 and rope tensions calculate with no input");
            var pit = new SafetyPitCalculationEngine().Calculate(n);
            Check(pit.P5.PerComponentLoadN.HasValue && pit.P6.PerComponentLoadN.HasValue && pit.P7.PerComponentLoadN.HasValue, "P5–P7 calculate with no input");
            var ff = new CarGuideForceCalculationEngine().Calculate(n, new ElevatorDesignEngine().CalculateCarGuideForceGeometry(n, ctx), ctx);
            Check(ff.FF1DesignN.HasValue && ff.FF2DesignN.HasValue, "FF1 / FF2 calculate with no input");
            var rope = Rope(n, null);
            Check(rope.HasResults && rope.Checks.Any(k => k.Id == "safety-factor") && rope.Traction.Count == 3 && rope.Omissions.Count == 0, "Rope / traction checks calculate with no input (3 traction cases, no omission)");
            Check(Qsel(n) == 0.5 && c.CounterweightMassKg == 1015, "New project keeps q 0.5 (CW 1015)");
            var sizing = ProjectPresets.Outcome(n, lib, ctx);
            Check(sizing.Status == "Selected" && ProjectPresets.Passes(rope) && rope.Checks.All(k => k.Ok != false) && rope.Traction.All(t => t.Ok), "New project: every rope and traction check OK (" + String.Join(", ", rope.Checks.Where(k => k.Ok.HasValue).Select(k => k.Id + " " + k.Actual)) + "; " + String.Join(", ", rope.Traction.Select(t => t.Id + " " + t.Ratio.ToString("0.###") + "/" + t.EFAlpha.ToString("0.###"))) + ")");
            Check(rope.Inputs.UndercutAngleDeg == expectNew.Item3 && (expectNew.Item3 == 95 ? n.Calculations.RopeTraction == null || n.Calculations.RopeTraction.SelectedUndercutAngleDeg == null : n.Calculations.RopeTraction.SelectedUndercutAngleDeg == expectNew.Item3) && !PresetFields.UndercutAngle.IsEdited(n), "New project β " + rope.Inputs.UndercutAngleDeg + "° selected while β stays unlocked; groove type and γ unchanged (" + rope.Inputs.GrooveType + ", γ " + rope.Inputs.GrooveAngleDeg + "°)");
            Wire.Write(Path.Combine(output, "new-project-results.json"), new { Basic = basic, P5 = pit.P5, P6 = pit.P6, P7 = pit.P7, FF = ff, Ropes = new { rope.Checks, rope.Traction, rope.Omissions }, Sizing = sizing });

            // ---------- 5. lock / follow / Reset ----------
            var fl = New(); SetQ(fl, 1000);
            Check(fl.Calculations.CarMassKg == 1100 && fl.Calculations.MachineMassKg == 450 && Math.Abs(fl.Calculations.CounterweightMassKg.Value - (1100 + Qsel(fl) * 1000)) < 1e-9, "Q 1000: unlocked P 1100, machine 450 and CW = P + q·Q follow (q " + Qsel(fl) + ", CW " + fl.Calculations.CounterweightMassKg + ")");
            Check(fl.Shaft.WidthMm == 1500 && fl.Elevator.SpeedMetresPerSecond == 1.0 && fl.Calculations.CarGuideForces.GuideShoeSpacingMm == 2800, "Fields that do not depend on Q keep their presets");
            var pField = ProjectPresets.Find("Calculations.CarMassKg"); pField.Edit(fl, 700.0); SetQ(fl, 800);
            Check(fl.Calculations.CarMassKg == 700 && fl.Calculations.MachineMassKg == 400 && Math.Abs(fl.Calculations.CounterweightMassKg.Value - (700 + Qsel(fl) * 800)) < 1e-9 && pField.IsEdited(fl), "Locked P 700 stays at Q 800; machine 400 and CW 700 + q·800 follow");
            var cwField = ProjectPresets.Find("Calculations.CounterweightMassKg"); cwField.Edit(fl, 1234.0); SetQ(fl, 630);
            Check(fl.Calculations.CounterweightMassKg == 1234 && fl.Calculations.CarMassKg == 700, "A user-edited CW no longer follows P + q·Q");
            var round = ProjectJson.Deserialize(ProjectJson.Serialize(fl));
            Check(round.FollowsPresets == true && round.UserEditedFields.OrderBy(k => k).SequenceEqual(new[] { "Calculations.CarMassKg", "Calculations.CounterweightMassKg", "Elevator.CapacityKg" }) && round.Calculations.CarMassKg == 700, "Flags and locked values round-trip through the project file");
            pField.Reset(fl); cwField.Reset(fl); ProjectPresets.Apply(fl, lib, ctx);
            Check(fl.Calculations.CarMassKg == 700 && fl.Calculations.CounterweightMassKg == 1015 && Qsel(fl) == 0.5 && !pField.IsEdited(fl) && !cwField.IsEdited(fl), "Reset unlocks P and CW; they follow again (700 / 1015, q 0.5)");
            var blankQ = New(); SetQ(blankQ, null);
            Check(blankQ.Elevator.CapacityKg == null && blankQ.Calculations.CarMassKg == null && blankQ.Calculations.CounterweightMassKg == null && blankQ.Suspension.RopeCount == null, "A Q the user cleared stays blank and its followers become blank");

            // ---------- 6. rope and groove selection ----------
            // A: MBL locked at 20 kN (any diameter): the search still runs; it must equal the brute-force order (d, then n).
            var a = New(); SetQ(a, 320); PresetFields.MinimumBreakingLoad.Edit(a, 20.0);
            var oa = ProjectPresets.Apply(a, lib, ctx); var ea = Brute(a, lib);
            Check((ea == null ? oa.Status == "NoPass" : oa.Status == "Selected" && a.Suspension.RopeCount == ea.Item1 && a.Suspension.RopeDiameterMm == ea.Item2 && ProjectPresets.Passes(Rope(a, null))), "Fixture A (Q 320, MBL locked 20 kN): selection equals the brute-force smallest d then n (" + a.Suspension.RopeCount + " × " + a.Suspension.RopeDiameterMm + " mm)");
            Check(a.Calculations.RopeTraction.MinimumBreakingLoadKn == 20 && PresetFields.MinimumBreakingLoad.IsEdited(a), "Fixture A: the locked MBL is never changed");
            // B: rope count locked at 7: only the diameter is searched.
            var b2 = New(); ProjectPresets.Find(ProjectPresets.RopeCountKey).Edit(b2, 7); var ob = ProjectPresets.Apply(b2, lib, ctx); var eb = Brute(b2, lib);
            Check(b2.Suspension.RopeCount == 7 && (eb == null ? ob.Status == "NoPass" : b2.Suspension.RopeDiameterMm == eb.Item2), "Fixture B: locked count 7 kept; diameter " + b2.Suspension.RopeDiameterMm + " mm from the search");
            // C: count and diameter locked → no rope change; NOT OK stays.
            var cl = New(); SetQ(cl, 320); PresetFields.MinimumBreakingLoad.Edit(cl, 20.0); ProjectPresets.Find(ProjectPresets.RopeCountKey).Edit(cl, 4); ProjectPresets.Find(ProjectPresets.RopeDiameterKey).Edit(cl, 10.0);
            var oc = ProjectPresets.Apply(cl, lib, ctx);
            Check(cl.Suspension.RopeCount == 4 && cl.Suspension.RopeDiameterMm == 10 && Rope(cl, null).Checks.First(k => k.Id == "safety-factor").Ok == false, "Fixture C: locked count and diameter are never changed; the safety factor stays NOT OK (" + oc.Status + ")");
            // D: β locked at 95° on the new project: β never changes; the ropes are searched with β 95°.
            var bl = New(); PresetFields.UndercutAngle.Edit(bl, 95.0); var od = ProjectPresets.Apply(bl, lib, ctx); var rd = Rope(bl, null);
            Check(rd.Inputs.UndercutAngleDeg == 95 && bl.Calculations.RopeTraction.SelectedUndercutAngleDeg == null && PresetFields.UndercutAngle.IsEdited(bl), "Fixture D: a locked β (95°) is never changed (" + od.Status + (od.Status == "NoPass" ? ", limiting check " + od.LimitingCheck : "") + ")");
            Check(od.Status == "NoPass" ? bl.Suspension.RopeCount == lib.RopesFor(630).RopeCount && bl.Suspension.RopeDiameterMm == lib.RopesFor(630).DiameterMm && !String.IsNullOrEmpty(od.LimitingCheck) : ProjectPresets.Passes(rd), "Fixture D: with β locked either a passing rope is found or the ladder value stays with a named limiting check");
            // E: groove type locked: β is not stepped.
            var gl = New(); PresetFields.GrooveType.Edit(gl, GrooveTypes.UndercutU); ProjectPresets.Apply(gl, lib, ctx);
            Check(Rope(gl, null).Inputs.UndercutAngleDeg == 95 && (gl.Calculations.RopeTraction.SelectedUndercutAngleDeg == null), "Fixture E: a locked groove type keeps the preset β (95°)");
            // F: Reset β unlocks it and the selection runs again.
            PresetFields.UndercutAngle.Reset(bl); ProjectPresets.Apply(bl, lib, ctx);
            Check(Rope(bl, null).Inputs.UndercutAngleDeg == Rope(n, null).Inputs.UndercutAngleDeg && bl.Suspension.RopeCount == n.Suspension.RopeCount && bl.Suspension.RopeDiameterMm == n.Suspension.RopeDiameterMm, "Fixture F: Reset β → the selection gives the new-project result again");
            // G: nothing passes (Q 1000 on the survey shaft): the ladder value stays, the sheave stays 520, the limiting check is named.
            var np = New(); SetQ(np, 1000); var og = ProjectPresets.Outcome(np, lib, ctx);
            var eg = Brute(np, lib);
            Check(eg == null ? (og.Status == "NoPass" && np.Suspension.RopeCount == 6 && np.Suspension.RopeDiameterMm == 12 && !String.IsNullOrEmpty(og.LimitingCheck)) : (og.Status == "Selected" && np.Suspension.RopeCount == eg.Item1 && np.Suspension.RopeDiameterMm == eg.Item2 && Rope(np, null).Inputs.UndercutAngleDeg == eg.Item3 && Qsel(np) == eg.Item4), "Fixture G: Q 1000 on the survey shaft agrees with the brute force (" + og.Status + ", " + np.Suspension.RopeCount + " × " + np.Suspension.RopeDiameterMm + " mm" + (og.Status == "NoPass" ? ", limiting check " + og.LimitingCheck : "") + "); sheave " + np.Machine.TractionSheavePitchDiameterMm);

            // Geometry reuse in the selection: the shared geometry does not change with n, β or q (only with Dt and d validation).
            foreach (var gq in new double[] { 630, 1600 })
            {
                var g0 = New(); SetQ(g0, gq); var gA = ProjectJson.Clone(g0); gA.Suspension.RopeCount = 4; var gB = ProjectJson.Clone(g0); gB.Suspension.RopeCount = 7;
                if (gB.Calculations.RopeTraction == null) gB.Calculations.RopeTraction = new RopeTractionInputs(); gB.Calculations.RopeTraction.SelectedUndercutAngleDeg = 105; gB.Calculations.CounterweightMassKg = gB.Calculations.CarMassKg + 0.45 * gq;
                Check(Wire.Json(RopeTractionCalculationEngine.Geometry(gA, ctx)) == Wire.Json(RopeTractionCalculationEngine.Geometry(gB, ctx)), "Q " + gq + ": the shared geometry is identical for 4 vs 7 ropes, β 95 vs 105, q 0.50 vs 0.45 (reuse in the selection is exact)");
                var rA = new RopeTractionCalculationEngine().Calculate(gB, ctx, lib); var rB = new RopeTractionCalculationEngine().Calculate(gB, ctx, lib, RopeTractionCalculationEngine.Geometry(gA, ctx));
                Check(Wire.Json(new { rA.Checks, rA.Traction, rA.SfActual, rA.SfRequired }) == Wire.Json(new { rB.Checks, rB.Traction, rB.SfActual, rB.SfRequired }), "Q " + gq + ": a calculation with the reused geometry equals the plain calculation");
            }
            // H: CW locked at Q 1000: q is never changed (no selection, CW as entered).
            var hq = New(); SetQ(hq, 1000); ProjectPresets.Find("Calculations.CounterweightMassKg").Edit(hq, 1600.0); ProjectPresets.Apply(hq, lib, ctx);
            Check(hq.Calculations.CounterweightMassKg == 1600 && (hq.Calculations.RopeTraction == null || hq.Calculations.RopeTraction.SelectedBalanceFactor == null), "Fixture H: a locked counterweight (1600 kg) is never changed; no q selection");
            // I: sheave locked at 520: never changed.
            var hs = New(); SetQ(hs, 1600); ProjectPresets.Find(ProjectPresets.SheaveKey).Edit(hs, 520.0); var oi = ProjectPresets.Apply(hs, lib, ctx);
            Check(hs.Machine.TractionSheavePitchDiameterMm == 520 && ProjectPresets.Find(ProjectPresets.SheaveKey).IsEdited(hs), "Fixture I: a locked sheave (520 mm) is never changed (" + oi.Status + ")");

            // ---------- 7. older projects and Fill blanks ----------
            var oldText = File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json"));
            var old = ProjectJson.Deserialize(oldText); var before = ProjectJson.Serialize(old);
            Check(old.FollowsPresets == null && ProjectPresets.Fields.All(x => x.IsEdited(old)), "Older project: every project preset field counts as user-edited");
            var applied = ProjectPresets.Apply(old, lib, ctx);
            Check(applied == null && Canon(ProjectJson.Serialize(old)) == Canon(before), "Older project opens unchanged: Apply writes nothing (key-order-independent comparison)");
            Check(old.Elevator.SpeedMetresPerSecond == null && old.Calculations.CarMassKg == null, "Older project blanks stay blank (speed, car mass)");
            Check(ProjectPresets.AnyBlank(old, "GENERAL") && ProjectPresets.AnyBlank(old, null) && !ProjectPresets.AnyBlank(old, "SHAFT"), "Fill blanks is offered where blanks exist (General) and not on a complete page (Shaft)");
            ProjectPresets.FillBlanks(old, lib, ctx, "GENERAL");
            Check(old.FollowsPresets == true && old.Elevator.SpeedMetresPerSecond == 1.0 && old.Elevator.CapacityKg == 450 && ProjectPresets.Find("Elevator.CapacityKg").IsEdited(old) && old.Calculations.CarMassKg == null && old.Suspension.RopeDiameterMm == 8 && old.Suspension.RopeCount == 6, "Fill blanks (General): speed 1.0 filled; Q 450 kept and locked; other pages untouched");
            ProjectPresets.FillBlanks(old, lib, ctx, null);
            Check(old.Calculations.CarMassKg == 531.25 && old.Calculations.MachineMassKg == 290.625 && old.Calculations.CounterweightMassKg == 756.25 && old.Calculations.CarGuideForces.GuideShoeSpacingMm == 2800 && old.Calculations.SafetyPit.SafetyGearType == "Progressive" && old.Machine.TractionSheavePitchDiameterMm == 520, "Fill blanks (all): masses from Q 450 (531.25 / 290.625 / 756.25), h, gear and sheave filled");
            Check(old.Suspension.RopeDiameterMm == 8 && old.Suspension.RopeCount == 6 && old.Shaft.WidthMm == 1450 && old.Shaft.PitDepthMm == 1000 && old.Building.Floors.Count == 5, "Fill blanks keeps every existing value (ropes 6 × 8, SW 1450, pit 1000, floors)");
            Check(old.Calculations.Mode == "Basic" && old.Calculations.MachineCgSource == null, "Fill blanks keeps the Basic mode and leaves Advanced-only blanks of a Basic project");
            var gaps = ProjectJson.Deserialize(oldText); gaps.Building.Floors[0].HeightToNextMm = null; ProjectPresets.FillBlanks(gaps, lib, ctx, "FLOORS");
            Check(gaps.Building.Floors[0].HeightToNextMm == 3000 && gaps.Building.Floors[1].HeightToNextMm == 3200 && gaps.Elevator.SpeedMetresPerSecond == null, "Fill blanks (Floors): a blank interval gets 3000 mm; other intervals and pages unchanged");

            // ---------- 8. identical inputs give identical numbers ----------
            var same = ProjectJson.Deserialize(ProjectJson.Serialize(n)); same.FollowsPresets = null; same.UserEditedFields = null;
            var x1 = Wire.Json(Numbers(n)); var x2 = Wire.Json(Numbers(same));
            File.WriteAllText(Path.Combine(output, "identical-inputs.json"), x1);
            Check(x1 == x2, "A preset-built project and an old-style project with the same values give identical P1–P4, P5–P7, FF and rope / traction numbers");
            Check(Canon(ProjectJson.Serialize(ProjectJson.Deserialize(ProjectJson.Serialize(n)))) == Canon(ProjectJson.Serialize(n)), "New project file round-trips unchanged");

        }
        static void Sweep()
        {
            // ---------- 9. Q sweep: survey shaft and a Table 6 shaft ----------
            var md = new StringBuilder();
            md.Append("| Shaft | Q | car area / Table 6 max (m²) | P / CW (kg) | q | n × d (mm) | Dt (mm) | Dp (mm) · Dp/d | β (°) | Sf (required) | Dt/d | T1/T2 loading · braking · stalled | Result | Design Notes |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            var cases = new List<Tuple<string, double>>();
            foreach (var shaft0 in new[] { "survey", "table6" }) foreach (var q0 in new double[] { 320, 480, 630, 800, 1000, 1275, 1600 }) cases.Add(Tuple.Create(shaft0, q0));
            Table6Area(1300, 1800);
        var rowsArr = new object[cases.Count]; var mdArr = new string[cases.Count];
            Parallel.For(0, cases.Count, idx =>
            {
                var shaft = cases[idx].Item1; var q = cases[idx].Item2;
                    var sp9 = New(); SetQ(sp9, q);
                    if (shaft == "table6") { var dims = Table6Shaft(q); ProjectPresets.Lock(sp9, "Shaft.WidthMm"); ProjectPresets.Lock(sp9, "Shaft.DepthMm"); sp9.Shaft.WidthMm = dims[0]; sp9.Shaft.DepthMm = dims[1]; }
                    var o9 = ProjectPresets.Apply(sp9, lib, ctx); var r9 = Rope(sp9, null); var e9 = Brute(sp9, lib);
                    var notes = DesignNotes.Build(sp9, ctx, new CalculationSheetInputs { Presets = lib, GeneratedLocal = new DateTime(2026, 10, 8) });
                    var ropeNotes = notes.Recommendations.Where(x => x.StartsWith("No 1:1 arrangement")).ToList();
                    bool lowDeflector = r9.DpOverDr.HasValue && r9.DpOverDr.Value < 40 - 1e-9;
                    Check(!r9.Checks.Any(k => k.Id == "dd-deflector") && notes.Notes.Count == (lowDeflector ? 1 : 0) && (!lowDeflector || (notes.Notes[0].StartsWith("Deflector pitch diameter " + r9.Geometry.DeflectorPitchMm + " mm gives D/d = ") && notes.Notes[0].Contains("EN 81-20 5.5.2.1 requires D/d ≥ 40") && notes.Notes[0].Contains("(EN 81-50 5.12)"))) && !notes.Warnings.Concat(notes.Recommendations).Any(x => x.Contains("Deflector D/d")),
                        "Sweep " + shaft + " Q " + q + ": no deflector D/d check; Design Notes quotation " + (lowDeflector ? "present once (Dp/d " + r9.DpOverDr.Value.ToString("0.##") + ")" : "absent (Dp/d ≥ 40 or no deflector)"));
                    bool ok = ProjectPresets.Passes(r9);
                    double area = Math.Round(r9.Geometry.CarWidthMm.Value * r9.Geometry.CarDepthMm.Value / 1e6, 2); var amax = CarAreaRules.MaximumAvailableAreaM2(q);
                    Check(ok ? (o9.Status == "Selected" && e9 != null && sp9.Suspension.RopeCount == e9.Item1 && sp9.Suspension.RopeDiameterMm == e9.Item2 && r9.Inputs.UndercutAngleDeg == e9.Item3 && Qsel(sp9) == e9.Item4 && ropeNotes.Count == 0) : (o9.Status == "NoPass" && e9 == null && ropeNotes.Count == 1 && ropeNotes[0].Contains(Lower(o9.LimitingCheck)) && ropeNotes[0].Contains("2:1 roping is recommended for this rated load") && ropeNotes[0].Contains("would need MBL ≥ ")),
                        "Sweep " + shaft + " " + sp9.Shaft.WidthMm + " × " + sp9.Shaft.DepthMm + ", Q " + q + ": " + (ok ? "all rope / traction checks OK with " + sp9.Suspension.RopeCount + " × " + sp9.Suspension.RopeDiameterMm + " mm (brute force agrees)" : "no passing combination (brute force agrees); exactly one Design Notes recommendation naming '" + o9.LimitingCheck + "' with 2:1 roping and the minimum MBL at 7 ropes"));
                    if (shaft == "table6") Check(amax.HasValue && area <= Math.Round(amax.Value, 2) + 1e-9, "Table 6 shaft for Q " + q + ": car area " + area + " m² ≤ " + Math.Round(amax.Value, 2) + " m²");
                    Func<string, string> ck = id => { var k = r9.Checks.FirstOrDefault(x => x.Id == id); return k == null ? "–" : k.Actual + (k.Ok == false ? " NOT OK" : ""); };
                    Func<string, string> tc = id => { var t = r9.Traction.FirstOrDefault(x => x.Id == id); return t == null ? "–" : t.Ratio.ToString("0.###") + (t.Ok ? "" : " NOT OK"); };
                    rowsArr[idx] = (new { Shaft = shaft, sp9.Shaft.WidthMm, sp9.Shaft.DepthMm, Q = q, CarArea = area, Table6Max = amax, P = sp9.Calculations.CarMassKg, CW = sp9.Calculations.CounterweightMassKg, n = sp9.Suspension.RopeCount, d = sp9.Suspension.RopeDiameterMm, Dt = sp9.Machine.TractionSheavePitchDiameterMm, Beta = r9.Inputs.UndercutAngleDeg, Q_factor = Qsel(sp9), Status = o9.Status, o9.LimitingCheck, Checks = r9.Checks.Select(k => new { k.Id, k.Actual, k.Required, k.Ok }), Traction = r9.Traction.Select(t => new { t.Id, t.Ratio, t.EFAlpha, t.Ok }), Dp = r9.Geometry.DeflectorPitchMm, r9.DpOverDr, DesignNotesWarnings = notes.Warnings, DesignNotesRecommendations = notes.Recommendations, DesignNotesNotes = notes.Notes });
                    mdArr[idx] = ("| " + (shaft == "survey" ? "survey " : "Table 6 ") + sp9.Shaft.WidthMm + "×" + sp9.Shaft.DepthMm + " | " + q + " | " + area + " / " + (amax.HasValue ? Math.Round(amax.Value, 2).ToString() : "–") + " | " + sp9.Calculations.CarMassKg + " / " + sp9.Calculations.CounterweightMassKg + " | " + Qsel(sp9) + " | " + sp9.Suspension.RopeCount + " × " + sp9.Suspension.RopeDiameterMm + " | " + sp9.Machine.TractionSheavePitchDiameterMm + " | " + (r9.Geometry.DeflectorPitchMm.HasValue ? r9.Geometry.DeflectorPitchMm + " · " + r9.DpOverDr.Value.ToString("0.##") : "–") + " | " + r9.Inputs.UndercutAngleDeg + " | " + ck("safety-factor") + " (" + (r9.SfRequired.HasValue ? r9.SfRequired.Value.ToString("0.##") : "–") + ") | " + ck("dd-sheave") + " | " + tc("loading") + " · " + tc("braking") + " · " + tc("stalled") + " | " + (ok ? "all OK" : "NOT OK: " + o9.LimitingCheck) + " | " + (notes.Empty ? "No design notes" : String.Join(" <br> ", notes.Warnings.Select(x => "W: " + x).Concat(notes.Recommendations.Select(x => "R: " + x)).Concat(notes.Notes.Select(x => "N: " + x)))) + " |\n");
            });
            var rows = new List<object>(rowsArr);
            foreach (var line in mdArr) md.Append(line);
            Wire.Write(Path.Combine(output, "q-sweep.json"), rows);
            File.WriteAllText(Path.Combine(output, "q-sweep.md"), md.ToString());
        }
        // JSON compared independent of field order (JavaScriptSerializer field order is not guaranteed between calls).
        static string Canon(string json) { return Canonical(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(json)); }
        static string Canonical(object v)
        {
            var js = new System.Web.Script.Serialization.JavaScriptSerializer(); var map = v as IDictionary<string, object>;
            if (map != null) return "{" + String.Join(",", map.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => js.Serialize(k) + ":" + Canonical(map[k]))) + "}";
            var list = v as System.Collections.IEnumerable; if (list != null && !(v is string)) return "[" + String.Join(",", list.Cast<object>().Select(Canonical)) + "]";
            return js.Serialize(v);
        }
        static string Lower(string t) { return String.IsNullOrEmpty(t) ? t : t.Substring(0, 1).ToLowerInvariant() + t.Substring(1); }
        // Largest car (Table 6) on a shaft whose depth gives the 400 mm deflector (SD 1800–2200); else SD 1400–1600 (250 mm deflector).
        static double[] Table6Shaft(double q)
        {
            var amax = Math.Round(CarAreaRules.MaximumAvailableAreaM2(q).Value, 2); double[] best = null; double bestArea = -1;
            foreach (var depths in new[] { new double[] { 1800, 2000, 2200 }, new double[] { 1400, 1600 } })
            {
                foreach (var sd in depths) for (double sw = 1300; sw <= 2200; sw += 100)
                {
                    double? area = Table6Area(sw, sd);
                    if (!area.HasValue) continue;
                    if (area.Value <= amax + 1e-9 && area.Value > bestArea) { bestArea = area.Value; best = new[] { sw, sd }; }
                }
                if (best != null) return best;
            }
            return new double[] { 1300, 1400 };
        }
        // Car area of every candidate shaft, computed once (in parallel) and shared by the seven capacities.
        static Dictionary<string, double?> table6Areas;
        static double? Table6Area(double sw, double sd)
        {
            if (table6Areas == null)
            {
                var keys = new List<double[]>();
                foreach (var depth in new double[] { 1800, 2000, 2200, 1400, 1600 }) for (double w = 1300; w <= 2200; w += 100) keys.Add(new[] { w, depth });
                var areas = new double?[keys.Count];
                Parallel.For(0, keys.Count, k =>
                {
                    var t = ProjectPresets.CreateNew(lib, null, null, null); /* geometry only: no rope search */ t.Shaft.WidthMm = keys[k][0]; t.Shaft.DepthMm = keys[k][1];
                    var g = RopeTractionCalculationEngine.Geometry(t, ctx);
                    if (g.CarWidthMm.HasValue) areas[k] = Math.Round(g.CarWidthMm.Value * g.CarDepthMm.Value / 1e6, 2);
                });
                var map = new Dictionary<string, double?>();
                for (int k = 0; k < keys.Count; k++) map[keys[k][0] + "x" + keys[k][1]] = areas[k];
                table6Areas = map;
            }
            return table6Areas[sw + "x" + sd];
        }
        static double Qsel(ElevatorProject p) { var rt = p.Calculations == null ? null : p.Calculations.RopeTraction; return rt != null && rt.SelectedBalanceFactor.HasValue ? rt.SelectedBalanceFactor.Value : lib.CounterweightBalanceFactor.Value; }
        // Independent brute force of the joint selection order (least change first): Dt steps outermost, then q steps, then β steps,
        // then d from the ladder diameter downwards (largest first) and larger d ascending (d ≤ Dt / 40), then n 4…7. Only unlocked
        // values vary. Returns (n, d, β, q) of the first combination whose checks are all OK, else null.
        static Tuple<int, double, double, double> Brute(ElevatorProject src, PresetLibrary l)
        {
            bool nFree = !ProjectPresets.Find(ProjectPresets.RopeCountKey).IsEdited(src), dFree = !ProjectPresets.Find(ProjectPresets.RopeDiameterKey).IsEdited(src), bFree = !PresetFields.UndercutAngle.IsEdited(src) && !PresetFields.GrooveType.IsEdited(src);
            bool qFree = !ProjectPresets.Find("Calculations.CounterweightMassKg").IsEdited(src), sFree = !ProjectPresets.Find(ProjectPresets.SheaveKey).IsEdited(src);
            double dt0 = src.Machine.TractionSheavePitchDiameterMm ?? 520; double ladder = dFree ? l.RopesFor(src.Elevator.CapacityKg).DiameterMm : src.Suspension.RopeDiameterMm.Value;
            var sheaves = sFree ? new[] { dt0 }.Concat(l.TractionSheavePitchSteps.Where(v => v > dt0)).ToList() : new List<double> { dt0 };
            var qs = qFree ? new[] { l.CounterweightBalanceFactor.Value }.Concat(l.BalanceFactorSteps.Where(v => v != l.CounterweightBalanceFactor.Value)).ToList() : new List<double> { Double.NaN };
            var betas = bFree ? l.UndercutAngleStepsDeg : new List<double> { Convert.ToDouble(PresetFields.UndercutAngle.Effective(new PresetContext { Project = src, Library = l })) };
            var ns = nFree ? Enumerable.Range(4, 4).ToList() : new List<int> { src.Suspension.RopeCount.Value };
            var bruteGeometry = new Dictionary<string, RopeTractionGeometry>();
            foreach (var dt in sheaves) { bool invalidSheave = false; foreach (var q in qs) foreach (var b in betas)
            {
                if (invalidSheave) break;
                var table = l.Ropes.Table.Select(r => r.DiameterMm).Where(v => v <= dt / 40 + 1e-9).ToList();
                var ds = dFree ? table.Where(v => v <= ladder + 1e-9).OrderByDescending(v => v).Concat(table.Where(v => v > ladder + 1e-9).OrderBy(v => v)).ToList() : new List<double> { src.Suspension.RopeDiameterMm.Value };
                foreach (var d in ds) foreach (var n in ns)
                {
                    var t = ProjectJson.Clone(src); t.Suspension.RopeCount = n; t.Suspension.RopeDiameterMm = d; t.Machine.TractionSheavePitchDiameterMm = dt;
                    if (t.Calculations.RopeTraction == null) t.Calculations.RopeTraction = new RopeTractionInputs();
                    if (bFree) t.Calculations.RopeTraction.SelectedUndercutAngleDeg = b;
                    if (qFree) t.Calculations.CounterweightMassKg = t.Calculations.CarMassKg + q * t.Elevator.CapacityKg;
                    RopeTractionGeometry geo; string gk = dt + "|" + d; if (!bruteGeometry.TryGetValue(gk, out geo)) { geo = RopeTractionCalculationEngine.Geometry(t, ctx); bruteGeometry[gk] = geo; }
                    var r = new RopeTractionCalculationEngine().Calculate(t, ctx, l, geo);
                    if (r.Omissions.Any(o => o.Kind == "INVALID_GEOMETRY" || o.Kind == "INVALID_INPUT")) { invalidSheave = true; break; }
                    if (r.Omissions.Count == 0 && r.Traction.Count >= 2 && r.Checks.Any(k => k.Id == "safety-factor") && r.Checks.All(k => k.Ok != false) && r.Traction.All(x => x.Ok)) return Tuple.Create(n, d, b, qFree ? q : l.CounterweightBalanceFactor.Value);
                }
            } }
            return null;
        }
    }
}
