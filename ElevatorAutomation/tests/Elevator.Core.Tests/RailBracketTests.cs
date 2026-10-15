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
