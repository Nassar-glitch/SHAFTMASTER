using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Company;
using Elevator.Core.Layouts;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Reporting.Sheet;
using Xunit;

namespace Elevator.Core.Tests
{
    // Cloud L2a (DESIGN-FLOW "Layouts and PDF"): the company name and title-block presets, the UI-free title-block data and the
    // file-name builder COMPANY-TYPE-CW-W{SW}D{SD}-PROJECT-MODEL{NN}.
    public class LayoutsTests
    {
        static readonly DesignContext Ctx = TestData.Context();
        static string PresetsPath { get { return Path.Combine(TestData.Bin, "presets.json"); } }
        static readonly PresetLibrary Lib = PresetLibrary.Load(PresetsPath);
        static readonly DateTime Day = new DateTime(2026, 10, 12, 9, 30, 0);

        static ElevatorProject New()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(Lib, Ctx, seed.Counterweight, seed.Rails.BracketSource);
        }

        static PresetLibrary Variant(Func<string, string> change) { return PresetLibrary.Parse(change(File.ReadAllText(PresetsPath))); }

        static void Floors(ElevatorProject p, params object[] nameHeight)
        {
            p.Building.Floors.Clear();
            for (int i = 0; i < nameHeight.Length; i += 2)
                p.Building.Floors.Add(new FloorDefinition { Name = (string)nameHeight[i], HeightToNextMm = (double?)nameHeight[i + 1] });
        }

        // ---------- presets ----------
        [Fact]
        public void Shipped_library_holds_the_title_block_presets_and_no_company_name()
        {
            Assert.DoesNotContain("CompanyName", File.ReadAllText(PresetsPath));
            Assert.Equal("SIMPLEX COLLECTIVE DOWN.", Lib.TitleBlock.Control);
            Assert.Equal("3 PHASE 380 V. 50 Hz.", Lib.TitleBlock.Supply);
        }

        [Fact]
        public void An_older_library_without_the_new_members_takes_the_shipped_defaults()
        {
            var old = Variant(s => s.Replace("\"TitleBlock\": { \"Control\": \"SIMPLEX COLLECTIVE DOWN.\", \"Supply\": \"3 PHASE 380 V. 50 Hz.\", \"TypeOfWork\": \"أعمال مصاعد\" },", ""));
            Assert.Null(old.TitleBlock);
            Assert.Equal(TitleBlockPreset.DefaultControl, old.TitleBlockOrDefault().Control);
            Assert.Equal(TitleBlockPreset.DefaultSupply, old.TitleBlockOrDefault().Supply);
            // Not calculation inputs: the library fingerprint is unchanged, so no stored result goes stale.
            Assert.Equal(old.Hash(), Lib.Hash());
            Assert.Equal(Variant(s => s.Replace("SIMPLEX COLLECTIVE DOWN.", "DUPLEX")).Hash(), Lib.Hash());
        }

        [Fact]
        public void The_title_block_presets_are_validated()
        {
            var e = Assert.Throws<ArgumentException>(() => Variant(s => s.Replace("\"Supply\": \"3 PHASE 380 V. 50 Hz.\"", "\"Supply\": null")));
            Assert.Contains("TitleBlock needs Control and Supply", e.Message);
        }

        [Fact]
        public void The_new_presets_round_trip_through_json()
        {
            var json = Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Serialize(Lib);
            var back = PresetLibrary.Parse(json);
            Assert.Equal(Lib.TitleBlock.Control, back.TitleBlock.Control);
            Assert.Equal(Lib.TitleBlock.Supply, back.TitleBlock.Supply);
            Assert.Equal(Lib.Hash(), back.Hash());
        }

        // ---------- lock and follow (control, supply) ----------
        [Fact]
        public void Control_and_supply_follow_the_preset_until_edited_then_lock_and_reset()
        {
            var p = New();
            var bytes = ProjectJson.Serialize(p);
            Assert.Equal("SIMPLEX COLLECTIVE DOWN.", TitleBlockFields.Effective(TitleBlockFields.Control, p, Lib));
            Assert.Equal("3 PHASE 380 V. 50 Hz.", TitleBlockFields.Effective(TitleBlockFields.Supply, p, Lib));
            Assert.False(TitleBlockFields.Control.IsEdited(p));
            Assert.False(p.Extensions.ContainsKey("titleBlock"));
            // An unlocked value follows an upstream (library) change.
            var duplex = Variant(s => s.Replace("SIMPLEX COLLECTIVE DOWN.", "DUPLEX COLLECTIVE."));
            Assert.Equal("DUPLEX COLLECTIVE.", TitleBlockFields.Effective(TitleBlockFields.Control, p, duplex));
            // Editing locks: the library no longer moves it; the other field still follows.
            TitleBlockFields.Control.Edit(p, "SIMPLEX SELECTIVE.");
            Assert.True(TitleBlockFields.Control.IsEdited(p));
            Assert.Contains(TitleBlockFields.ControlKey, p.UserEditedFields);
            Assert.Equal("SIMPLEX SELECTIVE.", TitleBlockFields.Effective(TitleBlockFields.Control, p, duplex));
            Assert.Equal("3 PHASE 380 V. 50 Hz.", TitleBlockFields.Effective(TitleBlockFields.Supply, p, duplex));
            // A blank edit is kept as blank (never replaced by a placeholder).
            TitleBlockFields.Supply.Edit(p, "");
            Assert.Equal("", TitleBlockFields.Effective(TitleBlockFields.Supply, p, Lib));
            // Reset returns to the preset and removes the stored value; with both reset the project is unchanged byte for byte.
            TitleBlockFields.Supply.Reset(p);
            TitleBlockFields.Control.Reset(p);
            Assert.Equal("DUPLEX COLLECTIVE.", TitleBlockFields.Effective(TitleBlockFields.Control, p, duplex));
            Assert.False(TitleBlockFields.Control.IsEdited(p));
            Assert.False(p.Extensions.ContainsKey("titleBlock"));
            Assert.Equal(bytes, ProjectJson.Serialize(p));
        }

        [Fact]
        public void Edited_title_block_values_round_trip_through_the_project_file()
        {
            var p = New();
            TitleBlockFields.Control.Edit(p, "DUPLEX COLLECTIVE.");
            TitleBlockFields.Supply.Edit(p, "1 PHASE 220 V. 50 Hz.");
            var back = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.True(TitleBlockFields.Control.IsEdited(back));
            Assert.Equal("DUPLEX COLLECTIVE.", TitleBlockFields.Effective(TitleBlockFields.Control, back, Lib));
            Assert.Equal("1 PHASE 220 V. 50 Hz.", TitleBlockFields.Effective(TitleBlockFields.Supply, back, Lib));
            Assert.Equal(ProjectJson.Serialize(back), ProjectJson.Serialize(ProjectJson.Deserialize(ProjectJson.Serialize(back))));
            // Editing after a round trip (extension read back as a dictionary) still works, and Reset removes it.
            TitleBlockFields.Control.Edit(back, "X");
            Assert.Equal("X", TitleBlockFields.Effective(TitleBlockFields.Control, back, Lib));
            TitleBlockFields.Control.Reset(back);
            TitleBlockFields.Supply.Reset(back);
            Assert.False(back.Extensions.ContainsKey("titleBlock"));
        }

        [Fact]
        public void An_older_project_follows_the_title_block_presets_and_the_fields_are_found_by_key()
        {
            var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            Assert.NotEqual(true, p.FollowsPresets);
            Assert.Equal("SIMPLEX COLLECTIVE DOWN.", TitleBlockFields.Effective(TitleBlockFields.Control, p, Lib));
            Assert.Same(TitleBlockFields.Control, ProjectPresets.Find(TitleBlockFields.ControlKey));
            Assert.Same(TitleBlockFields.Supply, ProjectPresets.Find(TitleBlockFields.SupplyKey));
            Assert.Equal(InputEffect.None, LiveCalculation.EffectOf(TitleBlockFields.ControlKey));
            Assert.Equal(InputEffect.None, LiveCalculation.EffectOf(TitleBlockFields.SupplyKey));
            // Applying the presets never writes the title-block values (they are not materialized).
            var n = New();
            ProjectPresets.Apply(n, Lib, Ctx);
            Assert.False(n.Extensions.ContainsKey("titleBlock"));
        }

        // ---------- title block ----------
        [Fact]
        public void A_complete_project_fills_every_field_with_a_source()
        {
            var p = New();
            p.Info.Name = "  VILLA bx12 ";
            p.Report = new ProjectReportDetails { ClientName = "Mr. Client", PreparedBy = "A. Engineer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver", Revision = "1" };
            p.Elevator.CapacityKg = 450;
            p.Elevator.SpeedMetresPerSecond = 1.0;
            Floors(p, "G", 3100.0, "1", 3060.0, "2", 3120.0, "3", null);
            var t = TitleBlockData.Build(p, Lib, null, null, null, Day);
            Assert.Equal("", t.CompanyName); // no company name entered: left out
            Assert.Equal("VILLA bx12", t.ProjectName);
            Assert.Equal("Mr. Client", t.Client);
            Assert.Equal("12/10/2026", t.Date);
            Assert.Equal("A. Engineer", t.PreparedBy);
            Assert.Equal("B. Checker", t.CheckedBy);
            Assert.Equal("C. Approver", t.ApprovedBy);
            Assert.Equal("01", t.Revision);
            Assert.Equal("1.00 M/SEC.", t.RatedSpeed);
            Assert.Equal("450 KG. - 6 PASSENGERS.", t.RatedLoad);
            Assert.Equal("9.28 METRES.", t.Travel);
            Assert.Equal("4", t.Stops);
            Assert.Equal("(G-1-2-3)", t.Floors);
            Assert.Equal("SIMPLEX COLLECTIVE DOWN.", t.Control);
            Assert.Equal("3 PHASE 380 V. 50 Hz.", t.Supply);
        }

        [Fact]
        public void Fields_without_a_source_are_empty_never_a_placeholder()
        {
            var p = New();
            p.Info.Name = null;
            p.Report = null;
            p.Elevator.CapacityKg = null;
            p.Elevator.SpeedMetresPerSecond = null;
            var t = TitleBlockData.Build(p, Lib, null, null, null, null);
            foreach (var f in typeof(TitleBlockData).GetFields().Where(f => f.FieldType == typeof(string)))
                Assert.NotNull(f.GetValue(t));
            Assert.Equal("", t.DrawingTitle);
            Assert.Equal("AS SHOWN", t.Scale); // every view carries its own scale
            Assert.Equal("1", t.SheetNo);
            Assert.Equal("", t.ProjectNumber);
            Assert.Equal("", t.Phase);
            Assert.Equal("", t.ProjectName);
            Assert.Equal("", t.Client);
            Assert.Equal("", t.Date);
            Assert.Equal("", t.PreparedBy);
            Assert.Equal("", t.CheckedBy);
            Assert.Equal("", t.ApprovedBy);
            Assert.Equal("", t.CompanyName);
            Assert.Equal("00", t.Revision); // blank revision is the first issue: 00 on every document and in the file names
            Assert.Equal("", t.RatedSpeed);
            Assert.Equal("", t.RatedLoad);
            foreach (var load in new[] { t.P1, t.P2, t.P3, t.P4, t.P5, t.P6, t.P7 })
                Assert.Equal("", load);
            // Whitespace-only texts are blank too.
            p.Info.Name = "   ";
            p.Report = new ProjectReportDetails { ClientName = " ", PreparedBy = "\t", Revision = "  " };
            t = TitleBlockData.Build(p, Lib, null, null, null, null);
            Assert.Equal("", t.ProjectName);
            Assert.Equal("", t.Client);
            Assert.Equal("", t.PreparedBy);
            Assert.Equal("00", t.Revision);
            // No remark text anywhere.
            foreach (var f in typeof(TitleBlockData).GetFields().Where(f => f.FieldType == typeof(string)))
            {
                string v = (string)f.GetValue(t);
                foreach (var word in new[] { "preset", "assumed", "catalog", "review", "preliminary", "n/a", "-" + "-" })
                    Assert.DoesNotContain(word, v, StringComparison.OrdinalIgnoreCase);
            }
            // Without a library: no company, persons, control or supply.
            t = TitleBlockData.Build(p, null, null, null, null, null);
            Assert.Equal("", t.CompanyName);
            Assert.Equal("", t.Control);
            Assert.Equal("", t.Supply);
            Assert.NotNull(TitleBlockData.Build(null, Lib, null, null, null, null));
        }

        [Theory]
        [InlineData(1.0, "1.00 M/SEC.")]
        [InlineData(0.63, "0.63 M/SEC.")]
        [InlineData(1.6, "1.60 M/SEC.")]
        [InlineData(0.0, "")]
        [InlineData(-1.0, "")]
        [InlineData(double.NaN, "")]
        public void Rated_speed_text(double v, string text)
        {
            Assert.Equal(text, TitleBlockData.Speed(v));
        }

        [Theory]
        [InlineData(450.0, 6, "450 KG. - 6 PASSENGERS.")]
        [InlineData(630.0, 8, "630 KG. - 8 PASSENGERS.")]
        [InlineData(100.0, 1, "100 KG. - 1 PASSENGER.")]
        [InlineData(0.0, 0, "")]
        public void Rated_load_text(double q, int persons, string text)
        {
            Assert.Equal(text, TitleBlockData.RatedLoadText(q, persons));
        }

        [Fact]
        public void Rated_load_persons_follow_the_library_person_mass()
        {
            var p = New();
            p.Elevator.CapacityKg = 630;
            Assert.Equal("630 KG. - 8 PASSENGERS.", TitleBlockData.Build(p, Lib, null, null, null, null).RatedLoad);
            p.Elevator.CapacityKg = 1000;
            Assert.Equal("1000 KG. - 13 PASSENGERS.", TitleBlockData.Build(p, Lib, null, null, null, null).RatedLoad);
        }

        [Fact]
        public void Travel_stops_and_floors_count_the_served_floors_only()
        {
            var p = New();
            Floors(p, "B", 2800.0, "G", 3100.0, "1", 3060.0, "2", 3120.0, "3", null);
            Assert.Equal("12.08 METRES.", TitleBlockData.TravelText(p));
            Assert.Equal("5", TitleBlockData.StopsText(p));
            Assert.Equal("(B-G-1-2-3)", TitleBlockData.FloorsText(p));
            // The lowest floor is not served: travel from G to 3, four stops.
            p.Building.Floors[0].IsServed = false;
            Assert.Equal("9.28 METRES.", TitleBlockData.TravelText(p));
            Assert.Equal("4", TitleBlockData.StopsText(p));
            Assert.Equal("(G-1-2-3)", TitleBlockData.FloorsText(p));
            // An intermediate floor not served: travel unchanged, fewer stops.
            p.Building.Floors[2].IsServed = false;
            Assert.Equal("9.28 METRES.", TitleBlockData.TravelText(p));
            Assert.Equal("3", TitleBlockData.StopsText(p));
            Assert.Equal("(G-2-3)", TitleBlockData.FloorsText(p));
            // Default new project: 5 stops at 3000 mm.
            var n = New();
            Assert.Equal("12.00 METRES.", TitleBlockData.TravelText(n));
            Assert.Equal("5", TitleBlockData.StopsText(n));
            Assert.Equal("(G-1-2-3-4)", TitleBlockData.FloorsText(n));
        }

        [Fact]
        public void Incomplete_floors_leave_travel_empty()
        {
            var p = New();
            Floors(p, "G", null, "1", null);
            Assert.Equal("", TitleBlockData.TravelText(p));
            Assert.Equal("2", TitleBlockData.StopsText(p));
            Assert.Equal("(G-1)", TitleBlockData.FloorsText(p));
            Floors(p, "G", null);
            Assert.Equal("", TitleBlockData.TravelText(p));
            Assert.Equal("1", TitleBlockData.StopsText(p));
            p.Building.Floors[0].IsServed = false;
            Assert.Equal("", TitleBlockData.StopsText(p));
            Assert.Equal("", TitleBlockData.FloorsText(p));
            p.Building.Floors.Clear();
            Assert.Equal("", TitleBlockData.TravelText(p));
            Assert.Equal("", TitleBlockData.FloorsText(p));
            p.Building = null;
            Assert.Equal("", TitleBlockData.TravelText(p));
            Assert.Equal("", TitleBlockData.StopsText(p));
        }

        [Theory]
        [InlineData(5346.45, "P1 = 545 KG.")]   // 545 kg exactly stays
        [InlineData(12345.0, "P1 = 1260 KG.")]  // 1258.4 kg -> 1260
        [InlineData(11772.0, "P1 = 1200 KG.")]  // 1200 kg exactly stays
        [InlineData(11781.81, "P1 = 1205 KG.")] // 1201 kg rounds up to the next 5 kg
        [InlineData(0.0, "P1 = 0 KG.")]
        [InlineData(-11781.81, "P1 = -1205 KG.")]
        public void Loads_show_kg_only(double n, string text)
        {
            Assert.Equal(text, TitleBlockData.Load("P1", n));
            Assert.Equal("", TitleBlockData.Load("P1", null));
            Assert.Equal("", TitleBlockData.Load("P1", double.NaN));
        }

        [Fact]
        public void Loads_P1_to_P7_come_from_the_calculation_results()
        {
            var p = New();
            var chassis = new CalculationWorkspace().Calculate(p, Ctx);
            var pit = new SafetyPitCalculationEngine().Calculate(p);
            Assert.NotNull(chassis.Reactions);
            var t = TitleBlockData.Build(p, Lib, null, chassis.Reactions, pit, Day);
            var byId = chassis.Reactions.ToDictionary(r => r.Id, r => r.UpwardN);
            Assert.Equal("P1 = " + LoadUnits.KgText(byId["P1"]) + " KG.", t.P1);
            Assert.Equal("P2 = " + LoadUnits.KgText(byId["P2"]) + " KG.", t.P2);
            Assert.Equal("P3 = " + LoadUnits.KgText(byId["P3"]) + " KG.", t.P3);
            Assert.Equal("P4 = " + LoadUnits.KgText(byId["P4"]) + " KG.", t.P4);
            // P5: one car guide rail; P6: one car buffer; P7: one counterweight buffer.
            Assert.Equal("P5 = " + LoadUnits.KgText(pit.P5.PerComponentLoadN.Value) + " KG.", t.P5);
            Assert.Equal("P6 = " + LoadUnits.KgText(pit.P6.PerComponentLoadN.Value) + " KG.", t.P6);
            Assert.Equal("P7 = " + LoadUnits.KgText(pit.P7.PerComponentLoadN.Value) + " KG.", t.P7);
            int i = 1;
            foreach (var load in new[] { t.P1, t.P2, t.P3, t.P4, t.P5, t.P6, t.P7 })
                Assert.Matches(@"^P" + (i++) + @" = -?\d+ KG\.$", load);
            foreach (var r in chassis.Reactions)
                Assert.Equal(0.0, LoadUnits.Kg(r.UpwardN) % 5);
        }

        [Fact]
        public void P6_and_P7_are_the_load_of_one_buffer()
        {
            var p = New();
            ProjectPresets.Find("Calculations.SafetyPit.CarBufferCount").Edit(p, 2);
            ProjectPresets.Find("Calculations.SafetyPit.CounterweightBufferCount").Edit(p, 2);
            var pit = new SafetyPitCalculationEngine().Calculate(p);
            Assert.True(pit.P6.GroupLoadN > pit.P6.PerComponentLoadN);
            Assert.True(pit.P7.GroupLoadN > pit.P7.PerComponentLoadN);
            var t = TitleBlockData.Build(p, Lib, null, null, pit, null);
            Assert.Equal("P6 = " + LoadUnits.KgText(pit.P6.PerComponentLoadN.Value) + " KG.", t.P6);
            Assert.Equal("P7 = " + LoadUnits.KgText(pit.P7.PerComponentLoadN.Value) + " KG.", t.P7);
        }

        [Fact]
        public void Unavailable_or_outdated_results_leave_the_loads_empty()
        {
            var p = New();
            var pit = new SafetyPitCalculationEngine().Calculate(p);
            // A missing P7 input: P7 empty, P5 / P6 kept.
            pit.P7.PerComponentLoadN = null;
            var t = TitleBlockData.Build(p, Lib, null, new List<BearingReaction>(), pit, null);
            Assert.Equal("", t.P1);
            Assert.NotEqual("", t.P5);
            Assert.Equal("", t.P7);
            // A P5–P7 result of other inputs is not used.
            var stale = new SafetyPitCalculationEngine().Calculate(p);
            p.Calculations.CarMassKg = p.Calculations.CarMassKg + 100;
            t = TitleBlockData.Build(p, Lib, null, null, stale, null);
            Assert.Equal("", t.P5);
            Assert.Equal("", t.P6);
            Assert.Equal("", t.P7);
        }

        [Fact]
        public void The_live_overload_uses_current_results_only()
        {
            var flow = PresetFlow.Load(PresetsPath, Ctx);
            var live = new LiveCalculation(Ctx, flow);
            var p = New();
            Assert.Equal("", TitleBlockData.Build(p, Lib, null, live, null).P1);
            live.Recalculate(p);
            var t = TitleBlockData.Build(p, Lib, null, live, Day);
            Assert.NotEqual("", t.P1);
            Assert.NotEqual("", t.P4);
            Assert.NotEqual("", t.P5);
            Assert.NotEqual("", t.P7);
            // An input change makes both results outdated until the next recalculation.
            ProjectPresets.Lock(p, "Calculations.CarMassKg");
            p.Calculations.CarMassKg = p.Calculations.CarMassKg + 50;
            t = TitleBlockData.Build(p, Lib, null, live, Day);
            Assert.Equal("", t.P1);
            Assert.Equal("", t.P5);
            live.Recalculate(p);
            Assert.NotEqual("", TitleBlockData.Build(p, Lib, null, live, Day).P1);
        }

        [Fact]
        public void The_title_block_uses_the_edited_control_and_supply_and_the_entered_company()
        {
            var p = New();
            TitleBlockFields.Control.Edit(p, "  DUPLEX COLLECTIVE. ");
            var lib = Variant(s => s.Replace("3 PHASE 380 V. 50 Hz.", "3 PHASE 400 V. 50 Hz."));
            var t = TitleBlockData.Build(p, lib, new CompanySettings { CompanyName = " Nile Lifts " }, null, null, null);
            Assert.Equal("Nile Lifts", t.CompanyName);
            Assert.Equal("DUPLEX COLLECTIVE.", t.Control);
            Assert.Equal("3 PHASE 400 V. 50 Hz.", t.Supply);
        }

        // ---------- revision ----------
        [Theory]
        [InlineData(null, 0, "00", "00")]
        [InlineData("", 0, "00", "00")]
        [InlineData("  ", 0, "00", "00")]
        [InlineData("0", 0, "00", "00")]
        [InlineData("1", 1, "01", "01")]
        [InlineData(" 7 ", 7, "07", "07")]
        [InlineData("02", 2, "02", "02")]
        [InlineData("12", 12, "12", "12")]
        [InlineData("100", 100, "100", "100")]
        public void Revision_numbers_are_written_with_two_digits(string revision, int number, string text, string shown)
        {
            Assert.Equal(number, DrawingRevision.Number(revision));
            Assert.Equal(text, DrawingRevision.Text(number));
            Assert.Equal(shown, DrawingRevision.Display(revision));
            Assert.Null(DrawingRevision.Validate(revision));
            var p = New();
            p.Info.Name = "P";
            p.Report = new ProjectReportDetails { Revision = revision };
            // The documents and the title block show a blank revision (the first issue) as 00.
            Assert.Equal(shown.Length == 0 ? "00" : shown, TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            Assert.Equal(shown.Length == 0 ? "00" : shown, DocumentData.Resolve(p, null).Revision);
            Assert.EndsWith("-MODEL" + text, DrawingFileName.BaseName(p, null));
        }

        [Theory]
        [InlineData("A")]
        [InlineData("Rev 1")]
        [InlineData("-1")]
        [InlineData("1.5")]
        [InlineData("+2")]
        [InlineData("9999999999")]
        public void A_revision_that_is_not_a_whole_number_has_no_NN(string revision)
        {
            Assert.Null(DrawingRevision.Number(revision));
            var p = New();
            p.Info.Name = "VILLA";
            p.Report = new ProjectReportDetails { Revision = revision };
            // Shown as stored on every document, never changed.
            Assert.Equal(revision.Trim(), DrawingRevision.Display(revision));
            Assert.Equal(revision.Trim(), TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            var e = Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName(p, null));
            Assert.Equal("Revision must be a number: 00, 01, 02 …", e.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => DrawingRevision.Text(-1));
            // Reported by validation, never changed.
            var issue = DrawingRevision.Validate(p);
            Assert.NotNull(issue);
            Assert.Equal(DrawingRevision.NotANumberCode, issue.Code);
            Assert.Equal("Report.Revision", issue.Field);
            Assert.Equal(ValidationStatus.FAIL, issue.Status);
            Assert.Equal(revision, p.Report.Revision);
            var doc = DocumentData.Resolve(p, null);
            Assert.Null(doc.RevisionNumber);
            Assert.Equal(revision.Trim(), doc.Revision);
            Assert.NotNull(doc.RevisionIssue);
        }

        // ---------- file names ----------
        [Fact]
        public void The_design_flow_example()
        {
            Assert.Equal(
                "ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00",
                DrawingFileName.BaseName("ELEVATORSCOMPANY", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1600, "VILLA bx12", 0));
        }

        [Theory]
        [InlineData(CounterweightPositionCode.RCW, "RCW")]
        [InlineData(CounterweightPositionCode.SCWL, "SCWL")]
        [InlineData(CounterweightPositionCode.SCWR, "SCWR")]
        public void Each_counterweight_code(CounterweightPositionCode cw, string code)
        {
            Assert.Equal("C-GB-" + code + "-W1500D1800-P-MODEL03", DrawingFileName.BaseName("C", ElevatorSystemType.GB, cw, 1500, 1800, "P", 3));
        }

        [Theory]
        [InlineData(ElevatorSystemType.GB, "GB")]
        [InlineData(ElevatorSystemType.GL, "GL")]
        [InlineData(ElevatorSystemType.HY, "HY")]
        [InlineData(ElevatorSystemType.MRL, "MRL")]
        public void Each_system_type_code(ElevatorSystemType type, string code)
        {
            Assert.Equal("C-" + code + "-RCW-W1500D1800-P-MODEL00", DrawingFileName.BaseName("C", type, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
        }

        [Theory]
        [InlineData("VILLA bx12", "VILLA bx12")]
        [InlineData("  Villa  B ", "  Villa  B")]
        [InlineData("A\\B/C:D*E?F\"G<H>I|J", "A_B_C_D_E_F_G_H_I_J")]
        [InlineData("Tab\there\nnew\u0001\u007f", "Tab_here_new__")]
        [InlineData("Villa...", "Villa")]
        [InlineData("Villa. . ", "Villa")]
        [InlineData("V.I.L.L.A", "V.I.L.L.A")]
        [InlineData("فيلا 12", "فيلا 12")]
        [InlineData("a-b_c (d) [e] #1 & co, 50%", "a-b_c (d) [e] #1 & co, 50%")]
        public void Names_are_kept_as_typed_except_forbidden_characters(string typed, string clean)
        {
            Assert.Equal(clean, DrawingFileName.Clean(typed));
            Assert.Equal("ELEVATORS COMPANY-GB-RCW-W1500D1800-" + clean + "-MODEL00",
                DrawingFileName.BaseName("ELEVATORS COMPANY", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, typed, 0));
            Assert.Equal(clean + "-GB-RCW-W1500D1800-P-MODEL00",
                DrawingFileName.BaseName(typed, ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
        }

        [Fact]
        public void Revision_padding_and_shaft_size_integers()
        {
            Assert.EndsWith("-MODEL00", DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
            Assert.EndsWith("-MODEL09", DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 9));
            Assert.EndsWith("-MODEL10", DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 10));
            Assert.Contains("-W1501D1800-", DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500.5, 1799.6, "P", 0));
            Assert.Contains("-W1500D1800-", DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500.4, 1800.0, "P", 0));
        }

        [Fact]
        public void A_blank_company_is_left_out_a_blank_project_or_invalid_size_is_refused()
        {
            Assert.Equal("GB-RCW-W1500D1800-P-MODEL00", DrawingFileName.BaseName("  ", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
            Assert.Equal("GB-RCW-W1500D1800-P-MODEL00", DrawingFileName.BaseName(null, ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
            Assert.Equal("GB-RCW-W1500D1800-P-MODEL00", DrawingFileName.BaseName(" ..", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
            Assert.Equal("GB-RCW-W1500D1800-P-MODEL00", DrawingFileName.BaseName("", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, null, 0));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, " . ", 0));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 0, 1800, "P", 0));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, double.NaN, "P", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrawingFileName.BaseName("C", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", -1));
        }

        [Fact]
        public void One_base_name_for_the_dwg_the_pdf_and_the_project_file()
        {
            var f = DrawingFileName.Files("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00");
            Assert.Equal("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00", f.BaseName);
            Assert.Equal("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00.dwg", f.Dwg);
            Assert.Equal("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00.pdf", f.Pdf);
            Assert.Equal("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00.elevator.json", f.Project);
            Assert.Throws<ArgumentException>(() => DrawingFileName.Files(""));
        }

        [Fact]
        public void A_project_name_from_the_entered_company_shaft_and_revision()
        {
            var p = New();
            p.Info.Name = "VILLA bx12";
            p.Shaft.WidthMm = 1500;
            p.Shaft.DepthMm = 1600;
            Assert.Equal(ElevatorSystemType.GB, DrawingFileName.SystemTypeOf(p));
            Assert.Equal(CounterweightPositionCode.RCW, DrawingFileName.CounterweightOf(p));
            // No company name entered: the company segment is left out.
            Assert.Equal("GB-RCW-W1500D1600-VILLA bx12-MODEL00", DrawingFileName.BaseName(p, null));
            Assert.Equal("GB-RCW-W1500D1600-VILLA bx12-MODEL00", DrawingFileName.BaseName(p, new CompanySettings { CompanyName = "  " }));
            p.Report = new ProjectReportDetails { Revision = "2" };
            var company = new CompanySettings { CompanyName = "ELEVATORS COMPANY" };
            Assert.Equal("ELEVATORS COMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, company));
            // Kept exactly as typed.
            Assert.Equal(" Nile  Lifts-GB-RCW-W1500D1600-VILLA bx12-MODEL02",
                DrawingFileName.BaseName(p, new CompanySettings { CompanyName = " Nile  Lifts" }));
            // The enum is ready for the side-counterweight layout.
            Assert.Equal("ELEVATORS COMPANY-GB-SCWL-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, company, CounterweightPositionCode.SCWL));
            Assert.Equal("ELEVATORS COMPANY-GB-SCWR-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, company, CounterweightPositionCode.SCWR));
            // A system without a type code yet.
            p.Elevator.SystemId = "hydraulic";
            Assert.Null(DrawingFileName.SystemTypeOf(p));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName(p, company));
        }
    
        // ---------- one set of company / report data ----------
        [Fact]
        public void Every_current_project_maps_to_a_rear_counterweight()
        {
            Assert.Equal(CounterweightPositionCode.RCW, DrawingFileName.CounterweightOf(New()));
            var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            Assert.Equal(CounterweightPositionCode.RCW, DrawingFileName.CounterweightOf(p));
            p.Counterweight.Position = "side";
            Assert.Equal(CounterweightPositionCode.RCW, DrawingFileName.CounterweightOf(p)); // the plan geometry is always rear today
            Assert.Equal(new[] { "RCW", "SCWL", "SCWR" }, Enum.GetNames(typeof(CounterweightPositionCode)));
        }

        [Fact]
        public void The_company_name_is_only_what_the_user_entered()
        {
            Assert.Equal("", DocumentData.Company(null));
            Assert.Equal("", DocumentData.Company(new CompanySettings()));
            Assert.Equal("", DocumentData.Company(new CompanySettings { CompanyName = "   " }));
            Assert.Equal("Nile  Lifts Co. ", DocumentData.Company(new CompanySettings { CompanyName = "Nile  Lifts Co. " }));
            var t = TitleBlockData.Build(New(), Lib, null, null, null, null);
            Assert.Equal("", t.CompanyName);
        }

        [Fact]
        public void One_set_of_report_data_for_the_title_block_and_the_file_name()
        {
            var p = New();
            p.Info.Name = "VILLA bx12";
            p.Report = new ProjectReportDetails { ClientName = " Delta ", PreparedBy = "A. Engineer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver", Revision = "3" };
            var company = new CompanySettings { CompanyName = "Nile Lifts ", LogoFile = "company-logo-1234abcd.png" };
            var doc = DocumentData.Resolve(p, company);
            Assert.Equal("Nile Lifts ", doc.CompanyName);
            Assert.Equal("company-logo-1234abcd.png", doc.LogoFile);
            Assert.Equal("Delta", doc.Client);
            Assert.Equal("A. Engineer", doc.PreparedBy);
            Assert.Equal("B. Checker", doc.CheckedBy);
            Assert.Equal("C. Approver", doc.ApprovedBy);
            Assert.Equal(3, doc.RevisionNumber);
            Assert.Equal("03", doc.Revision);
            Assert.Null(doc.RevisionIssue);
            var t = TitleBlockData.Build(p, Lib, company, null, null, null);
            Assert.Equal("Nile Lifts", t.CompanyName);
            Assert.Equal(doc.Client, t.Client);
            Assert.Equal(doc.PreparedBy, t.PreparedBy);
            Assert.Equal(doc.CheckedBy, t.CheckedBy);
            Assert.Equal(doc.ApprovedBy, t.ApprovedBy);
            Assert.Equal(doc.Revision, t.Revision);
            Assert.EndsWith("-MODEL" + doc.Revision, DrawingFileName.BaseName(p, company));
            Assert.StartsWith("Nile Lifts-GB-", DrawingFileName.BaseName(p, company));
            // No project: company data only.
            var none = DocumentData.Resolve(null, null);
            Assert.Equal("", none.CompanyName);
            Assert.Equal("", none.Client);
            Assert.Equal("00", none.Revision);
            Assert.Equal(0, none.RevisionNumber);
        }

        [Fact]
        public void An_older_project_with_a_letter_revision_loads_and_is_reported()
        {
            var json = File.ReadAllText(Path.Combine(TestData.Root, "tests", "golden", "fixtures", "titleblock-filled.elevator.json"));
            var p = ProjectJson.Deserialize(json);
            Assert.Equal("A", p.Report.Revision);
            var issue = DrawingRevision.Validate(p);
            Assert.NotNull(issue);
            Assert.Equal("A", TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            Assert.Equal("A", DocumentData.Resolve(p, null).Revision);
            // Never silently changed: the revision text is saved back as it was.
            Assert.Equal("A", ProjectJson.Deserialize(ProjectJson.Serialize(p)).Report.Revision);
            Assert.Null(DrawingRevision.Validate(New()));
        }

        [Fact]
        public void Company_defaults_fill_the_names_a_project_leaves_blank()
        {
            var p = New();
            p.Report = new ProjectReportDetails { PreparedBy = "A. Engineer" };
            var company = new CompanySettings { PreparedBy = "Default P", CheckedBy = " Default C ", ApprovedBy = "Default A" };
            var doc = DocumentData.Resolve(p, company);
            Assert.Equal("A. Engineer", doc.PreparedBy); // the project's own value wins
            Assert.Equal("Default C", doc.CheckedBy);
            Assert.Equal("Default A", doc.ApprovedBy);
            var t = TitleBlockData.Build(p, Lib, company, null, null, null);
            Assert.Equal("Default C", t.CheckedBy);
            // No project report group at all: the defaults still apply; no defaults: blank.
            p.Report = null;
            Assert.Equal("Default P", DocumentData.Resolve(p, company).PreparedBy);
            Assert.Equal("", DocumentData.Resolve(p, new CompanySettings()).PreparedBy);
            Assert.Equal("", DocumentData.Resolve(p, null).ApprovedBy);
        }

        [Fact]
        public void The_phase_is_a_project_extension_and_adds_nothing_to_older_files()
        {
            var p = New();
            string before = ProjectJson.Serialize(p);
            Assert.Equal("", DocumentData.PhaseOf(p));
            DocumentData.SetPhase(p, "  Tender ");
            Assert.Equal("Tender", DocumentData.PhaseOf(p));
            Assert.Equal("Tender", DocumentData.Resolve(p, null).Phase);
            Assert.Equal("Tender", DocumentData.PhaseOf(ProjectJson.Deserialize(ProjectJson.Serialize(p))));
            DocumentData.SetPhase(p, " ");
            Assert.Equal("", DocumentData.PhaseOf(p));
            Assert.Equal(before, ProjectJson.Serialize(p)); // blank removes it: byte for byte as before
        }

        [Fact]
        public void The_settings_file_adapter_keeps_its_format_and_maps_to_the_portable_model()
        {
            string saved = CompanyReportSettings.SettingsDirectory;
            string dir = Path.Combine(TestData.Folder("layouts-company-settings"), Guid.NewGuid().ToString("N"));
            try
            {
                CompanyReportSettings.SettingsDirectory = dir;
                // No file: blank settings, so the documents follow the library company name.
                var empty = CompanyReportSettings.Load().ToCompanySettings();
                Assert.Null(empty.CompanyName);
                Assert.Equal("", DocumentData.Company(empty));
                // A settings file written by the current application still loads.
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "report-settings.json"), "{\"CompanyName\":\"Nile Lifts Engineering\",\"LogoFile\":null,\"LogoPath\":null}");
                var loaded = CompanyReportSettings.Load();
                Assert.Equal("Nile Lifts Engineering", loaded.ToCompanySettings().CompanyName);
                Assert.Equal("Nile Lifts Engineering", DocumentData.Company(loaded.ToCompanySettings()));
                // Apply + Save writes the file members: the company name, the logo and the optional defaults (older files have none).
                var s = new CompanyReportSettings();
                s.Apply(new CompanySettings { CompanyName = "Delta  Lifts" });
                s.Save();
                string text = File.ReadAllText(Path.Combine(dir, "report-settings.json"));
                var members = ((IDictionary<string, object>)Elevator.Core.Serialization.CoreJson.Create().DeserializeObject(text)).Keys.OrderBy(k => k, StringComparer.Ordinal);
                Assert.Equal(new[] { "ApprovedBy", "CheckedBy", "CompanyName", "LogoFile", "LogoPath", "PreparedBy" }, members);
                Assert.Equal("Delta  Lifts", CompanyReportSettings.Load().ToCompanySettings().CompanyName);
            }
            finally
            {
                CompanyReportSettings.SettingsDirectory = saved;
            }
        }

        // ---------- C2: the A0 sheet fields ----------
        [Fact]
        public void Type_of_work_phase_sheet_number_entrances_and_project_number()
        {
            var p = New();
            p.Report = new ProjectReportDetails { DocumentNumber = " NL-2026-014 " };
            Floors(p, "G", 3100.0, "1", 3060.0, "2", 3120.0, "3", null);
            var t = TitleBlockData.Build(p, Lib, null, null, null, Day);
            Assert.Equal("أعمال مصاعد", t.TypeOfWork);
            Assert.Equal("", t.Phase);
            Assert.Equal("1", t.SheetNo);
            Assert.Equal("AS SHOWN", t.Scale);
            Assert.Equal("4", t.Entrances);
            Assert.Equal("(G-1-2-3)", t.EntranceNames);
            Assert.Equal(t.Stops, t.Entrances);
            Assert.Equal("NL-2026-014", t.ProjectNumber);
            // an older library without the type of work takes the shipped default
            var old = Variant(x => x.Replace(", \"TypeOfWork\": \"أعمال مصاعد\"", ""));
            Assert.Null(old.TitleBlock.TypeOfWork);
            Assert.Equal("أعمال مصاعد", TitleBlockData.Build(p, old, null, null, null, null).TypeOfWork);
        }

        [Fact]
        public void Type_of_work_and_phase_lock_when_edited()
        {
            var p = New();
            var other = Variant(x => x.Replace("\"TypeOfWork\": \"أعمال مصاعد\"", "\"TypeOfWork\": \"أعمال تركيبات\""));
            Assert.Equal("أعمال تركيبات", TitleBlockFields.Effective(TitleBlockFields.TypeOfWork, p, other)); // follows the library
            TitleBlockFields.TypeOfWork.Edit(p, "أعمال صيانة");
            TitleBlockFields.Phase.Edit(p, "تنفيذ");
            Assert.Equal("أعمال صيانة", TitleBlockFields.Effective(TitleBlockFields.TypeOfWork, p, other));
            var back = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            var t = TitleBlockData.Build(back, Lib, null, null, null, null);
            Assert.Equal("أعمال صيانة", t.TypeOfWork);
            Assert.Equal("تنفيذ", t.Phase);
            Assert.Same(TitleBlockFields.Phase, ProjectPresets.Find("TitleBlock.Phase"));
            Assert.Equal(InputEffect.None, LiveCalculation.EffectOf("TitleBlock.TypeOfWork"));
            TitleBlockFields.TypeOfWork.Reset(back);
            TitleBlockFields.Phase.Reset(back);
            Assert.Equal("أعمال مصاعد", TitleBlockData.Build(back, Lib, null, null, null, null).TypeOfWork);
            Assert.Equal("", TitleBlockData.Build(back, Lib, null, null, null, null).Phase);
        }

        [Fact]
        public void Every_attribute_has_its_value_script_and_style()
        {
            var p = New();
            p.Info.Name = "فيلا التجمع";
            p.Report = new ProjectReportDetails { ClientName = "Mr. Client", Revision = "2", DocumentNumber = "P-14" };
            var live = new LiveCalculation(Ctx, PresetFlow.Load(PresetsPath, Ctx));
            live.Recalculate(p);
            var t = TitleBlockData.Build(p, Lib, new CompanySettings { CompanyName = "شركة النيل للمصاعد" }, live, Day);
            var a = t.Attributes();
            Assert.Equal(TitleBlockData.Tags, a.Select(x => x.Tag).ToArray());
            Func<string, TitleBlockAttribute> at = tag => a.Single(x => x.Tag == tag);
            // Arabic values: the xarab style and the encoded text; Latin values: the cent style and the text as it is.
            Assert.Equal(TextScript.Arabic, at("PROJECT").Script);
            Assert.Equal("SM_AR", at("PROJECT").Style);
            Assert.Equal(XarabText.Encode("فيلا التجمع"), at("PROJECT").DrawingText);
            Assert.Equal("فيلا التجمع", XarabText.Decode(at("PROJECT").DrawingText));
            Assert.Equal("SM_EN", at("CLIENT").Style);
            Assert.Equal("Mr. Client", at("CLIENT").DrawingText);
            Assert.Equal("SM_AR", at("TYPE_OF_WORK").Style);
            Assert.Equal("XulhG lwhu]", at("TYPE_OF_WORK").DrawingText);
            // the company goes to the line of its language
            Assert.Equal("شركة النيل للمصاعد", at("COMPANY_AR").Value);
            Assert.Equal("", at("COMPANY").Value);
            var latin = TitleBlockData.Build(p, Lib, new CompanySettings { CompanyName = "Nile Lifts" }, null, null, Day).Attributes();
            Assert.Equal("Nile Lifts", latin.Single(x => x.Tag == "COMPANY").DrawingText);
            Assert.Equal("", latin.Single(x => x.Tag == "COMPANY_AR").Value);
            // values
            Assert.Equal(t.P1, at("P1").Value);
            Assert.StartsWith("P7 = ", at("P7").Value);
            Assert.Equal("02", at("REV_ROW1_NO").Value);
            Assert.Equal("12/10/2026", at("REV_ROW1_DATE").Value);
            Assert.Equal("12/10/2026", at("DATE").Value);
            Assert.Equal("P-14", at("DOC_NO").Value);
            Assert.Equal("AS SHOWN", at("SCALE").Value);
            Assert.Equal("1", at("SHEET_NO").Value);
            Assert.Equal("", at("DRAWING_NAME").Value);
            Assert.Equal(t.Stops, at("ENTRANCES").Value);
            foreach (var x in a.Where(x => x.Value.Length == 0))
            {
                Assert.Equal("", x.DrawingText);
                Assert.Equal("SM_EN", x.Style);
            }
            // a blank revision is the first issue: the first row reads 00 and the date
            p.Report.Revision = null;
            var blank = TitleBlockData.Build(p, Lib, null, null, null, Day).Attributes();
            Assert.Equal("00", blank.Single(x => x.Tag == "REV_ROW1_NO").Value);
            Assert.Equal("12/10/2026", blank.Single(x => x.Tag == "REV_ROW1_DATE").Value);
            Assert.Equal("00", DocumentData.Resolve(p, null).Revision); // the calculation sheet and the Design Notes show 00 too (2026-10-12)
        }

        [Fact]
        public void The_template_block_has_exactly_these_attribute_tags()
        {
            // Templates/Layout_Geared_A0.dxf (tools/layouts/build_template.py): every ATTDEF of SM_TITLEBLOCK has a value here.
            var lines = File.ReadAllLines(Path.Combine(TestData.Root, "Templates", "Layout_Geared_A0.dxf"));
            var tags = new List<string>();
            var styles = new Dictionary<string, string>();
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1] != "ATTDEF")
                    continue;
                string tag = null, style = null;
                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    string code = lines[j].Trim();
                    if (code == "2")
                        tag = lines[j + 1];
                    if (code == "7")
                        style = lines[j + 1];
                }
                tags.Add(tag);
                styles[tag] = style;
            }
            Assert.Equal(TitleBlockData.Tags.OrderBy(x => x, StringComparer.Ordinal), tags.OrderBy(x => x, StringComparer.Ordinal));
            Assert.All(styles.Values, v => Assert.Contains(v, new[] { XarabText.ArabicStyle, XarabText.LatinStyle }));
            var text = File.ReadAllText(Path.Combine(TestData.Root, "Templates", "Layout_Geared_A0.dxf"));
            Assert.Contains("xarab.shx", text);
            Assert.Contains("cent.shx", text);
            Assert.DoesNotContain("PDFCreator", text);
            // the size without the carriage returns a Windows checkout adds (git autocrlf)
            Assert.True(File.ReadAllBytes(Path.Combine(TestData.Root, "Templates", "Layout_Geared_A0.dxf")).Count(x => x != 13) < 2000000);
        }
    }
}
