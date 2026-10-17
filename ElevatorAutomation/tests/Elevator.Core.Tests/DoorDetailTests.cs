using System;
using System.IO;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Xunit;

namespace Elevator.Core.Tests
{
    // G2: the presets of the landing door detail (the user's master doors), their lock and follow, and that a project without edits is
    // unchanged.
    public class DoorDetailTests
    {
        static readonly Lazy<PresetLibrary> Lib = new Lazy<PresetLibrary>(() => PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")));
        static PresetLibrary Library { get { return Lib.Value; } }

        static ElevatorProject NewProject()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(Library, TestData.Context(), seed.Counterweight, seed.Rails.BracketSource);
        }

        [Fact]
        public void The_door_panel_follows_the_door_type_until_chosen()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Manual);
            Assert.Equal(DoorDetailFields.VisionWindow, DoorDetailFields.Effective(p, Library).Panel);
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            Assert.Equal(DoorDetailFields.Solid, DoorDetailFields.Effective(p, Library).Panel);
            DoorDetailFields.DoorPanel.Edit(p, DoorDetailFields.Glass);
            DoorFlow.SetType(p, DoorTypes.Manual);
            Assert.Equal(DoorDetailFields.Glass, DoorDetailFields.Effective(p, Library).Panel);
            DoorDetailFields.DoorPanel.Reset(p);
            Assert.Equal(DoorDetailFields.VisionWindow, DoorDetailFields.Effective(p, Library).Panel);
            // a choice of the other type falls back to the type's preset
            DoorDetailFields.DoorPanel.Edit(p, DoorDetailFields.Solid);
            Assert.Equal(DoorDetailFields.VisionWindow, DoorDetailFields.Effective(p, Library).Panel);
        }

        [Fact]
        public void The_hinge_side_is_right_until_chosen()
        {
            var p = NewProject();
            Assert.True(DoorDetailFields.Effective(p, Library).HingeRight);
            DoorDetailFields.HingeSide.Edit(p, DoorDetailFields.Left);
            Assert.False(DoorDetailFields.Effective(p, Library).HingeRight);
        }

        [Fact]
        public void The_structural_opening_is_the_opening_the_frame_and_the_margins()
        {
            var p = NewProject();
            var s = DoorDetailFields.Effective(p, Library);
            Assert.Equal(1100, s.StructuralWidth(800));
            Assert.Equal(2000, s.ClearHeightMm);
            DoorDetailFields.StructuralMargin.Edit(p, 100.0);
            Assert.Equal(1000, DoorDetailFields.Effective(p, Library).StructuralWidth(800));
        }

        [Fact]
        public void A_project_without_edits_has_no_door_detail_extension()
        {
            var p = NewProject();
            var before = ProjectJson.Serialize(p);
            Assert.DoesNotContain("doorDetail", before);
            DoorDetailFields.HingeSide.Edit(p, DoorDetailFields.Left);
            Assert.Contains("doorDetail", ProjectJson.Serialize(p));
            DoorDetailFields.HingeSide.Reset(p);
            Assert.Equal(before, ProjectJson.Serialize(p));
        }
    }
}
