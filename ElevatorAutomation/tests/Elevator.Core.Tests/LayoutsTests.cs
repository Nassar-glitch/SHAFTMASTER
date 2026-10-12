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
        public void Shipped_library_holds_the_company_name_and_title_block_presets()
        {
            Assert.Equal("ELEVATORS COMPANY", Lib.CompanyName);
            Assert.Equal("ELEVATORS COMPANY", Lib.CompanyNameOrDefault());
            Assert.Equal("SIMPLEX COLLECTIVE DOWN.", Lib.TitleBlock.Control);
            Assert.Equal("3 PHASE 380 V. 50 Hz.", Lib.TitleBlock.Supply);
        }

        [Fact]
        public void An_older_library_without_the_new_members_takes_the_shipped_defaults()
        {
            var old = Variant(s => s.Replace("\"CompanyName\": \"ELEVATORS COMPANY\",", "")
                .Replace("\"TitleBlock\": { \"Control\": \"SIMPLEX COLLECTIVE DOWN.\", \"Supply\": \"3 PHASE 380 V. 50 Hz.\" },", ""));
            Assert.Null(old.CompanyName);
            Assert.Null(old.TitleBlock);
            Assert.Equal(PresetLibrary.DefaultCompanyName, old.CompanyNameOrDefault());
            Assert.Equal(TitleBlockPreset.DefaultControl, old.TitleBlockOrDefault().Control);
            Assert.Equal(TitleBlockPreset.DefaultSupply, old.TitleBlockOrDefault().Supply);
            // Not calculation inputs: the library fingerprint is unchanged, so no stored result goes stale.
            Assert.Equal(old.Hash(), Lib.Hash());
            Assert.Equal(Variant(s => s.Replace("ELEVATORS COMPANY", "NILE LIFTS").Replace("SIMPLEX COLLECTIVE DOWN.", "DUPLEX")).Hash(), Lib.Hash());
        }

        [Fact]
        public void The_company_name_is_editable_in_the_library_and_validated()
        {
            var edited = Variant(s => s.Replace("\"ELEVATORS COMPANY\"", "\"Nile Lifts  Co.\""));
            Assert.Equal("Nile Lifts  Co.", edited.CompanyNameOrDefault());
            var e = Assert.Throws<ArgumentException>(() => Variant(s => s.Replace("\"ELEVATORS COMPANY\"", "\"  \"")));
            Assert.Contains("CompanyName must not be blank", e.Message);
            e = Assert.Throws<ArgumentException>(() => Variant(s => s.Replace("\"Supply\": \"3 PHASE 380 V. 50 Hz.\"", "\"Supply\": null")));
            Assert.Contains("TitleBlock needs Control and Supply", e.Message);
        }

        [Fact]
        public void The_new_presets_round_trip_through_json()
        {
            var json = Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Serialize(Lib);
            var back = PresetLibrary.Parse(json);
            Assert.Equal(Lib.CompanyName, back.CompanyName);
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
            Assert.Equal("ELEVATORS COMPANY", t.CompanyName);
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
            Assert.Equal("", t.Scale);
            Assert.Equal("", t.ProjectName);
            Assert.Equal("", t.Client);
            Assert.Equal("", t.Date);
            Assert.Equal("", t.PreparedBy);
            Assert.Equal("", t.CheckedBy);
            Assert.Equal("", t.ApprovedBy);
            Assert.Equal("00", t.Revision); // blank revision: the first issue
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
        public void The_title_block_uses_the_edited_control_and_supply_and_the_library_company()
        {
            var p = New();
            TitleBlockFields.Control.Edit(p, "  DUPLEX COLLECTIVE. ");
            var lib = Variant(s => s.Replace("\"ELEVATORS COMPANY\"", "\"Nile Lifts\"").Replace("3 PHASE 380 V. 50 Hz.", "3 PHASE 400 V. 50 Hz."));
            var t = TitleBlockData.Build(p, lib, null, null, null, null);
            Assert.Equal("Nile Lifts", t.CompanyName);
            Assert.Equal("DUPLEX COLLECTIVE.", t.Control);
            Assert.Equal("3 PHASE 400 V. 50 Hz.", t.Supply);
        }

        // ---------- revision ----------
        [Theory]
        [InlineData(null, 0, "00")]
        [InlineData("", 0, "00")]
        [InlineData("  ", 0, "00")]
        [InlineData("0", 0, "00")]
        [InlineData("1", 1, "01")]
        [InlineData(" 7 ", 7, "07")]
        [InlineData("02", 2, "02")]
        [InlineData("12", 12, "12")]
        [InlineData("100", 100, "100")]
        public void Revision_numbers_are_written_with_two_digits(string revision, int number, string text)
        {
            Assert.Equal(number, DrawingRevision.Number(revision));
            Assert.Equal(text, DrawingRevision.Text(number));
            Assert.Null(DrawingRevision.Validate(revision));
            var p = New();
            p.Report = new ProjectReportDetails { Revision = revision };
            Assert.Equal(text, TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
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
            Assert.Equal("", TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            var e = Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName(p, Lib, null));
            Assert.Equal("Revision must be a number: 00, 01, 02 …", e.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => DrawingRevision.Text(-1));
            // Reported by validation, never changed.
            var issue = DrawingRevision.Validate(p);
            Assert.NotNull(issue);
            Assert.Equal(DrawingRevision.NotANumberCode, issue.Code);
            Assert.Equal("Report.Revision", issue.Field);
            Assert.Equal(ValidationStatus.FAIL, issue.Status);
            Assert.Equal(revision, p.Report.Revision);
            var doc = DocumentData.Resolve(p, Lib, null);
            Assert.Null(doc.RevisionNumber);
            Assert.Equal("", doc.Revision);
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
        public void Blank_names_or_invalid_sizes_are_refused()
        {
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName("  ", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1500, 1800, "P", 0));
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
        public void A_project_name_from_the_library_company_shaft_and_revision()
        {
            var p = New();
            p.Info.Name = "VILLA bx12";
            p.Shaft.WidthMm = 1500;
            p.Shaft.DepthMm = 1600;
            Assert.Equal(ElevatorSystemType.GB, DrawingFileName.SystemTypeOf(p));
            Assert.Equal(CounterweightPositionCode.RCW, DrawingFileName.CounterweightOf(p));
            Assert.Equal("ELEVATORS COMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00", DrawingFileName.BaseName(p, Lib, null));
            p.Report = new ProjectReportDetails { Revision = "2" };
            var lib = Variant(s => s.Replace("\"ELEVATORS COMPANY\"", "\"ELEVATORSCOMPANY\""));
            Assert.Equal("ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, lib, null));
            // The company entered in the settings wins over the library, kept exactly as typed.
            Assert.Equal(" Nile  Lifts-GB-RCW-W1500D1600-VILLA bx12-MODEL02",
                DrawingFileName.BaseName(p, lib, new CompanySettings { CompanyName = " Nile  Lifts" }));
            // The enum is ready for the side-counterweight layout.
            Assert.Equal("ELEVATORSCOMPANY-GB-SCWL-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, lib, null, CounterweightPositionCode.SCWL));
            Assert.Equal("ELEVATORSCOMPANY-GB-SCWR-W1500D1600-VILLA bx12-MODEL02", DrawingFileName.BaseName(p, lib, null, CounterweightPositionCode.SCWR));
            // A system without a type code yet.
            p.Elevator.SystemId = "hydraulic";
            Assert.Null(DrawingFileName.SystemTypeOf(p));
            Assert.Throws<ArgumentException>(() => DrawingFileName.BaseName(p, Lib, null));
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
        public void The_company_name_follows_the_library_until_entered_in_the_settings()
        {
            Assert.Equal("ELEVATORS COMPANY", DocumentData.Company(null, Lib));
            Assert.Equal("ELEVATORS COMPANY", DocumentData.Company(new CompanySettings(), Lib));
            Assert.Equal("ELEVATORS COMPANY", DocumentData.Company(new CompanySettings { CompanyName = "   " }, Lib));
            Assert.Equal("Nile  Lifts Co. ", DocumentData.Company(new CompanySettings { CompanyName = "Nile  Lifts Co. " }, Lib));
            Assert.Equal("", DocumentData.Company(null, null));
            // The library value follows an edit of the library.
            var edited = Variant(s => s.Replace("\"ELEVATORS COMPANY\"", "\"Delta Lifts\""));
            Assert.Equal("Delta Lifts", DocumentData.Company(null, edited));
            Assert.Equal("Nile", DocumentData.Company(new CompanySettings { CompanyName = "Nile" }, edited));
        }

        [Fact]
        public void One_set_of_report_data_for_the_title_block_and_the_file_name()
        {
            var p = New();
            p.Info.Name = "VILLA bx12";
            p.Report = new ProjectReportDetails { ClientName = " Delta ", PreparedBy = "A. Engineer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver", Revision = "3" };
            var company = new CompanySettings { CompanyName = "Nile Lifts ", LogoFile = "company-logo-1234abcd.png" };
            var doc = DocumentData.Resolve(p, Lib, company);
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
            Assert.EndsWith("-MODEL" + doc.Revision, DrawingFileName.BaseName(p, Lib, company));
            Assert.StartsWith("Nile Lifts-GB-", DrawingFileName.BaseName(p, Lib, company));
            // No project: company data only.
            var none = DocumentData.Resolve(null, Lib, null);
            Assert.Equal("ELEVATORS COMPANY", none.CompanyName);
            Assert.Equal("", none.Client);
            Assert.Equal("00", none.Revision);
        }

        [Fact]
        public void An_older_project_with_a_letter_revision_loads_and_is_reported()
        {
            var json = File.ReadAllText(Path.Combine(TestData.Root, "tests", "golden", "fixtures", "titleblock-filled.elevator.json"));
            var p = ProjectJson.Deserialize(json);
            Assert.Equal("A", p.Report.Revision);
            var issue = DrawingRevision.Validate(p);
            Assert.NotNull(issue);
            Assert.Equal("", TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            // Never silently changed: the revision text is saved back as it was.
            Assert.Equal("A", ProjectJson.Deserialize(ProjectJson.Serialize(p)).Report.Revision);
            Assert.Null(DrawingRevision.Validate(New()));
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
                Assert.Equal("ELEVATORS COMPANY", DocumentData.Company(empty, Lib));
                // A settings file written by the current application still loads.
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "report-settings.json"), "{\"CompanyName\":\"Nile Lifts Engineering\",\"LogoFile\":null,\"LogoPath\":null}");
                var loaded = CompanyReportSettings.Load();
                Assert.Equal("Nile Lifts Engineering", loaded.ToCompanySettings().CompanyName);
                Assert.Equal("Nile Lifts Engineering", DocumentData.Company(loaded.ToCompanySettings(), Lib));
                // Apply + Save writes the same members as before.
                var s = new CompanyReportSettings();
                s.Apply(new CompanySettings { CompanyName = "Delta  Lifts" });
                s.Save();
                string text = File.ReadAllText(Path.Combine(dir, "report-settings.json"));
                var members = ((IDictionary<string, object>)Elevator.Core.Serialization.CoreJson.Create().DeserializeObject(text)).Keys.OrderBy(k => k, StringComparer.Ordinal);
                Assert.Equal(new[] { "CompanyName", "LogoFile", "LogoPath" }, members);
                Assert.Equal("Delta  Lifts", CompanyReportSettings.Load().ToCompanySettings().CompanyName);
            }
            finally
            {
                CompanyReportSettings.SettingsDirectory = saved;
            }
        }
    }
}
