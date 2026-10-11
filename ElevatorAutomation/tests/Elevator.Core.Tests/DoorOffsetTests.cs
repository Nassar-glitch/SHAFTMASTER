using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Xunit;

namespace Elevator.Core.Tests
{
    // DESIGN-FLOW "Doors Offset" (D4): the distance from the shaft centre line to the opening centre line and its side, Left or Right as
    // seen in the plan drawing; defaults, the Left / Right mapping for both handings, the allowed ranges, a value outside them (kept,
    // reported, drawn at the nearest allowed position), the narrow-shaft shift and the conversion of the opening offset of old files.
    public class DoorOffsetTests
    {
        static readonly DesignContext Ctx = TestData.Context();

        static DoorInput Door(string type, DoorSettings s = null)
        {
            return new DoorInput { Type = type, Settings = s ?? new DoorSettings(), Catalog = DoorCatalog.Default() };
        }

        static DoorSettings Offset(double mm, string side, string handing = null)
        {
            return new DoorSettings { OffsetMm = mm, OffsetSide = side, Handing = handing };
        }

        // The reference drawings' shaft and car: 1720 wide shaft, 1350 car, so the car side wall is 185 mm inside the shaft wall.
        static ElevatorParameters Reference(double front)
        {
            return new ElevatorParameters
            {
                SW = 1720, SD = 2800, CarDbg = 1450, CarWidth = 1350, CwDbg = 1450, CwWidth = 1410, CarFront = front,
                CarRear = 2600, CarRow = 1160, CwRow = 2475, CwFront = 2400, CwRear = 2550, CwDepth = 150
            };
        }

        static DoorLayout Place(DoorInput door, ElevatorParameters plan, double opening, out DoorOffset offset)
        {
            var l = DoorRules.Layout(door, plan, opening, false);
            offset = DoorOffset.From(door, l, plan);
            return l;
        }

        // ---------- defaults ----------
        [Fact]
        public void Default_2S_is_the_preferred_offset_towards_the_fixed_side_as_Left_or_Right()
        {
            DoorOffset o;
            var right = Place(Door(DoorTypes.Automatic2S), Reference(245), 800, out o);
            // Stack on the right (opposite the governor): the fixed side is the left, 75 mm at 800.
            Assert.Equal(-75, right.CentreShiftMm, 6);
            Assert.False(o.Locked);
            Assert.False(o.OutOfRange);
            Assert.Equal(-75, o.PlacedShiftMm, 6);
            Assert.Equal(DoorTypes.Left, DoorOffset.SideOf(o.PlacedShiftMm, DoorTypes.Left));
            // Stack on the left: the same preset reads 75 Right.
            DoorOffset ol;
            var left = Place(Door(DoorTypes.Automatic2S, new DoorSettings { Handing = DoorTypes.Left }), Reference(245), 800, out ol);
            Assert.Equal(75, left.CentreShiftMm, 6);
            Assert.Equal(DoorTypes.Right, DoorOffset.SideOf(ol.PlacedShiftMm, DoorTypes.Left));
        }

        [Fact]
        public void Default_2C_is_centred()
        {
            DoorOffset o;
            var l = Place(Door(DoorTypes.Automatic2C), Reference(220), 800, out o);
            Assert.Equal(0, l.CentreShiftMm, 6);
            Assert.Equal(460, l.OpeningLeftX, 6);
            Assert.Equal(1260, l.OpeningRightX, 6);
            Assert.False(o.Locked);
            Assert.False(o.OutOfRange);
        }

        // ---------- the Left / Right mapping ----------
        [Theory]
        [InlineData(DoorTypes.Left, 50, DoorTypes.Right, 50, -50)]    // stack right, 50 Left = towards the fixed side
        [InlineData(DoorTypes.Left, 50, DoorTypes.Left, -50, -50)]    // stack left, 50 Left = towards the stacking side
        [InlineData(DoorTypes.Right, 5, DoorTypes.Right, -5, 5)]      // stack right, 5 Right = towards the stacking side
        [InlineData(DoorTypes.Right, 100, DoorTypes.Left, 100, 100)]  // stack left, 100 Right = towards the fixed side
        public void Left_and_Right_are_as_seen_in_the_plan_for_both_handings(string side, double mm, string handing, double towardsFixed, double shift)
        {
            var s = Offset(mm, side, handing);
            DoorOffset o;
            // A wide shaft, so that every value is inside the range.
            var plan = Reference(245);
            plan.SW = 2200;
            plan.CarWidth = 1830;
            plan.CarDbg = 1930;
            var l = Place(Door(DoorTypes.Automatic2S, s), plan, 800, out o);
            Assert.Equal(shift, l.CentreShiftMm, 6);
            Assert.Equal(towardsFixed, l.OpeningOffsetMm, 6);
            Assert.Equal(towardsFixed, l.PreferredOffsetMm, 6);
            Assert.False(o.OutOfRange);
            Assert.Equal(shift, o.RequestedShiftMm, 6);
            Assert.Equal(1100 + shift - 400, l.OpeningLeftX, 6);
            Assert.Equal(1100 + shift + 400, l.OpeningRightX, 6);
        }

        [Fact]
        public void The_2C_offset_moves_the_opening_and_both_boxes()
        {
            DoorOffset o;
            var l = Place(Door(DoorTypes.Automatic2C, Offset(8, DoorTypes.Right)), Reference(220), 800, out o);
            Assert.False(o.OutOfRange);
            Assert.Equal(8, l.CentreShiftMm, 6);
            Assert.Equal(468, l.OpeningLeftX, 6);
            Assert.Equal(1268, l.OpeningRightX, 6);
            Assert.Equal(468 - 425, l.LandingLeftX, 6);
            Assert.Equal(1268 + 425, l.LandingRightX, 6);
            Assert.Equal(468 - 185, l.ActualFixedReturnMm, 6);
            Assert.Equal(1720 - 185 - 1268, l.ActualStackReturnMm, 6);
            var left = Place(Door(DoorTypes.Automatic2C, Offset(8, DoorTypes.Left)), Reference(220), 800, out o);
            Assert.Equal(-8, left.CentreShiftMm, 6);
            Assert.Equal(452, left.OpeningLeftX, 6);
        }

        // ---------- the allowed range ----------
        [Fact]
        public void Range_2S_is_the_existing_placement_limits()
        {
            DoorOffset o;
            Place(Door(DoorTypes.Automatic2S), Reference(245), 800, out o);
            // Left: the fixed-side car return at its minimum (0), the opening edge 185 from the wall: 185 + 400 - 860 = -275.
            // Right: the stacking pocket 425 and the wall clearance 25: 1720 - 25 - 425 - 800 = 470, centre 870, +10.
            Assert.Equal(-275, o.MinShiftMm, 6);
            Assert.Equal(10, o.MaxShiftMm, 6);
            Assert.Equal("Allowed: 275 Left … 10 Right", o.AllowedText);
            Assert.Contains("Left car return would fall below its minimum of 0 mm", o.LeftReason);
            Assert.Contains("Right landing frame would come closer than 25 mm to the Right wall", o.RightReason);
            // The same shaft with the stack on the left mirrors the range.
            DoorOffset m;
            Place(Door(DoorTypes.Automatic2S, new DoorSettings { Handing = DoorTypes.Left }), Reference(245), 800, out m);
            Assert.Equal("Allowed: 10 Left … 275 Right", m.AllowedText);
            Assert.Contains("Left landing frame would come closer", m.LeftReason);
            Assert.Contains("Right car return would fall below", m.RightReason);
        }

        [Fact]
        public void Range_2S_follows_the_minimums()
        {
            var s = new DoorSettings { MinFixedReturnMm = 100, MinStackReturnMm = 300, LandingWallClearanceMm = 40 };
            DoorOffset o;
            Place(Door(DoorTypes.Automatic2S, s), Reference(245), 800, out o);
            // Left: 185 + 100 = 285 -> 285 + 400 - 860 = -175. Right: min(1720 - 40 - 425 - 800, 1720 - 185 - 300 - 800) = 435, 435 + 400 - 860 = -25.
            Assert.Equal(-175, o.MinShiftMm, 6);
            Assert.Equal(-25, o.MaxShiftMm, 6);
            Assert.Equal("Allowed: 175 Left … 25 Left", o.AllowedText);
            Assert.Contains("Right car return would fall below its minimum of 300 mm", o.RightReason);
        }

        [Fact]
        public void Range_2C_keeps_both_boxes_inside_with_the_wall_clearance_and_both_returns()
        {
            DoorOffset o;
            Place(Door(DoorTypes.Automatic2C), Reference(220), 800, out o);
            // Box 425 + clearance 25 = 450 from each wall: opening edge 450 .. 470, centre 860 +- 10.
            Assert.Equal(-10, o.MinShiftMm, 6);
            Assert.Equal(10, o.MaxShiftMm, 6);
            Assert.Equal("Allowed: 10 Left … 10 Right", o.AllowedText);
            Assert.Contains("Left landing door box would come closer than 25 mm to the Left wall", o.LeftReason);
            // A wide shaft: the car returns bind when their minimum is raised.
            var plan = Reference(220);
            plan.SW = 2400;
            plan.CarWidth = 2030;
            DoorOffset w;
            Place(Door(DoorTypes.Automatic2C, new DoorSettings { MinFixedReturnMm = 600 }), plan, 800, out w);
            // Opening edge >= 185 + 600 = 785 from the wall; centred it is 800: 15 mm each way.
            Assert.Equal(-15, w.MinShiftMm, 6);
            Assert.Equal(15, w.MaxShiftMm, 6);
            Assert.Contains("car return would fall below its minimum of 600 mm", w.LeftReason);
        }

        // ---------- a value outside the range ----------
        [Fact]
        public void A_value_beyond_the_range_is_kept_reported_and_drawn_at_the_nearest_position()
        {
            DoorOffset o;
            var l = Place(Door(DoorTypes.Automatic2S, Offset(300, DoorTypes.Left)), Reference(245), 800, out o);
            Assert.True(o.Locked);
            Assert.True(o.OutOfRange);
            Assert.Equal("out of range: max 275 Left", o.Message);
            Assert.Equal(-300, o.RequestedShiftMm, 6);
            Assert.Equal(-275, o.PlacedShiftMm, 6);
            Assert.Equal(185, l.OpeningLeftX, 6);
            Assert.Equal(185, l.CarSideGapMm + l.ActualFixedReturnMm, 6);
            Assert.Equal(0, l.ActualFixedReturnMm, 6);
            Assert.True(l.Fits);
            Assert.Equal(
                "Automatic 2S (side opening) door: Doors Offset 300 Left is out of range; the maximum is 275 Left "
                + "(the Left car return would fall below its minimum of 0 mm). The drawings and the STEP model use 275 Left.",
                o.DesignNote);
            Assert.Equal("Doors Offset: 300 Left (max 275 Left)", o.Warning);
        }

        [Fact]
        public void A_value_beyond_the_right_limit_names_the_right_limit()
        {
            DoorOffset o;
            var l = Place(Door(DoorTypes.Automatic2S, Offset(50, DoorTypes.Right)), Reference(245), 800, out o);
            Assert.True(o.OutOfRange);
            Assert.Equal("out of range: max 10 Right", o.Message);
            Assert.Equal(10, o.PlacedShiftMm, 6);
            Assert.Equal(470, l.OpeningLeftX, 6);
            Assert.Equal(1720 - 25, l.LandingRightX, 6);
            DoorOffset c;
            var cl = Place(Door(DoorTypes.Automatic2C, Offset(100, DoorTypes.Right)), Reference(220), 800, out c);
            Assert.Equal("out of range: max 10 Right", c.Message);
            Assert.Equal(10, cl.CentreShiftMm, 6);
            Assert.Equal(1720 - 25, cl.LandingRightX, 6);
        }

        [Fact]
        public void A_value_exactly_at_the_limit_is_in_range()
        {
            DoorOffset o;
            Place(Door(DoorTypes.Automatic2S, Offset(275, DoorTypes.Left)), Reference(245), 800, out o);
            Assert.False(o.OutOfRange);
            Assert.Null(o.Message);
            Place(Door(DoorTypes.Automatic2S, Offset(10, DoorTypes.Right)), Reference(245), 800, out o);
            Assert.False(o.OutOfRange);
        }

        [Fact]
        public void A_range_that_lies_on_one_side_gives_a_minimum()
        {
            // 700 on a 1320 shaft with the car 920 wide: the opening has 20 mm of play, 110 .. 90 Left.
            var door = Door(DoorTypes.Automatic2S, Offset(50, DoorTypes.Left));
            var plan = Reference(245);
            plan.SW = 1320;
            plan.CarWidth = 920;
            DoorOffset o;
            var l = Place(door, plan, 700, out o);
            Assert.Equal(-110, o.MinShiftMm, 6);
            Assert.Equal(-90, o.MaxShiftMm, 6);
            Assert.True(o.OutOfRange);
            Assert.Equal("out of range: min 90 Left", o.Message);
            Assert.Equal(-90, l.CentreShiftMm, 6);
            Assert.Equal("Allowed: 110 Left … 90 Left", o.AllowedText);
        }

        [Fact]
        public void An_unlocked_default_never_reports_out_of_range()
        {
            // 2S 700 on 1300: the preferred 50 mm does not fit, the opening shifts (the narrow-shaft shift) and nothing is flagged.
            var door = Door(DoorTypes.Automatic2S);
            var plan = DesignRules.Calculate(1300, 2800, Ctx.Equipment, Ctx.CounterweightDepthMm, door);
            var l = DoorRules.Layout(door, plan, 700, true);
            var o = DoorOffset.From(door, l, plan);
            Assert.True(l.Shifted);
            Assert.False(o.Locked);
            Assert.False(o.OutOfRange);
            Assert.True(o.PlacedShiftMm >= o.MinShiftMm - 1e-6 && o.PlacedShiftMm <= o.MaxShiftMm + 1e-6);
            Assert.NotEqual(o.RequestedShiftMm, o.PlacedShiftMm);
        }

        // ---------- files ----------
        static ElevatorProject NewProject()
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            return ProjectSession.NewProject(Path.Combine(TestData.Bin, "reviewed-example.elevator.json"), lib, Ctx);
        }

        static DoorLayout LayoutOf(ElevatorProject p, out DoorOffset offset)
        {
            var plan = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan).Plan;
            var door = DoorInput.From(p, null);
            var l = DoorRules.Select(door, plan);
            offset = DoorOffset.From(door, l, plan);
            return l;
        }

        [Fact]
        public void The_offset_locks_distance_and_side_together_and_resets_together()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 800);
            p.Shaft.WidthMm = 1900;
            DoorFlow.SetOffset(p, 60, DoorTypes.Left);
            Assert.Contains(DoorSettings.OffsetKey, p.UserEditedFields);
            Assert.Contains(DoorSettings.OffsetSideKey, p.UserEditedFields);
            var s = DoorSettings.Read(p);
            Assert.Equal(60, s.OffsetMm);
            Assert.Equal(DoorTypes.Left, s.OffsetSide);
            DoorOffset o;
            var l = LayoutOf(p, out o);
            Assert.Equal(-60, l.CentreShiftMm, 6);
            Assert.True(o.Locked);
            DoorFlow.Reset(p, DoorSettings.OffsetKey);
            Assert.Equal(new[] { DoorSettings.OpeningKey }, DoorFlow.LockedKeys(p).ToArray());
            var s2 = DoorSettings.Read(p);
            Assert.Null(s2.OffsetMm);
            Assert.Null(s2.OffsetSide);
        }

        [Fact]
        public void Offset_keys_round_trip_byte_for_byte()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2C);
            p.Shaft.WidthMm = 1900;
            DoorFlow.SetOffset(p, 55, DoorTypes.Right);
            string json = ProjectJson.Serialize(p);
            Assert.Contains("\"Doors.OffsetMm\"", json);
            Assert.Contains("\"Doors.OffsetSide\"", json);
            var back = ProjectJson.Deserialize(json);
            Assert.Equal(JsonMembers.Sort(json), JsonMembers.Sort(ProjectJson.Serialize(back)));
            var s = DoorSettings.Read(back);
            Assert.Equal(55, s.OffsetMm);
            Assert.Equal(DoorTypes.Right, s.OffsetSide);
        }

        [Theory]
        [InlineData(null, 40, DoorTypes.Left)]            // governor on the left, stack right: fixed side left
        [InlineData(DoorTypes.Right, 40, DoorTypes.Left)]
        [InlineData(DoorTypes.Left, 40, DoorTypes.Right)] // stack left: fixed side right
        public void An_old_file_with_a_locked_opening_offset_gives_the_same_position(string handing, double mm, string side)
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 800);
            p.Shaft.WidthMm = 1900;
            if (handing != null)
                DoorFlow.SetHanding(p, handing);
            // The file as an earlier version wrote it.
            p.Extensions = p.Extensions ?? new Dictionary<string, object>();
            var doors = p.Extensions.ContainsKey("doors") ? (Dictionary<string, object>)p.Extensions["doors"] : new Dictionary<string, object>();
            doors["Doors.OpeningOffsetMm"] = mm;
            p.Extensions["doors"] = doors;
            p.UserEditedFields = p.UserEditedFields ?? new List<string>();
            p.UserEditedFields.Add("Doors.OpeningOffsetMm");
            var old = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            DoorOffset oo;
            var before = LayoutOf(old, out oo);
            Assert.Equal(mm, before.OpeningOffsetMm, 6);
            Assert.True(oo.Locked);
            // The same position as the new field set to the same Left / Right.
            var fresh = ProjectJson.Deserialize(ProjectJson.Serialize(old));
            DoorFlow.SetOffset(fresh, mm, side);
            DoorOffset no;
            var after = LayoutOf(fresh, out no);
            Assert.Equal(before.OpeningLeftX, after.OpeningLeftX, 6);
            Assert.Equal(before.OpeningRightX, after.OpeningRightX, 6);
            Assert.Equal(before.LandingLeftX, after.LandingLeftX, 6);
            Assert.Equal(before.LandingRightX, after.LandingRightX, 6);
            Assert.False(DoorFlow.IsLocked(fresh, "Doors.OpeningOffsetMm"));
            Assert.Null(DoorSettings.Read(fresh).OpeningOffsetMm);
            // An untouched old file keeps its key (byte for byte on save).
            Assert.Equal(JsonMembers.Sort(ProjectJson.Serialize(old)), JsonMembers.Sort(ProjectJson.Serialize(ProjectJson.Deserialize(ProjectJson.Serialize(old)))));
        }

        [Fact]
        public void An_old_opening_offset_does_not_move_a_centre_opening()
        {
            var s = new DoorSettings { OpeningOffsetMm = 40 };
            DoorOffset o;
            var l = Place(Door(DoorTypes.Automatic2C, s), Reference(220), 800, out o);
            Assert.Equal(0, l.CentreShiftMm, 6);
            Assert.Equal(460, l.OpeningLeftX, 6);
        }

        [Fact]
        public void Manual_doors_have_no_offset()
        {
            var plan = Reference(125);
            Assert.Null(DoorOffset.From(null, new DoorLayout(), plan));
            Assert.Null(DoorOffset.From(Door(DoorTypes.Automatic2S), new DoorLayout { Type = DoorTypes.Manual }, plan));
        }
    }
}
