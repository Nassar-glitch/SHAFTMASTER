using System;
using System.IO;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Tests;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Sheet;
using Xunit;

namespace Elevator.Reporting.Tests
{
    // Guide-rail section and Design Notes lines (DESIGN-FLOW "Guide rails"): a failing choice is kept; the sheet shows the true result
    // and the Design Notes name the failed item with the needed profile or spacing.
    public class GuideRailReportTests
    {
        static ElevatorProject Project(double q, string carProfile)
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            var ctx = TestData.Context();
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
            ProjectPresets.Find("Elevator.CapacityKg").Edit(p, q);
            if (carProfile != null)
                ProjectPresets.Find(ProjectPresets.CarProfileKey).Edit(p, carProfile);
            ProjectPresets.Apply(p, lib, ctx);
            return p;
        }

        static CalculationSheetInputs Inputs()
        {
            return new CalculationSheetInputs { Presets = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")) };
        }

        [Fact]
        public void A_passing_choice_has_the_section_and_no_design_note()
        {
            var p = Project(630, null);
            var sheet = CalculationSheet.Build(p, TestData.Context(), Inputs());
            Assert.True(sheet.GuideRailsAvailable);
            Assert.True(sheet.GuideRails.Ok);
            Assert.DoesNotContain(sheet.NotOk, x => x.StartsWith("Guide rails"));
            var notes = DesignNotes.DesignNotes.Build(p, TestData.Context(), Inputs());
            Assert.DoesNotContain(notes.Warnings, w => w.Contains(" rails, "));
        }

        [Fact]
        public void A_failing_locked_profile_is_kept_shown_NOT_OK_and_explained_in_the_design_notes()
        {
            var p = Project(1600, "T50/50/5");
            Assert.Equal("T50/50/5", p.Rails.CarProfile);
            var sheet = CalculationSheet.Build(p, TestData.Context(), Inputs());
            Assert.True(sheet.GuideRailsAvailable);
            Assert.False(sheet.GuideRails.Car.Ok);
            Assert.Contains(sheet.NotOk, x => x.StartsWith("Guide rails, car rails, "));
            var notes = DesignNotes.DesignNotes.Build(p, TestData.Context(), Inputs());
            var lines = notes.Warnings.Where(w => w.StartsWith("Car rails, ")).ToList();
            Assert.NotEmpty(lines);
            Assert.Equal(sheet.GuideRails.Car.Failures().Count(), lines.Count);   // one line per failed item
            Assert.All(lines, l => Assert.True(l.Contains("T70/65/9") || l.Contains("T89/62/16") || l.Contains("T90/75/16") || l.Contains("bracket spacing of at most") || l.Contains("No profile"), l));
            Assert.All(lines, l => Assert.DoesNotContain("preset", l.ToLowerInvariant()));
            // the profile named by a note really passes at the chosen spacing
            var g = sheet.GuideRails.Inputs;
            var needed = Elevator.Core.GuideRails.GuideRailRecommendation.SmallestPassing(g, true, sheet.GuideRails.Car.SpacingMm);
            if (needed != null)
                Assert.True(Elevator.Core.GuideRails.GuideRailCheckEngine.Line(g, true, needed, sheet.GuideRails.Car.SpacingMm).Ok);
        }
    }
}
