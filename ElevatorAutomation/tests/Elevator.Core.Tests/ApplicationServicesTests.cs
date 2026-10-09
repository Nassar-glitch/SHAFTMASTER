using System;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Presets;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Xunit;

namespace Elevator.Core.Tests
{
    // The UI-free application services of ARCHITECTURE-PLAN P4 (Elevator.Core.Application): project session, preset flow
    // (lock / follow / reset / fill blanks) and live calculation (what an input change recalculates).
    public class ApplicationServicesTests
    {
        static readonly DesignContext Ctx = TestData.Context();
        static readonly PresetFlow Presets = PresetFlow.Load(Path.Combine(TestData.Bin, "presets.json"), Ctx);
        static string SeedPath { get { return Path.Combine(TestData.Bin, "reviewed-example.elevator.json"); } }
        static ProjectSession NewSession() { return new ProjectSession(ProjectSession.NewProject(SeedPath, Presets.Library, Ctx)); }
        static ProjectSession ExampleSession() { return new ProjectSession(ProjectJson.Deserialize(File.ReadAllText(SeedPath))); }
        static PresetField Field(string key) { var f = PresetFlow.Managed(key); Assert.NotNull(f); return f; }

        // ---------- project session ----------
        [Fact]
        public void New_project_follows_the_presets_and_is_saved_state()
        {
            var s = NewSession();
            Assert.True(ProjectPresets.Follows(s.Project));
            Assert.False(s.Dirty);
            Assert.Null(s.FilePath);
            Assert.Equal(DrawingViews.All, s.Views);
            Assert.True(s.Arrange);
            Assert.NotNull(s.Project.Elevator.CapacityKg);
            Assert.Equal(Presets.Library.NewProject.CapacityKg, s.Project.Elevator.CapacityKg);
        }

        [Fact]
        public void Save_and_open_clear_the_unsaved_state_and_keep_the_project_bytes()
        {
            var s = NewSession(); s.Dirty = true;
            string path = Path.Combine(TestData.NewOutput("session-save"), "Project.elevator.json");
            s.Save(path);
            Assert.False(s.Dirty);
            Assert.Equal(Path.GetFullPath(path), s.FilePath);
            string saved = File.ReadAllText(path);
            Assert.Equal(JsonMembers.Sort(ProjectJson.Serialize(s.Project)), JsonMembers.Sort(saved));
            s.Dirty = true;
            s.Open(path);
            Assert.False(s.Dirty);
            Assert.Equal(JsonMembers.Sort(saved), JsonMembers.Sort(ProjectJson.Serialize(s.Project)));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"));
        }

        [Fact]
        public void Moving_an_intermediate_floor_makes_the_project_unsaved_and_the_ends_stay_fixed()
        {
            var s = NewSession();
            var floors = s.Project.Building.Floors;
            Assert.True(floors.Count >= 4);
            string moved = floors[1].FloorId;
            s.MoveIntermediate(moved, 1);
            Assert.True(s.Dirty);
            Assert.Equal(moved, s.Project.Building.Floors[2].FloorId);
            Assert.Throws<ArgumentException>(() => s.MoveIntermediate(s.Project.Building.Floors[0].FloorId, 1));
            Assert.Throws<ArgumentException>(() => s.MoveIntermediate(s.Project.Building.Floors.Last().FloorId, -1));
        }

        [Fact]
        public void Floor_row_edits_validate_and_make_the_project_unsaved()
        {
            var s = NewSession(); int changed = 0;
            var row = new FloorRow(s, s.Project.Building.Floors[0], () => changed++);
            row.Height = "3100";
            Assert.True(s.Dirty);
            Assert.Equal(3100, s.Project.Building.Floors[0].HeightToNextMm);
            Assert.Equal(1, changed);
            Assert.Throws<ArgumentException>(() => row.Height = "abc");
            var last = new FloorRow(s, s.Project.Building.Floors.Last(), () => changed++);
            Assert.Throws<ArgumentException>(() => last.Height = "3000");
        }

        // ---------- preset flow ----------
        [Fact]
        public void An_edited_field_locks_and_the_project_becomes_unsaved()
        {
            var s = NewSession(); var q = Field("Elevator.CapacityKg");
            Assert.False(q.IsEdited(s.Project));
            Presets.Edited(s, q);
            Assert.True(q.IsEdited(s.Project));
            Assert.True(s.Dirty);
        }

        [Fact]
        public void Unlocked_fields_follow_a_locked_driver_and_locked_fields_keep_their_value()
        {
            var s = NewSession(); var q = Field("Elevator.CapacityKg"); var cw = Field("Calculations.CounterweightMassKg"); var car = Field("Calculations.CarMassKg");
            double? cwBefore = s.Project.Calculations.CounterweightMassKg;
            s.Project.Elevator.CapacityKg = 1000; Presets.Edited(s, q);
            Assert.True(Presets.Apply(s.Project, true));
            Assert.Equal(1000, s.Project.Elevator.CapacityKg);
            Assert.NotEqual(cwBefore, s.Project.Calculations.CounterweightMassKg);
            Assert.Equal(Presets.Library.CounterweightFor(s.Project.Calculations.CarMassKg, 1000), s.Project.Calculations.CounterweightMassKg);
            // A locked follower keeps the typed value when its driver changes.
            s.Project.Calculations.CarMassKg = 777; Presets.Edited(s, car);
            s.Project.Elevator.CapacityKg = 1275; Presets.Apply(s.Project, false);
            Assert.Equal(777, s.Project.Calculations.CarMassKg);
            Assert.False(cw.IsEdited(s.Project));
            Assert.NotNull(Presets.LastRopeSizing);
        }

        [Fact]
        public void Reset_unlocks_and_the_field_follows_the_preset_again()
        {
            var s = NewSession(); var car = Field("Calculations.CarMassKg");
            double? preset = s.Project.Calculations.CarMassKg;
            s.Project.Calculations.CarMassKg = 999; Presets.Edited(s, car); s.Dirty = false;
            Presets.Apply(s.Project, true);
            Assert.Equal(999, s.Project.Calculations.CarMassKg);
            Presets.Reset(s, car);
            Assert.False(car.IsEdited(s.Project));
            Assert.True(s.Dirty);
            Presets.Apply(s.Project, true);
            Assert.Equal(preset, s.Project.Calculations.CarMassKg);
        }

        [Fact]
        public void Apply_does_nothing_without_a_library_or_for_a_project_that_does_not_follow()
        {
            var none = new PresetFlow(null, "missing", Ctx);
            var s = NewSession();
            Assert.False(none.Apply(s.Project, true));
            Assert.False(none.CanFillBlanks(s.Project, null));
            Assert.Null(none.PersonsFor(630));
            var e = Assert.Throws<ArgumentException>(() => none.EnsureLibrary());
            Assert.Equal("The preset library could not be read: missing", e.Message);
            Assert.Throws<ArgumentException>(() => none.FillBlanks(s, null));
            var old = ExampleSession(); old.Project.FollowsPresets = null;
            Assert.False(Presets.Apply(old.Project, true));
        }

        [Fact]
        public void Fill_blanks_sets_only_blank_fields_of_the_page_and_the_project_then_follows()
        {
            var s = ExampleSession(); s.Project.FollowsPresets = null;
            double? q = s.Project.Elevator.CapacityKg;
            s.Project.Calculations.CarMassKg = null;
            Assert.True(Presets.CanFillBlanks(s.Project, "CALCULATIONS"));
            Presets.FillBlanks(s, "CALCULATIONS");
            Assert.True(s.Dirty);
            Assert.True(ProjectPresets.Follows(s.Project));
            Assert.NotNull(s.Project.Calculations.CarMassKg);
            Assert.False(Field("Calculations.CarMassKg").IsEdited(s.Project));
            // Non-blank values are kept and locked.
            Assert.Equal(q, s.Project.Elevator.CapacityKg);
            Assert.True(Field("Elevator.CapacityKg").IsEdited(s.Project));
        }

        [Fact]
        public void Persons_follow_the_capacity()
        {
            Assert.Equal(8, Presets.PersonsFor(630));
            Assert.Equal(13, Presets.PersonsFor(1000));
            Assert.Null(Presets.PersonsFor(null));
            Assert.Equal(Presets.Library, Presets.Context(NewSession().Project).Library);
        }

        // ---------- live calculation ----------
        [Fact]
        public void An_input_change_marks_the_results_it_feeds()
        {
            Assert.Equal(InputEffect.None, LiveCalculation.EffectOf("Report.ClientName"));
            Assert.Equal(InputEffect.Design | InputEffect.Chassis | InputEffect.RopeTraction, LiveCalculation.EffectOf("Machine.TractionSheavePitchDiameterMm"));
            foreach (var path in new[] { "Elevator.CapacityKg", "Shaft.WidthMm", "Shaft.DepthMm" })
                Assert.Equal(InputEffect.Design | InputEffect.Chassis | InputEffect.RopeTraction | InputEffect.CarGuideForces, LiveCalculation.EffectOf(path));
        }

        [Fact]
        public void Recalculate_gives_every_result_and_keeps_current_results()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            live.Recalculate(s.Project);
            Assert.NotNull(live.Chassis.Result);
            Assert.NotNull(live.Chassis.Result.Reactions);
            Assert.NotNull(live.RopeTraction);
            Assert.NotNull(live.SafetyPit);
            Assert.NotNull(live.CarGuideForces);
            Assert.False(live.Running);
            var chassis = live.Chassis.Result; var rope = live.RopeTraction; var pit = live.SafetyPit; var ff = live.CarGuideForces;
            live.Recalculate(s.Project);
            Assert.Same(chassis, live.Chassis.Result);
            Assert.Same(rope, live.RopeTraction);
            Assert.Same(pit, live.SafetyPit);
            Assert.Same(ff, live.CarGuideForces);
            Assert.False(live.ChassisStale(s.Project));
            Assert.False(live.SafetyPitStale(s.Project));
            Assert.False(live.CarGuideForcesStale(s.Project));
        }

        [Fact]
        public void A_changed_input_recalculates_only_the_results_that_use_it()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            live.Recalculate(s.Project);
            var chassis = live.Chassis.Result; var rope = live.RopeTraction; var pit = live.SafetyPit; var ff = live.CarGuideForces;
            // h is an FF1 / FF2 input only.
            s.Project.Calculations.CarGuideForces.GuideShoeSpacingMm = 3100; Presets.Edited(s, Field(LiveCalculation.GuideShoeSpacingKey));
            Assert.True(live.CarGuideForcesStale(s.Project));
            Assert.False(live.ChassisStale(s.Project));
            Assert.False(live.SafetyPitStale(s.Project));
            live.Recalculate(s.Project);
            Assert.NotSame(ff, live.CarGuideForces);
            Assert.Same(chassis, live.Chassis.Result);
            Assert.Same(rope, live.RopeTraction);
            Assert.Same(pit, live.SafetyPit);
            // The empty car mass feeds P1–P4 and the pit loads.
            s.Project.Calculations.CarMassKg = 650; Presets.Edited(s, Field("Calculations.CarMassKg"));
            Assert.True(live.ChassisStale(s.Project));
            Assert.True(live.SafetyPitStale(s.Project));
            live.Recalculate(s.Project);
            Assert.NotSame(chassis, live.Chassis.Result);
            Assert.NotSame(pit, live.SafetyPit);
        }

        [Fact]
        public void Invalid_text_clears_the_value_and_the_dependent_results_show_the_message()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            live.Recalculate(s.Project);
            object parsed = live.EnterChassisText("Calculations.CarMassKg", "Empty car mass P (kg)", "12a", true);
            Assert.Null(parsed);
            Assert.Equal("12a", live.ChassisDrafts["Calculations.CarMassKg"]);
            Assert.Equal("Empty car mass P (kg): finite number required (mm or kg as labelled).", live.ChassisDraftErrors["Calculations.CarMassKg"]);
            Assert.True(live.RelevantDraftErrors(s.Project).ContainsKey("Calculations.CarMassKg"));
            // A locked field (an unlocked one takes its preset again on the next run).
            s.Project.Calculations.CarMassKg = null; Presets.Edited(s, Field("Calculations.CarMassKg"));
            live.Recalculate(s.Project);
            Assert.Null(s.Project.Calculations.CarMassKg);
            Assert.Null(live.Chassis.Result);
            Assert.Equal("INVALID_INPUT", live.SafetyPit.P5.Status);
            Assert.Null(live.SafetyPit.P5.PerComponentLoadN);
            Assert.Contains(live.SafetyPit.P5.Issues, i => i.Field == "Calculations.CarMassKg" && i.Message.EndsWith("Malformed text is not an omitted mass."));
            // A valid entry clears the message.
            Assert.Equal(640.0, live.EnterChassisText("Calculations.CarMassKg", "Empty car mass P (kg)", "640", true));
            Assert.Empty(live.ChassisDraftErrors);
            Assert.Null(live.EnterChassisText("Calculations.CarMassKg", "Empty car mass P (kg)", " ", true));
            Assert.Equal("text", live.EnterChassisText("Calculations.LoadCase", "Load case", "text", false));
        }

        [Fact]
        public void Basic_mode_ignores_invalid_text_of_inputs_it_does_not_use()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            Assert.Equal("Basic", CalculationModes.Resolve(s.Project.Calculations));
            live.EnterChassisText("Calculations.MachineCgOffsetXMm", "Machine CG ΔX (mm)", "x", true);
            Assert.Empty(live.RelevantDraftErrors(s.Project));
            s.Project.Calculations.Mode = "Advanced";
            Assert.Single(live.RelevantDraftErrors(s.Project));
        }

        [Fact]
        public void Invalid_guide_shoe_spacing_gives_invalid_FF_and_reset_to_preset_drops_the_text()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            Assert.Null(live.EnterGuideShoeSpacing("Guide-shoe spacing h (mm)", "3,1"));
            Assert.StartsWith("Guide-shoe spacing h (mm): invalid text", live.CarGuideForceDraftError);
            s.Project.Calculations.CarGuideForces.GuideShoeSpacingMm = null;
            live.Recalculate(s.Project);
            Assert.Equal("INVALID_INPUT", live.CarGuideForces.Status);
            Assert.Null(live.CarGuideForces.FF1DesignN);
            Assert.Contains(live.CarGuideForces.Issues, i => i.Field == LiveCalculation.GuideShoeSpacingKey);
            live.ClearDraft(LiveCalculation.GuideShoeSpacingKey);
            Assert.Null(live.CarGuideForceDraft);
            Assert.Null(live.CarGuideForceDraftError);
            Assert.Equal(3100.0, live.EnterGuideShoeSpacing("Guide-shoe spacing h (mm)", "3100"));
        }

        [Fact]
        public void Invalid_participating_percentage_counts_only_when_the_fraction_applies()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            const string path = "Calculations.SafetyPit.CarEventAdditionalMassPercent";
            Assert.Null(live.EnterSafetyPitPercentage(path, "Car participating mass (%)", "abc"));
            Assert.Equal("Car participating mass (%): finite percentage required.", live.SafetyPitDraftErrors[path]);
            Assert.Empty(live.RelevantSafetyPitDraftErrors(s.Project));
            s.Project.Calculations.SafetyPit.CarAdditionalMassAllocation = "FractionParticipates";
            s.Project.Calculations.CarSideAdditionalSuspendedMassKg = 50;
            Assert.True(live.RelevantSafetyPitDraftErrors(s.Project).ContainsKey(path));
            Assert.Equal(40.0, live.EnterSafetyPitPercentage(path, "Car participating mass (%)", "40"));
            Assert.Empty(live.SafetyPitDraftErrors);
        }

        [Fact]
        public void Resets_clear_the_results_and_the_typed_text()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            live.Recalculate(s.Project); live.Validate(s);
            live.EnterChassisText("Calculations.CarMassKg", "P", "x", true);
            live.EnterSafetyPitPercentage("Calculations.SafetyPit.CarEventAdditionalMassPercent", "%", "x");
            live.EnterGuideShoeSpacing("h", "x");
            var chassis = live.Chassis;
            live.InputsReplaced();
            Assert.Null(live.Design);
            Assert.Null(live.Chassis.Result);
            Assert.Same(chassis, live.Chassis);
            Assert.Null(live.RopeTraction); Assert.Null(live.SafetyPit); Assert.Null(live.CarGuideForces); Assert.Null(live.CarGuideForceGeometry);
            Assert.Empty(live.ChassisDrafts); Assert.Empty(live.ChassisDraftErrors); Assert.Empty(live.SafetyPitDraftErrors);
            Assert.Null(live.CarGuideForceDraftError);
            live.Recalculate(s.Project);
            live.Reset();
            Assert.NotSame(chassis, live.Chassis);
            Assert.Null(live.Chassis.Result);
            Assert.Null(live.SafetyPit);
        }

        [Fact]
        public void Validation_needs_a_view_and_a_nested_recalculation_is_skipped()
        {
            var s = NewSession(); var live = new LiveCalculation(Ctx, Presets);
            Assert.True(live.Validate(s).CanGenerate);
            var design = live.Validate(s);
            Assert.Same(design, live.Design);
            s.Views = 0;
            var e = Assert.Throws<ArgumentException>(() => live.Validate(s));
            Assert.Equal("Select at least one view.", e.Message);
            int applied = 0; bool runningInside = false;
            live.Recalculate(s.Project, () => { applied++; runningInside = live.Running; live.Recalculate(s.Project, () => applied++); });
            Assert.Equal(1, applied);
            Assert.True(runningInside);
            Assert.False(live.Running);
            // A failing preset step does not stop the calculation.
            var other = new LiveCalculation(Ctx, Presets);
            other.Recalculate(s.Project, () => { throw new InvalidOperationException("preset step"); });
            Assert.NotNull(other.Chassis.Result);
        }
    }
}
