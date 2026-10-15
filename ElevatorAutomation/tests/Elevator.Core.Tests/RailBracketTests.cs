using System;
using System.IO;
using System.Linq;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
using Xunit;

namespace Elevator.Core.Tests
{
    // G1: the bracket preset of each rail profile (BRACKETS.md), its lock and follow, and that a project without edits is unchanged.
    public class RailBracketTests
    {
        static readonly Lazy<PresetLibrary> Lib = new Lazy<PresetLibrary>(() => PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")));
        static PresetLibrary Library { get { return Lib.Value; } }

        static ElevatorProject NewProject()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(Library, TestData.Context(), seed.Counterweight, seed.Rails.BracketSource);
        }

        [Theory]
        [InlineData("T50/50/5", 40, 8)]
        [InlineData("T70/65/9", 50, 10)]
        [InlineData("T89/62/16", 60, 12)]
        [InlineData("T90/75/16", 60, 12)]
        public void Every_catalog_profile_has_the_bracket_of_its_blade_row(string profile, double clipLength, double boltDiameter)
        {
            var row = Library.GuideRailsOrDefault().Profile(profile);
            Assert.NotNull(row.Bracket);
            Assert.Equal(clipLength, row.Bracket.ClipLengthMm);
            Assert.Equal(boltDiameter, row.Bracket.ClipBoltDiameterMm);
            // the family common to every rail (the template library shows one)
            Assert.Equal(270, row.Bracket.AngleLengthMm);
            Assert.Equal(50, row.Bracket.AngleLegAMm);
            Assert.Equal(40, row.Bracket.AngleLegBMm);
            Assert.Equal(10, row.Bracket.AssemblyBoltDiameterMm);
            Assert.Equal(12, row.Bracket.AnchorDiameterMm);
            Assert.Equal(150, row.Bracket.AnchorLengthMm);
        }

        [Fact]
        public void A_profile_without_a_bracket_takes_the_row_of_its_blade_or_the_next_heavier_one()
        {
            Assert.Equal(40, RailBracketPreset.ForBlade(5).ClipLengthMm);
            Assert.Equal(50, RailBracketPreset.ForBlade(7).ClipLengthMm);
            Assert.Equal(50, RailBracketPreset.ForBlade(9).ClipLengthMm);
            Assert.Equal(60, RailBracketPreset.ForBlade(10).ClipLengthMm);
            Assert.Equal(60, RailBracketPreset.ForBlade(16).ClipLengthMm);
        }

        [Fact]
        public void The_bracket_follows_the_profile_until_a_value_is_edited()
        {
            var p = NewProject();
            var ctx = new PresetContext { Project = p, Library = Library };
            var clip = RailBracketFields.Find("Rails.Bracket.Car.ClipLengthMm");
            ProjectPresets.Find(ProjectPresets.CarProfileKey).Edit(p, "T50/50/5");
            Assert.Equal(40.0, (double)clip.Effective(ctx));
            ProjectPresets.Find(ProjectPresets.CarProfileKey).Edit(p, "T89/62/16");
            Assert.Equal(60.0, (double)clip.Effective(ctx));
            Assert.False(clip.IsEdited(p));

            clip.Edit(p, 75.0);
            Assert.True(clip.IsEdited(p));
            ProjectPresets.Find(ProjectPresets.CarProfileKey).Edit(p, "T70/65/9");
            Assert.Equal(75.0, (double)clip.Effective(ctx));
            Assert.Equal(75.0, RailBracketFields.Effective(p, Library, "Car").ClipLengthMm);
            // the other values and the other rail still follow
            Assert.Equal(10.0, RailBracketFields.Effective(p, Library, "Car").ClipBoltDiameterMm);
            Assert.False(RailBracketFields.Find("Rails.Bracket.CW.ClipLengthMm").IsEdited(p));

            clip.Reset(p);
            Assert.False(clip.IsEdited(p));
            Assert.Equal(50.0, (double)clip.Effective(ctx));
            Assert.False(RailBracketFields.AnyEdited(p));
        }

        [Fact]
        public void The_car_and_the_counterweight_brackets_follow_their_own_profiles()
        {
            var p = NewProject();
            ProjectPresets.Find(ProjectPresets.CarProfileKey).Edit(p, "T89/62/16");
            ProjectPresets.Find(ProjectPresets.CwProfileKey).Edit(p, "T50/50/5");
            Assert.Equal(60, RailBracketFields.Effective(p, Library, "Car").ClipLengthMm);
            Assert.Equal(40, RailBracketFields.Effective(p, Library, "CW").ClipLengthMm);
        }

        [Fact]
        public void A_project_without_bracket_edits_has_no_extension_and_serializes_unchanged()
        {
            var p = NewProject();
            Assert.True(p.Extensions == null || !p.Extensions.ContainsKey("railBrackets"));
            var before = ProjectJson.Serialize(p);
            RailBracketFields.Find("Rails.Bracket.Car.AnchorLengthMm").Edit(p, 200.0);
            Assert.NotEqual(before, ProjectJson.Serialize(p));
            RailBracketFields.Find("Rails.Bracket.Car.AnchorLengthMm").Reset(p);
            Assert.Equal(before, ProjectJson.Serialize(p));
        }

        static RailFixingFacts Facts(string family, double sw, double dbg, double h1, double b1, double rear, bool steel = false)
        {
            return new RailFixingFacts { Family = family, ShaftWidthMm = sw, DbgMm = dbg, RailHeightMm = h1, RailWidthMm = b1, RearWallToRowMm = rear, Steel = steel };
        }

        const double CarReach = 300, CwSideReach = 100;

        [Fact]
        public void Auto_gives_car_wall_brackets_and_a_cross_arm_for_the_cw_rails_of_the_rcw_default()
        {
            // SW 1500: car DBG 1200 (T70/65/9), CW DBG 1040 (T50/50/5), rear wall to the CW row 125, as the plan draws
            var car = RailFixing.Decide(Facts("Car", 1500, 1200, 65, 70, 0), RailFixing.Auto, CarReach);
            Assert.Equal(RailFixingType.WallBracket, car.Type);
            Assert.Equal(85, car.ArmLengthMm, 6);
            Assert.Equal("side wall", car.Surface);
            var cw = RailFixing.Decide(Facts("CW", 1500, 1040, 50, 50, 125), RailFixing.Auto, CwSideReach);
            Assert.Equal(RailFixingType.CrossArm, cw.Type);
            Assert.Equal("rear wall", cw.Surface);
            Assert.Equal(100, cw.ArmLengthMm, 6);
            Assert.Equal(180, cw.SideGapMm, 6);
            Assert.True(cw.Automatic);
        }

        [Fact]
        public void The_cw_rails_take_a_wall_bracket_only_within_the_side_reach()
        {
            // a gap of 100 (DBG 1200 in SW 1500 with a 50 mm rail) is within the reach of 100; 180 is not
            var near = RailFixing.Decide(Facts("CW", 1500, 1200, 50, 50, 125), RailFixing.Auto, CwSideReach);
            Assert.Equal(100, near.SideGapMm, 6);
            Assert.Equal(RailFixingType.WallBracket, near.Type);
            Assert.Equal("side wall", near.Surface);
            Assert.Equal(100, near.ArmLengthMm, 6);
            Assert.Equal(RailFixingType.CrossArm, RailFixing.Decide(Facts("CW", 1500, 1040, 50, 50, 125), RailFixing.Auto, CwSideReach).Type);
            // a wide shaft: far from the side walls, a cross arm as well
            var wide = RailFixing.Decide(Facts("CW", 1800, 1040, 50, 50, 125), RailFixing.Auto, CwSideReach);
            Assert.Equal(RailFixingType.CrossArm, wide.Type);
            Assert.Equal(330, wide.SideGapMm, 6);
            // the reach is a preset
            Assert.Equal(RailFixingType.WallBracket, RailFixing.Decide(Facts("CW", 1500, 1040, 50, 50, 125), RailFixing.Auto, 200).Type);
        }

        [Fact]
        public void A_chosen_fixing_is_kept_and_the_steel_surfaces_are_beams()
        {
            var f = Facts("CW", 1500, 1040, 50, 50, 125, true);
            var arm = RailFixing.Decide(f, RailFixing.CrossArmName, CwSideReach);
            Assert.Equal(RailFixingType.CrossArm, arm.Type);
            Assert.False(arm.Automatic);
            Assert.Equal("rear beam", arm.Surface);
            // Auto in a steel shaft: the same numbers, the rear beam
            Assert.Equal("rear beam", RailFixing.Decide(f, RailFixing.Auto, CwSideReach).Surface);
            var wall = RailFixing.Decide(Facts("CW", 1500, 1040, 50, 50, 125, true), RailFixing.WallBracketName, CwSideReach);
            Assert.Equal(RailFixingType.WallBracket, wall.Type);
            Assert.Equal("side beam", wall.Surface);
            Assert.Equal(180, wall.ArmLengthMm, 6);
        }

        [Fact]
        public void A_rail_without_a_rear_surface_takes_the_side_surface_whatever_the_gap()
        {
            // the side counterweight (SCWL / SCWR) has no plan layout yet: no rear dimension, the rail takes what the plan gives
            var d = RailFixing.Decide(Facts("CW", 2400, 1040, 50, 50, 0), RailFixing.Auto, CwSideReach);
            Assert.Equal(RailFixingType.WallBracket, d.Type);
        }

        [Fact]
        public void The_fixing_presets_follow_the_library_until_edited()
        {
            var p = NewProject();
            Assert.Equal(300.0, RailBracketFields.EffectiveFixing(p, Library).CarReachMm);
            Assert.Equal(100.0, RailBracketFields.EffectiveFixing(p, Library).CwSideReachMm);
            Assert.Equal(RailFixing.Auto, RailBracketFields.FixingSetting(p, Library, "CW"));
            RailBracketFields.Reach.Edit(p, 350.0);
            RailBracketFields.CwReach.Edit(p, 200.0);
            RailBracketFields.FixingCw.Edit(p, RailFixing.WallBracketName);
            Assert.Equal(350.0, RailBracketFields.EffectiveFixing(p, Library).CarReachMm);
            Assert.Equal(200.0, RailBracketFields.EffectiveFixing(p, Library).CwSideReachMm);
            Assert.Equal(RailFixing.WallBracketName, RailBracketFields.FixingSetting(p, Library, "CW"));
            Assert.Equal(RailFixing.Auto, RailBracketFields.FixingSetting(p, Library, "Car"));
            RailBracketFields.Reach.Reset(p);
            RailBracketFields.CwReach.Reset(p);
            RailBracketFields.FixingCw.Reset(p);
            Assert.False(RailBracketFields.AnyEdited(p));
            Assert.Equal("UPE 120", RailBracketFields.EffectiveFixing(p, Library).ArmProfile);
        }

        [Fact]
        public void The_shipped_defaults_equal_the_file_and_validate()
        {
            var shipped = GuideRailDefaults.Create();
            var file = Library.GuideRailsOrDefault();
            Assert.Empty(file.Validate());
            foreach (var row in shipped.Profiles)
            {
                var other = file.Profile(row.Name);
                Assert.Equal(row.Bracket.ClipLengthMm, other.Bracket.ClipLengthMm);
                Assert.Equal(row.Bracket.ClipBoltDiameterMm, other.Bracket.ClipBoltDiameterMm);
            }
        }
    }
}
