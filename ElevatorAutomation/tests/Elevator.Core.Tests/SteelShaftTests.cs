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
            copy.SteelShaft.Channels[0].MassKgPerM = 99;
            Assert.Equal(before, copy.Hash());
            // an older library without the group falls back to the shipped defaults
            copy.SteelShaft = null;
            Assert.Equal("UPN 140", copy.SteelShaftOrDefault().ColumnProfile);
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

        // ---------- the beam-level rule, per face ----------
        static double[] Side(double[] floors, double s, double baseMm, double topMm)
        {
            return SteelBeamLevels.Rule(floors, 2160, s, baseMm, topMm, false).Select(l => l.ElevationMm).ToArray();
        }

        static double[] Entrance(double[] floors, double s, double baseMm, double topMm)
        {
            return SteelBeamLevels.Rule(floors, 2160, s, baseMm, topMm, true).Select(l => l.ElevationMm).ToArray();
        }

        [Fact]
        public void Side_face_has_a_beam_at_every_floor_and_each_gap_is_divided_equally_into_the_fewest_parts_not_larger_than_s()
        {
            // floors 3000, s 1500: two parts of 1500; s 2000: two parts of 1500 (the fewest parts not larger than 2000)
            Assert.Equal(new[] { 0.0, 1500, 3000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000 + 1000, false), 0, 3000));
            Assert.Equal(new[] { 0.0, 1500, 3000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 2000, -1000, 3000 + 1000, false), 0, 3000));
            // floors 3250: s 1500 gives 3 parts (1083.33), s 2000 gives 2 parts (1625)
            AssertLevels(new[] { 0.0, 3250.0 / 3, 6500.0 / 3, 3250 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3250 }, 2160, 1500, -1000, 4250, false), 0, 3250));
            AssertLevels(new[] { 0.0, 1625, 3250 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3250 }, 2160, 2000, -1000, 4250, false), 0, 3250));
            // floors 3670: s 1500 gives 3 parts (1223.33), s 2000 gives 2 parts (1835)
            AssertLevels(new[] { 0.0, 3670.0 / 3, 7340.0 / 3, 3670 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3670 }, 2160, 1500, -1000, 4670, false), 0, 3670));
            AssertLevels(new[] { 0.0, 1835, 3670 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3670 }, 2160, 2000, -1000, 4670, false), 0, 3670));
            // floors 6000: s 1500 gives 4 parts, s 2000 gives 3 parts
            AssertLevels(new[] { 0.0, 1500, 3000, 4500, 6000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 1500, -1000, 7000, false), 0, 6000));
            AssertLevels(new[] { 0.0, 2000, 4000, 6000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 2000, -1000, 7000, false), 0, 6000));
            // a gap equal to s is one part, a hair above s is two parts
            Assert.Equal(new[] { 0.0, 1500 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 1500 }, 2160, 1500, -1000, 2500, false), 0, 1500));
            Assert.Equal(3, Between(SteelBeamLevels.Rule(new[] { 0.0, 1500.5 }, 2160, 1500, -1000, 2500, false), 0, 1501).Length);
        }

        static void AssertLevels(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.Equal(expected[i], actual[i], 6);
        }

        [Fact]
        public void Entrance_face_has_floor_and_door_head_beams_the_opening_is_not_split_and_the_rest_is_divided_equally()
        {
            // floor 3000: opening 0..2160 whole; 2160..3000 is 840 <= s: no beam
            Assert.Equal(new[] { 0.0, 2160, 3000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 4000, true), 0, 3000));
            // floor 3250: 2160..3250 is 1090 <= 1500: no beam
            Assert.Equal(new[] { 0.0, 2160, 3250 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3250 }, 2160, 1500, -1000, 4250, true), 0, 3250));
            // floor 3670: 2160..3670 is 1510 > 1500: two equal parts of 755; with s 2000 one part
            AssertLevels(new[] { 0.0, 2160, 2915, 3670 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3670 }, 2160, 1500, -1000, 4670, true), 0, 3670));
            AssertLevels(new[] { 0.0, 2160, 3670 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3670 }, 2160, 2000, -1000, 4670, true), 0, 3670));
            // floor 6000: 2160..6000 is 3840: three parts of 1280 at s 1500, two parts of 1920 at s 2000
            AssertLevels(new[] { 0.0, 2160, 3440, 4720, 6000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 1500, -1000, 7000, true), 0, 6000));
            AssertLevels(new[] { 0.0, 2160, 4080, 6000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 2000, -1000, 7000, true), 0, 6000));
            // the opening is larger than s: it still is not split
            Assert.Equal(new[] { 0.0, 2160, 3000 }, Between(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1000, -1000, 4000, true).Where(l => l.ElevationMm < 2200 || l.ElevationMm >= 2900).ToList(), 0, 3000));
            Assert.DoesNotContain(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1000, -1000, 4000, true), l => l.ElevationMm > 0 && l.ElevationMm < 2160);
        }

        [Fact]
        public void Pit_and_top_use_the_same_equal_division_on_every_face()
        {
            // pit 1150 at s 1500: no beam; pit 3100 at s 1500: three parts of 1033.33 (two beams)
            Assert.Empty(SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1150, 7200, false).Where(l => l.Kind == SteelLevelKind.Pit));
            AssertLevels(new[] { -3100.0 * 2 / 3, -3100.0 / 3 }, SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -3100, 7200, false).Where(l => l.Kind == SteelLevelKind.Pit).Select(l => l.ElevationMm).ToArray());
            AssertLevels(new[] { -3100.0 * 2 / 3, -3100.0 / 3 }, SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -3100, 7200, true).Where(l => l.Kind == SteelLevelKind.Pit).Select(l => l.ElevationMm).ToArray());
            // top (side face): from the top floor to the tower top, 4100 at s 1500 is three parts, the last beam is the tower top
            var top = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000 + 4100, false).Where(l => l.ElevationMm >= 3000).Select(l => l.ElevationMm).ToArray();
            AssertLevels(new[] { 3000.0, 3000 + 4100.0 / 3, 3000 + 8200.0 / 3, 7100 }, top);
            // top (entrance face): the door head of the top floor, then the equal division up to the tower top
            var etop = SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000 + 4100, true).Where(l => l.ElevationMm >= 3000).Select(l => l.ElevationMm).ToArray();
            AssertLevels(new[] { 3000.0, 5160, 5160 + 1940.0 / 2, 7100 }, etop);
            Assert.Equal(7100, SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 7100, false).Last().ElevationMm);
        }

        [Fact]
        public void No_gap_is_smaller_than_a_millimetre_and_none_above_a_door_head_or_between_side_floors_exceeds_s()
        {
            foreach (double s in new[] { 1500.0, 2000 })
            {
                foreach (double h in new[] { 3000.0, 3250, 3670, 6000, 2400, 5555.5 })
                {
                    var floors = new[] { 0.0, h, 2 * h };
                    foreach (bool entrance in new[] { false, true })
                    {
                        var levels = SteelBeamLevels.Rule(floors, 2160, s, -1100, 2 * h + 4200, entrance);
                        for (int i = 1; i < levels.Count; i++)
                        {
                            double gap = levels[i].ElevationMm - levels[i - 1].ElevationMm;
                            Assert.True(gap >= 1, "gap " + gap + " at " + levels[i].Key + " h " + h + " s " + s);
                            bool opening = entrance && levels[i - 1].Kind == SteelLevelKind.Floor && levels[i].Kind == SteelLevelKind.DoorHead;
                            if (!opening)
                                Assert.True(gap <= s + 1e-6, "gap " + gap + " above " + levels[i - 1].Key + " h " + h + " s " + s + (entrance ? " entrance" : " side"));
                        }
                        // equal division: the gaps of one segment between fixed beams are all the same
                        var fixedKeys = levels.Where(l => l.Kind == SteelLevelKind.Floor || l.Kind == SteelLevelKind.DoorHead || l.Key == "T:top").ToList();
                        for (int i = 1; i < fixedKeys.Count; i++)
                        {
                            var inside = levels.Where(l => l.ElevationMm >= fixedKeys[i - 1].ElevationMm - 1e-9 && l.ElevationMm <= fixedKeys[i].ElevationMm + 1e-9).Select(l => l.ElevationMm).ToList();
                            var gaps = inside.Zip(inside.Skip(1), (a, b) => b - a).ToList();
                            Assert.True(gaps.Max() - gaps.Min() < 1e-6, "unequal gaps in a segment");
                        }
                        Assert.Equal(3, levels.Count(l => l.Kind == SteelLevelKind.Floor));
                        Assert.Equal(entrance ? 3 : 0, levels.Count(l => l.Kind == SteelLevelKind.DoorHead));
                    }
                }
            }
        }

        [Fact]
        public void Levels_are_tagged_and_ordered()
        {
            var levels = SteelBeamLevels.Rule(new[] { 0.0, 6000 }, 2160, 1500, -3100, 6000 + 4200, true);
            Assert.Equal(levels.Select(l => l.ElevationMm).OrderBy(x => x).ToArray(), levels.Select(l => l.ElevationMm).ToArray());
            var tags = levels.ToDictionary(l => l.Key, l => l.Tag);
            Assert.Equal("floor", tags["F:0"]);
            Assert.Equal("floor", tags["F:1"]);
            Assert.Equal("door head", tags["D:0"]);
            Assert.Equal("door head", tags["D:1"]);
            Assert.Equal("intermediate", tags["I:0:1"]);
            Assert.Equal("pit", tags["P:1"]);
            Assert.Equal("top", tags["T:top"]);
            Assert.Equal(10200, levels.Last().ElevationMm);
        }

        [Fact]
        public void The_rule_rejects_what_it_cannot_place_and_reports_a_door_head_that_does_not_fit()
        {
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 0, -1000, 7200, false));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 0, 1500, -1000, 7200, true));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, 0, 7200, false));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000 }, 2160, 1500, -1000, 3000, false));
            Assert.Throws<ArgumentException>(() => SteelBeamLevels.Rule(new[] { 0.0, 3000, 3000 }, 2160, 1500, -1000, 7200, false));
            // a floor not above the door height: no door-head beam there on the entrance face, and a note
            var low = SteelBeamLevels.Rule(new[] { 0.0, 2000, 5000 }, 2160, 1500, -1000, 9000, true);
            Assert.DoesNotContain(low, l => l.Key == "D:0");
            var floors = new[] { 0.0, 2000, 5000 }.Select((e, i) => new FloorElevation { Name = i.ToString(), ElevationMm = e, IsServed = true }).ToList();
            var issues = SteelBeamLevels.Issues(floors, 2160, 1500, -1000, 9000);
            Assert.Contains(issues, m => m.StartsWith("At floor 0"));
            // a gap at a door is by design: no note about it
            Assert.DoesNotContain(issues, m => m.Contains("cannot be split"));
            Assert.Empty(SteelBeamLevels.Issues(floors.Skip(1).ToList(), 2160, 1000, -1000, 9000));
        }

        [Fact]
        public void Edited_levels_lock_per_face_and_Reset_restores_the_rule()
        {
            var p = SteelProject(6000);
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, 1500.0);
            var s0 = Build(p);
            var rule = s0.RuleLevels["Left"];
            AssertLevels(new[] { 0.0, 1500, 3000, 4500, 6000 }, Between(rule, 0, 6000));
            string first = rule.First(l => l.Kind == SteelLevelKind.Intermediate).Key;
            SteelFields.EditLevel(p, "Left", first, 1000);
            SteelFields.RemoveLevel(p, "Left", rule.Where(l => l.Kind == SteelLevelKind.Intermediate).ElementAt(1).Key);
            SteelFields.EditLevel(p, "Left", "F:G", 123);      // a floor level is fixed by the building: ignored
            SteelFields.EditLevel(p, "Left", "C:1", 4400);     // an added level
            var t = Build(p);
            Assert.True(t.Levels["Left"].Single(l => l.Key == first).Edited);
            Assert.Equal(1000, t.Levels["Left"].Single(l => l.Key == first).ElevationMm);
            Assert.True(t.Levels["Left"].Single(l => l.Key == "C:1").Edited);
            Assert.Contains(t.Levels["Left"], l => l.Key == "F:G" && l.ElevationMm == 0);
            // the other faces keep the rule
            Assert.Equal(t.RuleLevels["Right"].Select(l => l.ElevationMm), t.Levels["Right"].Select(l => l.ElevationMm));
            Assert.Equal(rule.Select(l => l.ElevationMm), t.RuleLevels["Left"].Select(l => l.ElevationMm));   // the rule itself is untouched
            // the edits survive the project file
            var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(t.Levels["Left"].Select(l => l.ElevationMm), Build(again).Levels["Left"].Select(l => l.ElevationMm));
            Assert.Equal(4, SteelFields.LevelEdits(again, "Left").Count);
            Assert.Empty(SteelFields.LevelEdits(again, "Right"));
            // one level back to the rule, then all
            SteelFields.ResetLevel(p, "Left", first);
            Assert.Equal(rule.Single(l => l.Key == first).ElevationMm, Build(p).Levels["Left"].Single(l => l.Key == first).ElevationMm);
            SteelFields.ResetLevels(p, "Left");
            Assert.False(SteelFields.LevelsEdited(p));
            Assert.Equal(rule.Select(l => l.ElevationMm), Build(p).Levels["Left"].Select(l => l.ElevationMm));
            Assert.False(p.Extensions.ContainsKey("steel") && ((IDictionary<string, object>)p.Extensions["steel"]).ContainsKey("levelEdits"));
        }

        [Fact]
        public void An_edit_of_a_level_the_rule_no_longer_has_is_ignored()
        {
            var p = SteelProject(6000);
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, 1500.0);
            SteelFields.EditLevel(p, "Back", "I:G:3", 4000);
            Assert.Contains(Build(p).Levels["Back"], l => l.Key == "I:G:3" && l.ElevationMm == 4000);
            // a shorter first floor has no third intermediate beam any more
            p.Building.Floors[0].HeightToNextMm = 2000;
            var s = Build(p);
            Assert.DoesNotContain(s.Levels["Back"], l => l.Key == "I:G:3");
        }

        [Fact]
        public void The_beam_spacing_is_the_car_rail_spacing_of_the_rails_page()
        {
            var p = SteelProject(6000);
            Assert.Equal(Convert.ToDouble(p.Rails.CarSpacingMm), Build(p).Inputs.SpacingMm);
            // typing the car spacing on RAILS changes the beams
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, 3000.0);
            var s = Build(p);
            Assert.Equal(3000.0, s.Inputs.SpacingMm);
            AssertLevels(new[] { 0.0, 3000, 6000 }, Between(s.Levels["Left"], 0, 6000));
            AssertLevels(new[] { 0.0, 2160, 4080, 6000 }, Between(s.Levels["Front"], 0, 6000).Where(x => x <= 6000).ToArray());
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
            Assert.Equal(200.0, SteelFields.PlateWidth.Effective(c)); Assert.Equal(200.0, SteelFields.PlateDepth.Effective(c)); Assert.Equal(15.0, SteelFields.PlateThickness.Effective(c));
            Assert.Equal(4.0, SteelFields.BoltCount.Effective(c)); Assert.Equal(20.0, SteelFields.BoltDiameter.Effective(c));
            Assert.Equal(400.0, SteelFields.GussetLength.Effective(c)); Assert.Equal(250.0, SteelFields.GussetWidth.Effective(c)); Assert.Equal(6.0, SteelFields.GussetThickness.Effective(c));
            Assert.Equal("St 37", SteelFields.SteelGrade.Effective(c));
            Assert.Equal(200.0, SteelFields.BaseMargin.Effective(c)); Assert.Equal(250.0, SteelFields.BaseThickness.Effective(c)); Assert.Equal(8.0, SteelFields.BarDiameter.Effective(c));
            Assert.False(SteelFields.All.Any(f => f.IsEdited(p) && f != SteelFields.Construction));
            // a unlocked value follows the library
            var other = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            other.SteelShaft.ColumnProfile = "UPN 160";
            var c2 = new PresetContext { Project = p, Library = other };
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c2));
        }

        [Fact]
        public void An_edited_value_locks_follows_nothing_and_Reset_returns_to_the_preset()
        {
            var p = SteelProject();
            SteelFields.PlateWidth.Edit(p, 250.0);
            SteelFields.ColumnProfile.Edit(p, "UPN 160");
            Assert.True(SteelFields.PlateWidth.IsEdited(p));
            var other = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            other.SteelShaft.BasePlateWidthMm = 180;
            var c = new PresetContext { Project = p, Library = other };
            Assert.Equal(250.0, SteelFields.PlateWidth.Effective(c));       // locked
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c));
            Assert.Equal("UPN 100", SteelFields.BeamProfile.Effective(c));    // not edited: still the preset
            var again = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(250.0, SteelFields.PlateWidth.Effective(new PresetContext { Project = again, Library = other }));
            Assert.True(SteelFields.PlateWidth.IsEdited(again));
            SteelFields.PlateWidth.Reset(p);
            Assert.False(SteelFields.PlateWidth.IsEdited(p));
            Assert.Equal(180.0, SteelFields.PlateWidth.Effective(new PresetContext { Project = p, Library = other }));
            Assert.Equal("UPN 160", SteelFields.ColumnProfile.Effective(c));
            SteelFields.ColumnProfile.Reset(p);
            SteelFields.Construction.Reset(p);
            Assert.False(SteelFields.IsSteel(p, Library));
            Assert.False(ProjectJson.Serialize(p).ToLowerInvariant().Contains("steel"));
            Assert.Same(SteelFields.PlateWidth, ProjectPresets.Find("Steel.BasePlateWidthMm"));
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

        // ---------- the rails drive the beams; the check uses the largest gap of the side and back faces ----------
        [Fact]
        public void The_largest_gap_of_the_side_and_back_faces_is_not_more_than_s_and_the_rail_check_uses_it()
        {
            var ctx = TestData.Context();
            var p = SteelProject(3200, 3200);
            var s = Build(p);
            double spacing = Convert.ToDouble(p.Rails.CarSpacingMm);
            Assert.Equal(spacing, s.Inputs.SpacingMm);
            Assert.True(s.LargestGapMm.Value <= spacing + 1e-6);
            Assert.Equal(spacing, p.Rails.CwSpacingMm);                       // the rails keep their own values: nothing is derived from the beams
            Assert.Equal(spacing, p.Rails.BracketMaximumSpacingMm);
            var check = GuideRailCheckEngine.Calculate(p, ctx, Library);
            Assert.True(check.Available);
            Assert.Equal(s.LargestGapMm.Value, check.Car.SpacingMm, 6);
            Assert.Equal(s.LargestGapMm.Value, check.Counterweight.SpacingMm, 6);
            // a typed spacing changes the beams
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, 1000.0);
            Assert.Equal(1000.0, Build(p).Inputs.SpacingMm);
            Assert.True(Build(p).LargestGapMm.Value <= 1000 + 1e-6);
            // an edited level on a bracket face changes the gap
            var q = SteelProject(6000);
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(q, 1500.0);
            var rule = Build(q).RuleLevels["Left"];
            SteelFields.RemoveLevel(q, "Left", rule.First(l => l.Kind == SteelLevelKind.Intermediate).Key);
            Assert.Equal(3000.0, Build(q).LargestGapMm.Value, 6);
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
            Assert.Null(SteelStructure.CheckSpacing(new PresetContext { Project = a, Library = Library }));
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
            foreach (var f in s.Faces)
            {
                var panels = new[] { s.BaseMm }.Concat(s.Levels[f.Name].Select(l => l.ElevationMm)).ToArray();
                Assert.Equal(2, f.Columns.Count);
                Assert.All(f.Columns, c => { Assert.Equal(s.BaseMm, c.Y0); Assert.Equal(s.TopMm, c.Y1); });
                Assert.Equal(s.Levels[f.Name].Count, f.Beams.Count);
                Assert.Equal(s.Levels[f.Name].Select(l => l.ElevationMm), f.Beams.Select(b => b.ElevationMm));
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
            Assert.Equal(s.Levels["Back"].Last().ElevationMm, top.ElevationMm);
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
            int nWidth = s.Levels["Front"].Count + s.Levels["Back"].Count, nDepth = s.Levels["Left"].Count + s.Levels["Right"].Count;
            var col = s.Members.Single(m => m.Item == "Column");
            Assert.Equal("UPN 140", col.Profile); Assert.Equal(4, col.Count);
            Assert.Equal(s.TopMm - s.BaseMm, col.LengthMm);
            Assert.Equal(16.0 * col.LengthMm / 1000, col.MassPerPieceKg, 9);
            var bw = s.Members.Single(m => m.Item == "Beam along the width");
            var bd = s.Members.Single(m => m.Item == "Beam along the depth");
            Assert.Equal(nWidth, bw.Count); Assert.Equal(nDepth, bd.Count);
            Assert.Equal(p.Shaft.WidthMm, bw.LengthMm); Assert.Equal(p.Shaft.DepthMm, bd.LengthMm);
            Assert.Equal("UPN 100", bw.Profile);
            Assert.Equal(10.6 * bw.LengthMm / 1000, bw.MassPerPieceKg, 9);
            var braces = s.Members.Where(m => m.Item == "Brace").ToList();
            Assert.All(braces, m => { Assert.Equal("L50x50x5", m.Profile); Assert.Equal(0, m.Count % 2); });
            Assert.Equal(2 * (s.Levels["Back"].Count + s.Levels["Left"].Count + s.Levels["Right"].Count), braces.Sum(m => m.Count));   // 3 braced faces, a panel per level, two diagonals each
            var plate = s.Members.Single(m => m.Item == "Base plate");
            Assert.Equal(4, plate.Count); Assert.Equal("PL 200x200x15", plate.Profile);
            Assert.Equal(200 * 200 * 15 * 1e-9 * 7850, plate.MassPerPieceKg, 9);
            var gusset = s.Members.Single(m => m.Item == "Gusset plate");
            Assert.Equal("PL 400x250x6", gusset.Profile);
            Assert.Equal(2 * (s.Levels["Back"].Count + s.Levels["Left"].Count + s.Levels["Right"].Count + 3), gusset.Count);
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
            Assert.Null(SteelStructure.CheckSpacing(new PresetContext { Project = p, Library = Library }));
            // a plain catalogue lookup of a derived field on a bad project does not throw either
            Assert.NotNull(SteelFields.BarCountAlongX.Effective(new PresetContext { Project = SteelProject(3000), Library = Library }));
        }
    }
}
