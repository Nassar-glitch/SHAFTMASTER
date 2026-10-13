using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
using Elevator.Core.Steel;
using Xunit;

namespace Elevator.Core.Tests
{
    // The portable steel-shaft model (DESIGN-FLOW "Steel shaft structure"): catalogues, the beam-level rule, the derived rail bracket
    // spacing, the geometry as data, the member list, and lock and follow. A concrete project must not change at all.
    public class SteelShaftTests
    {
        static readonly Lazy<PresetLibrary> Lib = new Lazy<PresetLibrary>(() => PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")));
        static PresetLibrary Library { get { return Lib.Value; } }
        static SteelShaftPreset Preset { get { return Library.SteelShaftOrDefault(); } }

        static ElevatorProject NewProject()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(Library, TestData.Context(), seed.Counterweight, seed.Rails.BracketSource);
        }

        // A project of two or more floors with the given heights (the last is not used), steel by default.
        static ElevatorProject SteelProject(params double[] heights)
        {
            var p = NewProject();
            if (heights.Length > 0)
            {
                p.Building.Floors.Clear();
                for (int i = 0; i <= heights.Length; i++)
                    p.Building.Floors.Add(new FloorDefinition { Name = i == 0 ? "G" : i.ToString(), HeightToNextMm = i < heights.Length ? (double?)heights[i] : null });
                p.Car.DisplayFloorId = p.Building.Floors[0].FloorId;
            }
            SteelFields.Construction.Edit(p, SteelShaftPreset.SteelStructure);
            ProjectPresets.Apply(p, Library, TestData.Context());
            return p;
        }

        static SteelStructure Build(ElevatorProject p)
        {
            var s = SteelStructure.Build(p, Library);
            Assert.True(s.Valid, string.Join("; ", s.Inputs.Problems));
            return s;
        }

        static double[] Between(IEnumerable<SteelLevel> levels, double from, double to)
        {
            return levels.Where(l => l.ElevationMm >= from - 1e-9 && l.ElevationMm <= to + 1e-9).Select(l => l.ElevationMm).ToArray();
        }

        // ---------- catalogue ----------
        [Fact]
        public void Catalogue_holds_UPN_80_to_200_and_the_equal_angles_with_the_producer_values()
        {
            Assert.Equal(new[] { "UPN 80", "UPN 100", "UPN 120", "UPN 140", "UPN 160", "UPN 180", "UPN 200" }, Preset.Channels.Select(c => c.Name).ToArray());
            var upn140 = Preset.Channel("UPN 140");
            Assert.Equal(140, upn140.HeightMm); Assert.Equal(60, upn140.WidthMm); Assert.Equal(7, upn140.WebMm); Assert.Equal(10, upn140.FlangeMm);
            Assert.Equal(10, upn140.RootRadiusMm); Assert.Equal(20.4, upn140.AreaCm2); Assert.Equal(16.0, upn140.MassKgPerM);
            Assert.Equal(605, upn140.IxCm4); Assert.Equal(86.4, upn140.WxCm3); Assert.Equal(62.7, upn140.IyCm4); Assert.Equal(14.8, upn140.WyCm3);
            Assert.Equal(17.5, upn140.EccentricityMm);
            Assert.Equal(10.6, Preset.Channel("UPN 100").MassKgPerM);
            Assert.Equal(25.3, Preset.Channel("UPN 200").MassKgPerM);
            Assert.Equal(8.64, Preset.Channel("UPN 80").MassKgPerM);
            var l50 = Preset.Angle("L50x50x5");
            Assert.Equal(4.80, l50.AreaCm2); Assert.Equal(3.77, l50.MassKgPerM); Assert.Equal(11.0, l50.IxCm4); Assert.Equal(14.0, l50.EccentricityMm);
            Assert.Equal(2.42, Preset.Angle("L40x40x4").MassKgPerM);
            Assert.Equal(5.42, Preset.Angle("L60x60x6").MassKgPerM);
        }

        [Fact]
        public void Catalogue_rows_are_consistent_mass_follows_area_and_wx_follows_ix()
        {
            foreach (var c in Preset.Channels)
            {
                Assert.InRange(c.MassKgPerM / (c.AreaCm2 * 0.785), 0.99, 1.01);                 // 7850 kg/m³
                Assert.InRange(c.WxCm3 / (c.IxCm4 / (c.HeightMm / 20.0)), 0.995, 1.005);       // Wx = Ix / (h/2)
                Assert.InRange(c.WyCm3 / (c.IyCm4 / (c.WidthMm - c.EccentricityMm) * 10), 0.98, 1.02);   // Wy = Iy / (b - e)
                Assert.Empty(c.Validate());
            }
            foreach (var a in Preset.Angles)
            {
                Assert.InRange(a.MassKgPerM / (a.AreaCm2 * 0.785), 0.99, 1.01);
                Assert.Empty(a.Validate());
            }
            Assert.True(Preset.Channels.Zip(Preset.Channels.Skip(1), (x, y) => y.HeightMm > x.HeightMm && y.MassKgPerM > x.MassKgPerM).All(ok => ok));
        }

        [Fact]
        public void Shipped_defaults_equal_the_file_and_the_group_is_outside_the_calculation_fingerprint()
        {
            var json = CoreJson.CreateWithDefaultLimit();
            Assert.Equal(json.Serialize(Library.SteelShaft), json.Serialize(SteelShaftDefaults.Create()));
            Assert.Empty(Library.Validate());
            string before = Library.Hash();
            var copy = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            copy.SteelShaft.BeamSpacingMm = 1234;
            copy.SteelShaft.Channels[0].MassKgPerM = 99;
            Assert.Equal(before, copy.Hash());
            // an older library without the group falls back to the shipped defaults
            copy.SteelShaft = null;
            Assert.Equal(1500, copy.SteelShaftOrDefault().BeamSpacingMm);
        }

        [Fact]
        public void A_bad_catalogue_or_default_is_rejected()
        {
            var copy = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            copy.SteelShaft.ColumnProfile = "UPN 999";
            Assert.Contains(copy.Validate(), m => m.Contains("ColumnProfile"));
            copy = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            copy.SteelShaft.Channels[1].MassKgPerM = 0;
            Assert.Contains(copy.Validate(), m => m.Contains("UPN 100"));
            copy = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            copy.SteelShaft.BoltCount = 5;
            Assert.Contains(copy.Validate(), m => m.Contains("BoltCount"));
        }

        // ---------- the beam-level rule ----------
        [Fact]
        public void Rule_examples_of_the_design_flow()
        {
            Assert.Equal(new[] { 0.0, 2160, 3000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 7200), 0, 3000));
            Assert.Equal(new[] { 0.0, 2160, 3660, 5160, 6000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 1500, -1000, 10000), 0, 6000));
            Assert.Equal(new[] { 0.0, 2160, 3250 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3250 }, 2160, 1500, -1000, 7200), 0, 3250));
        }

        [Fact]
        public void The_rest_equal_to_s_goes_to_the_floor_beam_and_a_little_more_adds_a_beam()
        {
            Assert.Equal(new[] { 0.0, 2160, 3660 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3660 }, 2160, 1500, -1000, 8000), 0, 3660));
            Assert.Equal(new[] { 0.0, 2160, 3660, 3670 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3670 }, 2160, 1500, -1000, 8000), 0, 3670));
        }

        [Fact]
        public void Levels_are_tagged_and_ordered_floors_door_heads_intermediates_pit_and_top()
        {
            var levels = SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 1500, -3100, 6000 + 4200);
            Assert.Equal(levels.Select(l => l.ElevationMm).OrderBy(x => x).ToArray(), levels.Select(l => l.ElevationMm).ToArray());
            var tags = levels.ToDictionary(l => l.Key, l => l.Tag);
            Assert.Equal("floor", tags["F:0"]);
            Assert.Equal("floor", tags["F:1"]);
            Assert.Equal("door head", tags["D:0"]);
            Assert.Equal("door head", tags["D:1"]);
            Assert.Equal("intermediate", tags["I:0:1"]);
            Assert.Equal("intermediate", tags["I:0:2"]);
            Assert.Equal("pit", tags["P:1"]);
            Assert.Equal("top", tags["T:top"]);
            Assert.Equal(10200, levels.Last().ElevationMm);
        }

        [Fact]
        public void Pit_steps_up_from_the_base_in_steps_of_s()
        {
            // pit 1000: no beam; 1700: base+1500; 3100: base+1500, base+3000 is within 1500 of... rest 100 -> floor
            Assert.Empty(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 7200).Where(l => l.Kind == SteelLevelKind.Pit));
            var pit = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1700, 7200).Where(l => l.Kind == SteelLevelKind.Pit).ToList();
            Assert.Equal(new[] { -200.0 }, pit.Select(l => l.ElevationMm).ToArray());
            var deep = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -3100, 7200).Where(l => l.Kind == SteelLevelKind.Pit).ToList();
            Assert.Equal(new[] { -1600.0, -100.0 }, deep.Select(l => l.ElevationMm).ToArray());
        }

        [Fact]
        public void Top_steps_up_from_the_top_door_head_to_the_tower_top_which_carries_the_last_beam()
        {
            var levels = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000 + 4200);
            Assert.Equal(new[] { 3000.0, 5160, 6660, 7200 }, Between(levels, 3000, 99999));
            Assert.Equal(new[] { SteelLevelKind.Floor, SteelLevelKind.DoorHead, SteelLevelKind.Top, SteelLevelKind.Top },
                levels.Where(l => l.ElevationMm >= 3000).Select(l => l.Kind).ToArray());
            // a low overhead: the tower top is the first beam above the door head
            var low = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000 + 3000);
            Assert.Equal(new[] { 3000.0, 5160, 6000 }, Between(low, 3000, 99999));
        }

        [Fact]
        public void Tall_floors_get_more_beams_and_no_gap_above_a_door_head_exceeds_s()
        {
            for (double h = 2200; h <= 12000; h += 37)
            {
                var levels = SteelBeamLevels.Rule(new[] { 0.0, h, 2 * h }, 2160, 1500, -1100, 2 * h + 4200);
                for (int i = 1; i < levels.Count; i++)
                {
                    double gap = levels[i].ElevationMm - levels[i - 1].ElevationMm;
                    Assert.True(gap > 0);
                    if (levels[i - 1].Kind != SteelLevelKind.Floor)
                        Assert.True(gap <= 1500 + 1e-6, "gap " + gap + " above " + levels[i - 1].Key + " at floor height " + h);
                }
                Assert.True(levels.Any(l => l.Key == "F:0") && levels.Any(l => l.Key == "F:1") && levels.Any(l => l.Key == "F:2"));
                Assert.Equal(3, levels.Count(l => l.Kind == SteelLevelKind.DoorHead));
            }
        }

        [Fact]
        public void The_rule_rejects_what_it_cannot_place_and_reports_what_the_doors_cannot_meet()
        {
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 0, -1000, 7200));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 0, 1500, -1000, 7200));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, 0, 7200));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000, 3000 }, 2160, 1500, -1000, 7200));
            // a floor not above the door height: no door-head beam there, and a note
            var low = SteelBeamLevels.Rule(new[] { 0.0, 2000, 5000 }, 2160, 1500, -1000, 9000);
            Assert.DoesNotContain(low, l => l.Key == "D:0");
            var floors = new[] { 0.0, 2000, 5000 }.Select((e, i) => new FloorElevation { Name = i.ToString(), ElevationMm = e, IsServed = true }).ToList();
            var issues = SteelBeamLevels.Issues(floors, 2160, 1500, -1000, 9000);
            Assert.Contains(issues, m => m.Contains("cannot be split"));
            Assert.Contains(issues, m => m.StartsWith("At floor 0"));
            // door height within s: nothing to report
            Assert.Empty(SteelBeamLevels.Issues(floors.Skip(1).ToList(), 2160, 2200, -1000, 9000));
        }

        [Fact]
        public void Edited_levels_lock_and_Reset_restores_the_rule()
        {
            var p = SteelProject(6000);
            var rule = Build(p).RuleLevels;
            Assert.Equal(new[] { 0.0, 2160, 3660, 5160, 6000 }, Between(rule, 0, 6000));
            SteelFields.EditLevel(p, "I:G:1", 3700);
            SteelFields.RemoveLevel(p, "I:G:2");
            SteelFields.EditLevel(p, "F:G", 123);      // a floor level is fixed by the building: ignored
            SteelFields.EditLevel(p, "C:1", 4400);     // an added level
            var s = Build(p);
            Assert.Equal(new[] { 0.0, 2160, 3700, 4400, 6000 }, Between(s.Levels, 0, 6000));
            Assert.True(s.Levels.Single(l => l.Key == "I:G:1").Edited);
            Assert.True(s.Levels.Single(l => l.Key == "C:1").Edited);
            Assert.False(s.Levels.Single(l => l.Key == "D:G").Edited);
            Assert.Equal(rule.Select(l => l.ElevationMm), s.RuleLevels.Select(l => l.ElevationMm));   // the rule itself is untouched
            // the edits survive the project file
            var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(Between(s.Levels, 0, 6000), Between(Build(again).Levels, 0, 6000));
            Assert.Equal(4, SteelFields.LevelEdits(again).Count);
            // one level back to the rule, then all
            SteelFields.ResetLevel(p, "I:G:1");
            Assert.Equal(new[] { 0.0, 2160, 3660, 4400, 6000 }, Between(Build(p).Levels, 0, 6000));
            SteelFields.ResetLevels(p);
            Assert.False(SteelFields.LevelsEdited(p));
            Assert.Equal(Between(rule, 0, 6000), Between(Build(p).Levels, 0, 6000));
            Assert.False(p.Extensions.ContainsKey("steel") && ((IDictionary<string, object>)p.Extensions["steel"]).ContainsKey("levelEdits"));
        }

        [Fact]
        public void An_edit_of_a_level_the_rule_no_longer_has_is_ignored_and_follows_back_when_it_returns()
        {
            var p = SteelProject(6000);
            SteelFields.EditLevel(p, "I:G:2", 4000);
            Assert.Contains(Build(p).Levels, l => l.Key == "I:G:2" && l.ElevationMm == 4000);
            // a shorter first floor has no second intermediate beam any more
            p.Building.Floors[0].HeightToNextMm = 3500;
            var s = Build(p);
            Assert.DoesNotContain(s.Levels, l => l.Key == "I:G:2");
            Assert.Equal(new[] { 0.0, 2160, 3500 }, Between(s.Levels, 0, 3500));
        }

        // ---------- the project: construction, lock and follow ----------
        [Fact]
        public void A_new_project_is_concrete_and_its_file_has_no_steel_content()
        {
            var p = NewProject();
            Assert.False(SteelFields.IsSteel(p, Library));
            Assert.Equal("Concrete", SteelFields.Construction.Effective(new PresetContext { Project = p, Library = Library }));
            Assert.False(ProjectJson.Serialize(p).ToLowerInvariant().Contains("steel"));
            Assert.Null(p.Extensions == null ? null : p.Extensions.Keys.FirstOrDefault(k => k == "steel"));
            Assert.Null(p.UserEditedFields);
            // applying the presets again does not create any steel content
            ProjectPresets.Apply(p, Library, TestData.Context());
            Assert.False(ProjectJson.Serialize(p).ToLowerInvariant().Contains("steel"));
        }

        [Fact]
        public void Steel_inputs_start_from_the_presets_and_follow_the_library()
        {
            var p = SteelProject();
            var c = new PresetContext { Project = p, Library = Library };
            Assert.True(SteelFields.IsSteel(p, Library));
            Assert.Equal("UPN", SteelFields.System.Effective(c));
            Assert.Equal("UPN 140", SteelFields.ColumnProfile.Effective(c));
            Assert.Equal("UPN 100", SteelFields.BeamProfile.Effective(c));
            Assert.Equal("L50x50x5", SteelFields.BracingProfile.Effective(c));
            Assert.Equal("DoubleCross", SteelFields.Bracing.Effective(c));
            Assert.Equal(1500.0, SteelFields.BeamSpacing.Effective(c));
            Assert.Equal(200.0, SteelFields.PlateWidth.Effective(c)); Assert.Equal(200.0, SteelFields.PlateDepth.Effective(c)); Assert.Equal(15.0, SteelFields.PlateThickness.Effective(c));
            Assert.Equal(4.0, SteelFields.BoltCount.Effective(c)); Assert.Equal(20.0, SteelFields.BoltDiameter.Effective(c));
            Assert.Equal(400.0, SteelFields.GussetLength.Effective(c)); Assert.Equal(250.0, SteelFields.GussetWidth.Effective(c)); Assert.Equal(6.0, SteelFields.GussetThickness.Effective(c));
            Assert.Equal("St 37", SteelFields.SteelGrade.Effective(c));
            Assert.Equal(200.0, SteelFields.BaseMargin.Effective(c)); Assert.Equal(250.0, SteelFields.BaseThickness.Effective(c)); Assert.Equal(8.0, SteelFields.BarDiameter.Effective(c));
            Assert.False(SteelFields.All.Any(f => f.IsEdited(p) && f != SteelFields.Construction));
            // a unlocked value follows the library
            var other = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            other.SteelShaft.BeamSpacingMm = 1200;
            other.SteelShaft.ColumnProfile = "UPN 160";
            var c2 = new PresetContext { Project = p, Library = other };
            Assert.Equal(1200.0, SteelFields.BeamSpacing.Effective(c2));
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c2));
        }

        [Fact]
        public void An_edited_value_locks_follows_nothing_and_Reset_returns_to_the_preset()
        {
            var p = SteelProject();
            SteelFields.BeamSpacing.Edit(p, 1800.0);
            SteelFields.ColumnProfile.Edit(p, "UPN 160");
            Assert.True(SteelFields.BeamSpacing.IsEdited(p));
            var other = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            other.SteelShaft.BeamSpacingMm = 1200;
            var c = new PresetContext { Project = p, Library = other };
            Assert.Equal(1800.0, SteelFields.BeamSpacing.Effective(c));       // locked
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c));
            Assert.Equal("UPN 100", SteelFields.BeamProfile.Effective(c));    // not edited: still the preset
            var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(1800.0, SteelFields.BeamSpacing.Effective(new PresetContext { Project = again, Library = other }));
            Assert.True(SteelFields.BeamSpacing.IsEdited(again));
            SteelFields.BeamSpacing.Reset(p);
            Assert.False(SteelFields.BeamSpacing.IsEdited(p));
            Assert.Equal(1200.0, SteelFields.BeamSpacing.Effective(new PresetContext { Project = p, Library = other }));
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c));
            SteelFields.ColumnProfile.Reset(p);
            SteelFields.Construction.Reset(p);
            Assert.False(SteelFields.IsSteel(p, Library));
            Assert.False(ProjectJson.Serialize(p).ToLowerInvariant().Contains("steel"));
            Assert.Same(SteelFields.BeamSpacing, ProjectPresets.Find("Steel.BeamSpacingMm"));
        }

        [Fact]
        public void The_base_bar_counts_follow_the_shaft_size_until_typed()
        {
            var p = SteelProject();
            var c = new PresetContext { Project = p, Library = Library };
            double nx = Convert.ToDouble(SteelFields.BarCountAlongX.Effective(c)), ny = Convert.ToDouble(SteelFields.BarCountAlongY.Effective(c));
            p.Shaft.WidthMm += 600; p.Shaft.DepthMm += 300;
            double nx2 = Convert.ToDouble(SteelFields.BarCountAlongX.Effective(c)), ny2 = Convert.ToDouble(SteelFields.BarCountAlongY.Effective(c));
            Assert.True(ny2 > ny, "bars along the depth are spread across the width");
            Assert.True(nx2 > nx);
            SteelFields.BarCountAlongX.Edit(p, 7.0);
            p.Shaft.WidthMm += 600;
            Assert.Equal(7.0, SteelFields.BarCountAlongX.Effective(c));
            var s = Build(p);
            Assert.Equal(7, s.Base.BarsAlongX.Count);
            Assert.Equal((int)Convert.ToDouble(SteelFields.BarCountAlongY.Effective(c)), s.Base.BarsAlongY.Count);
        }

        // ---------- derived rail bracket spacing ----------
        [Fact]
        public void The_bracket_spacing_of_a_steel_shaft_is_the_largest_gap_between_beam_levels_in_the_travel_zone()
        {
            Assert.Equal(2160.0, Build(SteelProject(3000)).DerivedBracketSpacingMm);               // 0, 2160, 3000 -> 2160 (the door)
            Assert.Equal(2160.0, Build(SteelProject(6000)).DerivedBracketSpacingMm);               // 2160, 1500, 1500, 840
            var p = SteelProject(6000);
            SteelFields.BeamSpacing.Edit(p, 3000.0);
            var s = Build(p);
            Assert.Equal(new[] { 0.0, 2160, 5160, 6000 }, Between(s.Levels, 0, 6000));
            Assert.Equal(3000.0, s.DerivedBracketSpacingMm);
            // an edited level changes the gap
            var q = SteelProject(6000);
            SteelFields.RemoveLevel(q, "I:G:1");
            Assert.Equal(3000.0, Build(q).DerivedBracketSpacingMm);   // 3660 removed: 2160 -> 5160 is now a 3000 gap
        }

        [Fact]
        public void The_rails_follow_the_derived_spacing_and_the_guide_rail_check_uses_it()
        {
            var ctx = TestData.Context();
            var p = SteelProject(3200, 3200);
            var s = Build(p);
            double derived = s.DerivedBracketSpacingMm.Value;
            Assert.Equal(derived, p.Rails.CarSpacingMm);                      // the preset: derived, no recommendation
            Assert.Equal(derived, p.Rails.CwSpacingMm);
            Assert.Equal(derived, p.Rails.BracketMaximumSpacingMm);
            var check = GuideRailCheckEngine.Calculate(p, ctx, Library);
            Assert.True(check.Available);
            Assert.Equal(derived, check.Car.SpacingMm);
            Assert.Equal(derived, check.Counterweight.SpacingMm);
            // a typed spacing on a steel project does not override the beams
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, 1000.0);
            Assert.Equal(derived, GuideRailCheckEngine.Calculate(p, ctx, Library).Car.SpacingMm);
            // the same project as concrete: the recommendation, unchanged
            var concrete = NewProject();
            Assert.Equal(2000.0, concrete.Rails.CarSpacingMm);
        }

        [Fact]
        public void A_concrete_project_is_byte_identical_with_the_steel_code_present()
        {
            var a = NewProject();
            ProjectPresets.Apply(a, Library, TestData.Context());
            var json = ProjectJson.Serialize(a);
            ProjectPresets.Apply(a, Library, TestData.Context());
            Assert.Equal(json, ProjectJson.Serialize(a));
            Assert.Equal(json, ProjectJson.Serialize(ProjectJson.Deserialize(json)));
            Assert.Null(SteelStructure.DerivedSpacing(new PresetContext { Project = a, Library = Library }));
        }

        // ---------- geometry ----------
        [Fact]
        public void Plan_has_four_corner_columns_outside_the_clear_rectangle_web_outside_and_channels_facing_each_other()
        {
            var p = SteelProject(3000, 3000);
            var s = Build(p);
            var plan = s.Plan;
            Assert.Equal(p.Shaft.WidthMm, plan.Clear.Width);
            Assert.Equal(p.Shaft.DepthMm, plan.Clear.Height);
            Assert.Equal(4, plan.Columns.Count);
            Assert.Equal(new[] { "BackLeft", "BackRight", "FrontLeft", "FrontRight" }, plan.Columns.Select(c => c.Corner).OrderBy(x => x).ToArray());
            foreach (var c in plan.Columns)
            {
                Assert.False(c.Box.Overlaps(plan.Clear), c.Corner + " steel must lie outside the clear rectangle");
                Assert.Equal(60, c.Box.Width);       // UPN 140 flange width into the shaft
                Assert.Equal(140, c.Box.Height);     // UPN 140 depth along the face
                Assert.Equal("UPN 140", c.Profile);
                Assert.Equal(8, c.Outline.Count);
                Assert.All(c.Outline, pt => { Assert.InRange(pt.X, c.Box.X0 - 1e-9, c.Box.X1 + 1e-9); Assert.InRange(pt.Y, c.Box.Y0 - 1e-9, c.Box.Y1 + 1e-9); });
                Assert.Equal(200, c.Plate.Width); Assert.Equal(200, c.Plate.Height);
                Assert.Equal(4, c.Bolts.Count);
                Assert.All(c.Bolts, b => { Assert.Equal(20, b.DiameterMm); Assert.True(b.X >= c.Plate.X0 && b.X <= c.Plate.X1 && b.Y >= c.Plate.Y0 && b.Y <= c.Plate.Y1); });
                // the bolt pattern is a 120 square about the plate centre
                Assert.Equal(120, c.Bolts.Max(b => b.X) - c.Bolts.Min(b => b.X), 6);
                Assert.Equal(120, c.Bolts.Max(b => b.Y) - c.Bolts.Min(b => b.Y), 6);
            }
            var left = plan.Columns.Where(c => c.WebSide == "Left").ToList();
            var right = plan.Columns.Where(c => c.WebSide == "Right").ToList();
            Assert.Equal(2, left.Count); Assert.Equal(2, right.Count);
            // the web is on the outside: the outline reaches the outer edge along the whole depth, the flange tips are on the clear line
            foreach (var c in left)
            {
                Assert.Equal(-60, c.Outline.Min(pt => pt.X)); Assert.Equal(0, c.Outline.Max(pt => pt.X));
                Assert.Equal(c.Box.Height, c.Outline.Where(pt => pt.X == -60).Max(pt => pt.Y) - c.Outline.Where(pt => pt.X == -60).Min(pt => pt.Y));
            }
            foreach (var c in right)
            {
                Assert.Equal(plan.Clear.X1, c.Outline.Min(pt => pt.X)); Assert.Equal(plan.Clear.X1 + 60, c.Outline.Max(pt => pt.X));
            }
            // a face's columns have the clear width between their flange tips
            Assert.Equal(plan.Clear.Width, right.Min(c => c.Box.X0) - left.Max(c => c.Box.X1));
            // plates are centred on the column; the footprint is the bounding box of the four plates
            Assert.Equal(plan.Columns.Min(c => c.Plate.X0), plan.PlateFootprint.X0);
            Assert.Equal(plan.Columns.Max(c => c.Plate.Y1), plan.PlateFootprint.Y1);
            Assert.Equal(70, plan.PlateIntrusionMm, 6);   // (200 - 60) / 2 of the plate reaches inside along the width
        }

        [Fact]
        public void Plan_beam_ring_has_four_beams_on_the_faces_between_the_columns_and_outside_the_clear_rectangle()
        {
            var s = Build(SteelProject(3000, 3000));
            var plan = s.Plan;
            Assert.Equal(new[] { "Back", "Front", "Left", "Right" }, plan.Beams.Select(b => b.Face).OrderBy(x => x).ToArray());
            foreach (var b in plan.Beams)
            {
                Assert.Equal("UPN 100", b.Profile);
                Assert.False(b.Box.Overlaps(plan.Clear));
                Assert.Equal(50, Math.Min(b.Box.Width, b.Box.Height));      // UPN 100 flange width across the face
                Assert.All(plan.Columns, c => Assert.False(b.Box.Overlaps(c.Box), b.Face + " beam must not run through a column"));
            }
            Assert.Equal(plan.Clear.Width, plan.Beams.Single(b => b.Face == "Front").Box.Width);
            Assert.Equal(plan.Clear.Height, plan.Beams.Single(b => b.Face == "Left").Box.Height);
            Assert.Equal("Front", plan.EntranceFace);
        }

        [Fact]
        public void Elevation_has_columns_beams_at_the_levels_and_braces_only_in_panels_never_on_the_entrance_face()
        {
            var s = Build(SteelProject(3000, 3000));
            Assert.Equal(new[] { "Back", "Front", "Left", "Right" }, s.Faces.Select(f => f.Name).OrderBy(x => x).ToArray());
            var panels = new[] { s.BaseMm }.Concat(s.Levels.Select(l => l.ElevationMm)).ToArray();
            foreach (var f in s.Faces)
            {
                Assert.Equal(2, f.Columns.Count);
                Assert.All(f.Columns, c => { Assert.Equal(s.BaseMm, c.Y0); Assert.Equal(s.TopMm, c.Y1); });
                Assert.Equal(s.Levels.Count, f.Beams.Count);
                Assert.Equal(s.Levels.Select(l => l.ElevationMm), f.Beams.Select(b => b.ElevationMm));
                Assert.All(f.Beams, b => { Assert.Equal(0, b.U0); Assert.Equal(f.LengthMm, b.U1); Assert.Equal(100, b.Z1 - b.Z0); });
                Assert.Equal(s.Base.BottomMm, f.BaseBlock.Y0);
                Assert.Equal(s.Base.TopMm, f.BaseBlock.Y1);
                if (f.IsEntrance)
                {
                    Assert.Equal("Front", f.Name);
                    Assert.Empty(f.Braces);
                    Assert.Empty(f.Gussets);
                    continue;
                }
                Assert.Equal(panels.Length - 1, f.Braces.Count);
                for (int i = 0; i < f.Braces.Count; i++)
                {
                    var b = f.Braces[i];
                    Assert.Equal(panels[i], b.Z0); Assert.Equal(panels[i + 1], b.Z1);        // one X in every panel, between consecutive levels
                    Assert.Equal(Math.Sqrt(f.LengthMm * f.LengthMm + (b.Z1 - b.Z0) * (b.Z1 - b.Z0)), b.LengthMm, 6);
                    Assert.Equal(b.Z0, b.DiagonalUp.Y0); Assert.Equal(b.Z1, b.DiagonalUp.Y1);
                    Assert.Equal(b.Z1, b.DiagonalDown.Y0); Assert.Equal(b.Z0, b.DiagonalDown.Y1);   // the two diagonals cross
                }
                Assert.Equal(2 * panels.Length, f.Gussets.Count);
            }
            Assert.Equal(1, s.Faces.Count(f => f.IsEntrance));
            Assert.Equal(3, s.Faces.Count(f => f.Braces.Count > 0));
            Assert.Equal(s.Plan.Clear.Width, s.Faces.Single(f => f.Name == "Back").LengthMm);
            Assert.Equal(s.Plan.Clear.Height, s.Faces.Single(f => f.Name == "Left").LengthMm);
        }

        [Fact]
        public void The_concrete_base_is_the_plate_footprint_plus_the_margin_with_bars_both_ways()
        {
            var p = SteelProject(3000);
            var s = Build(p);
            var b = s.Base;
            var fp = s.Plan.PlateFootprint;
            Assert.Equal(fp.X0 - 200, b.Plan.X0, 6); Assert.Equal(fp.X1 + 200, b.Plan.X1, 6);
            Assert.Equal(fp.Y0 - 200, b.Plan.Y0, 6); Assert.Equal(fp.Y1 + 200, b.Plan.Y1, 6);
            Assert.Equal(250, b.ThicknessMm);
            Assert.Equal(s.BaseMm, b.TopMm);
            Assert.Equal(s.BaseMm - 250, b.BottomMm);
            Assert.Equal(8, b.BarDiameterMm);
            Assert.Equal(s.Inputs.BarCountAlongX, b.BarsAlongX.Count);
            Assert.Equal(s.Inputs.BarCountAlongY, b.BarsAlongY.Count);
            Assert.All(b.BarsAlongX, bar => { Assert.Equal(bar.Y0, bar.Y1); Assert.Equal(b.Plan.X0 + 50, bar.X0, 6); Assert.Equal(b.Plan.X1 - 50, bar.X1, 6); });
            Assert.All(b.BarsAlongY, bar => { Assert.Equal(bar.X0, bar.X1); Assert.Equal(b.Plan.Y0 + 50, bar.Y0, 6); });
            // the counts come from the preset spacing of 120: spacing within it
            double spacing = (b.BarsAlongX[1].Y0 - b.BarsAlongX[0].Y0);
            Assert.InRange(spacing, 100, 120 + 1e-9);   // at most the preset spacing of 120, and not much less
            Assert.Equal(b.Plan.Width * b.Plan.Height * 250 * 1e-9, b.VolumeM3, 9);
            // a locked margin and thickness change the block
            SteelFields.BaseMargin.Edit(p, 300.0);
            SteelFields.BaseThickness.Edit(p, 400.0);
            var t = Build(p);
            Assert.Equal(s.Base.Plan.Width + 200, t.Base.Plan.Width, 6);
            Assert.Equal(400, t.Base.ThicknessMm);
        }

        [Fact]
        public void Tower_top_is_the_last_beam_ring_within_the_shaft_footprint_without_outriggers()
        {
            var s = Build(SteelProject(3000, 3000));
            var top = s.TowerTop;
            Assert.Equal(s.TopMm, top.ElevationMm);
            Assert.Equal(s.Levels.Last().ElevationMm, top.ElevationMm);
            Assert.Equal(s.HighestFloorMm + s.Inputs.OverheadMm, top.ElevationMm);
            Assert.False(top.Outriggers);
            Assert.Equal(4, top.Columns.Count);
            Assert.Equal(4, top.Beams.Count);
            Assert.Equal(0, top.MachineEnvelope.X0); Assert.Equal(0, top.MachineEnvelope.Y0);
            Assert.Equal(s.Plan.Clear.Width, top.MachineEnvelope.Width); Assert.Equal(s.Plan.Clear.Height, top.MachineEnvelope.Height);
            // nothing of the ring reaches beyond the steel of the plan columns: no outrigger
            Assert.Equal(s.Plan.Columns.Min(c => c.Box.X0), top.Outline.X0);
            Assert.Equal(s.Plan.Columns.Max(c => c.Box.Y1), top.Outline.Y1);
        }

        // ---------- member list ----------
        [Fact]
        public void Member_list_counts_lengths_and_masses_of_the_steel()
        {
            var p = SteelProject(3000);
            var s = Build(p);
            int n = s.Levels.Count;
            var col = s.Members.Single(m => m.Item == "Column");
            Assert.Equal("UPN 140", col.Profile); Assert.Equal(4, col.Count);
            Assert.Equal(s.TopMm - s.BaseMm, col.LengthMm);
            Assert.Equal(16.0 * col.LengthMm / 1000, col.MassPerPieceKg, 9);
            var bw = s.Members.Single(m => m.Item == "Beam along the width");
            var bd = s.Members.Single(m => m.Item == "Beam along the depth");
            Assert.Equal(2 * n, bw.Count); Assert.Equal(2 * n, bd.Count);
            Assert.Equal(p.Shaft.WidthMm, bw.LengthMm); Assert.Equal(p.Shaft.DepthMm, bd.LengthMm);
            Assert.Equal("UPN 100", bw.Profile);
            Assert.Equal(10.6 * bw.LengthMm / 1000, bw.MassPerPieceKg, 9);
            var braces = s.Members.Where(m => m.Item == "Brace").ToList();
            Assert.All(braces, m => { Assert.Equal("L50x50x5", m.Profile); Assert.Equal(0, m.Count % 2); });
            Assert.Equal(2 * 3 * n, braces.Sum(m => m.Count));       // 3 braced faces, n panels (base + levels), two diagonals each
            var plate = s.Members.Single(m => m.Item == "Base plate");
            Assert.Equal(4, plate.Count); Assert.Equal("PL 200x200x15", plate.Profile);
            Assert.Equal(200 * 200 * 15 * 1e-9 * 7850, plate.MassPerPieceKg, 9);
            var gusset = s.Members.Single(m => m.Item == "Gusset plate");
            Assert.Equal("PL 400x250x6", gusset.Profile);
            Assert.Equal(3 * 2 * (n + 1), gusset.Count);
            Assert.Equal(16, s.Members.Single(m => m.Item == "Anchor bolt").Count);
            Assert.Equal(s.Members.Sum(m => m.MassPerPieceKg * m.Count), s.TotalMassKg, 9);
            Assert.True(s.TotalMassKg > col.MassPerPieceKg * 4);
            // a bigger column and beam profile raise the mass
            SteelFields.ColumnProfile.Edit(p, "UPN 200");
            SteelFields.BeamProfile.Edit(p, "UPN 140");
            Assert.True(Build(p).TotalMassKg > s.TotalMassKg);
        }

        [Fact]
        public void Missing_inputs_give_problems_not_exceptions()
        {
            var p = SteelProject(3000);
            SteelFields.ColumnProfile.Edit(p, "UPN 999");
            var s = SteelStructure.Build(p, Library);
            Assert.False(s.Valid);
            Assert.Contains(s.Inputs.Problems, m => m.Contains("column profile"));
            Assert.Empty(s.Levels);
            Assert.Null(SteelStructure.DerivedSpacing(new PresetContext { Project = p, Library = Library }));
            // a plain catalogue lookup of a derived field on a bad project does not throw either
            Assert.NotNull(SteelFields.BarCountAlongX.Effective(new PresetContext { Project = SteelProject(3000), Library = Library }));
        }
    }
}
