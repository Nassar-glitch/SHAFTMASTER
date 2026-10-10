using System;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
using Xunit;

namespace Elevator.Core.Tests
{
    // DESIGN-FLOW "Doors" (D1): automatic door catalog, selection, validation, front zone, landing geometry, clash and EN 81-20
    // checks, lock / follow / reset, and the guarantee that Manual doors and existing projects are exactly as before.
    public class DoorTests
    {
        static readonly DesignContext Ctx = TestData.Context();
        static readonly string[] Types = { DoorTypes.Automatic2S, DoorTypes.Automatic2C };
        static readonly double[] Sizes = { 700, 800, 900, 1000, 1200 };

        static DoorCatalog Catalog() { return DoorCatalog.Default(); }

        static DoorInput Door(string type, DoorSettings s = null, DoorCatalog c = null)
        {
            return new DoorInput { Type = type, Settings = s ?? new DoorSettings(), Catalog = c ?? Catalog() };
        }

        // The reference drawings' shaft and car (1720 wide shaft, 1350 car, DBG 1450, guide axis 1160 from the front wall).
        static ElevatorParameters Reference(double front)
        {
            return new ElevatorParameters
            {
                SW = 1720, SD = 2800, CarDbg = 1450, CarWidth = 1350, CwDbg = 1450, CwWidth = 1410, CarFront = front,
                CarRear = 2600, CarRow = 1160, CwRow = 2475, CwFront = 2400, CwRear = 2550, CwDepth = 150
            };
        }

        static ElevatorParameters Plan(double sw, double sd, DoorInput door)
        {
            return DesignRules.Calculate(sw, sd, Ctx.Equipment, Ctx.CounterweightDepthMm, door);
        }

        static ElevatorProject NewProject()
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            return ProjectSession.NewProject(Path.Combine(TestData.Bin, "reviewed-example.elevator.json"), lib, Ctx);
        }

        // ---------- the reference drawings (800 mm), exactly ----------
        [Fact]
        public void Reference_2S_800_reproduces_the_drawing()
        {
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S), Reference(245), 800, false);
            Assert.True(l.Fits, DoorFlow.FitMessage(l));
            Assert.Equal(130, l.CarZoneMm);
            Assert.Equal(25, l.SillGapMm);
            Assert.Equal(90, l.LandingZoneMm);
            Assert.Equal(245, l.FrontZoneMm);
            Assert.Equal(200, l.FixedReturnMm);
            Assert.Equal(350, l.StackReturnMm);
            Assert.Equal(200, l.ActualFixedReturnMm);
            Assert.Equal(350, l.ActualStackReturnMm);
            Assert.Equal(75, l.OpeningOffsetMm);
            Assert.Equal(DoorTypes.Right, l.StackingSide);
            // From the shaft left wall: opening 385 .. 1185 (385 and 535 to the walls in the drawing).
            Assert.Equal(385, l.OpeningLeftX);
            Assert.Equal(1185, l.OpeningRightX);
            Assert.Equal(355, l.LandingLeftX);
            Assert.Equal(1610, l.LandingRightX);
            Assert.Equal(407.5, l.PanelWidthMm);
            Assert.Equal(115, l.CarToWallMm);
            Assert.Empty(l.Problems);
        }

        [Fact]
        public void Reference_2C_800_reproduces_the_drawing()
        {
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2C), Reference(220), 800, false);
            Assert.True(l.Fits, DoorFlow.FitMessage(l));
            Assert.Equal(110, l.CarZoneMm);
            Assert.Equal(30, l.SillGapMm);
            Assert.Equal(80, l.LandingZoneMm);
            Assert.Equal(220, l.FrontZoneMm);
            Assert.Equal(275, l.ActualFixedReturnMm);
            Assert.Equal(275, l.ActualStackReturnMm);
            Assert.Equal(0, l.OpeningOffsetMm);
            // Centred: 460 either side of the opening to the walls; the landing frame is 35 mm from each wall.
            Assert.Equal(460, l.OpeningLeftX);
            Assert.Equal(1260, l.OpeningRightX);
            Assert.Equal(35, l.LandingLeftX);
            Assert.Equal(1685, l.LandingRightX);
            Assert.Equal(399, l.PanelWidthMm);
            Assert.Equal(110, l.CarToWallMm);
        }

        [Fact]
        public void Handing_mirrors_the_2S_layout()
        {
            var auto = DoorRules.Layout(Door(DoorTypes.Automatic2S), Reference(245), 800, false);
            Assert.Equal(DoorTypes.Right, auto.StackingSide);
            var right = DoorSettings_(DoorTypes.Right);
            var rl = DoorRules.Layout(Door(DoorTypes.Automatic2S, right), Reference(245), 800, false);
            Assert.Equal(auto.OpeningLeftX, rl.OpeningLeftX);
            var left = DoorRules.Layout(Door(DoorTypes.Automatic2S, DoorSettings_(DoorTypes.Left)), Reference(245), 800, false);
            Assert.Equal(DoorTypes.Left, left.StackingSide);
            // Mirror image about the shaft centre.
            Assert.Equal(1720 - auto.OpeningRightX, left.OpeningLeftX, 6);
            Assert.Equal(1720 - auto.OpeningLeftX, left.OpeningRightX, 6);
            Assert.Equal(1720 - auto.LandingRightX, left.LandingLeftX, 6);
            Assert.Equal(1720 - auto.LandingLeftX, left.LandingRightX, 6);
            Assert.Equal(-auto.CentreShiftMm, left.CentreShiftMm, 6);
        }

        static DoorSettings DoorSettings_(string handing) { return new DoorSettings { Handing = handing }; }

        [Fact]
        public void Automatic_handing_follows_the_governor_side_of_the_catalog()
        {
            var c = Catalog();
            c.GovernorSide = DoorTypes.Right;
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S, null, c), Reference(245), 800, false);
            Assert.Equal(DoorTypes.Left, l.StackingSide);
        }

        // ---------- every size x type ----------
        static double NeededCar(string type, double o)
        {
            // Fixed 200 + opening + stacking o/2 - 50 (2S), or the opening and two returns o/4 + 75 (2C).
            return type == DoorTypes.Automatic2S ? 200 + o + (o / 2 - 50) : o + 2 * (o / 4 + 75);
        }

        static double NeededLanding(string type, double o)
        {
            double pocket = o / 2 + 25, wc = 35;
            if (type == DoorTypes.Automatic2C)
                return o + 2 * (pocket + wc);
            double off = (o / 2 - 50 - 200) / 2;
            return o + 2 * Math.Max(Math.Max(pocket, o / 2 + 7.5) + wc - off, 30 + wc + off);
        }

        [Theory]
        [InlineData(1500, 2600)]
        [InlineData(1720, 2800)]
        [InlineData(1900, 2800)]
        [InlineData(2200, 3000)]
        public void Every_size_and_type_matches_the_closed_form(double sw, double sd)
        {
            foreach (string type in Types)
            {
                var car = Plan(sw, sd, Door(type));
                double cw = car.CarWidth;
                foreach (double o in Sizes)
                {
                    var door = Door(type, new DoorSettings { OpeningMm = o });
                    var p = Plan(sw, sd, door);
                    Assert.Equal(o, p.DoorWidth);
                    var l = DoorRules.Select(door, p);
                    bool carFits = NeededCar(type, o) <= cw + 1e-7, landingFits = NeededLanding(type, o) <= sw + 1e-7;
                    Assert.Equal(carFits && landingFits, l.Fits);
                    Assert.Equal(!carFits, l.Problems.Any(x => x.Code == "car-width"));
                    Assert.Equal(!landingFits, l.Problems.Any(x => x.Code == "landing-width"));
                    if (!carFits)
                    {
                        var prob = l.Problems.First(x => x.Code == "car-width");
                        Assert.Equal(NeededCar(type, o), prob.NeededCarWidthMm.Value, 6);
                        Assert.Equal(sw + NeededCar(type, o) - cw, prob.NeededShaftWidthMm.Value, 6);
                    }
                }
                // Auto: the largest standard size that fits, else the smallest with its problems.
                var best = Sizes.OrderByDescending(o => o).FirstOrDefault(o => NeededCar(type, o) <= cw + 1e-7 && NeededLanding(type, o) <= sw + 1e-7);
                var auto = DoorRules.Select(Door(type), Plan(sw, sd, Door(type)));
                Assert.True(auto.AutoSelected);
                if (best > 0)
                {
                    Assert.True(auto.Fits);
                    Assert.Equal(best, auto.OpeningMm);
                }
                else
                {
                    Assert.False(auto.Fits);
                    Assert.Equal(700, auto.OpeningMm);
                }
            }
        }

        [Fact]
        public void On_the_reference_shaft_with_the_current_recipe_the_largest_fitting_size_is_700()
        {
            // The equipment profile (150 mm rail allowance per side) gives a 1320 mm car in 1720: 800 needs a 1350 mm car.
            foreach (string type in Types)
            {
                var door = Door(type);
                var p = Plan(1720, 2800, door);
                Assert.Equal(1320, p.CarWidth);
                Assert.Equal(700, p.DoorWidth);
                var l = DoorRules.Select(door, p);
                Assert.True(l.Fits);
                var user = DoorRules.Select(Door(type, new DoorSettings { OpeningMm = 800 }), p);
                Assert.False(user.Fits);
                Assert.Equal(1350, user.NeededCarWidthMm.Value, 6);
                Assert.Equal(1750, user.NeededShaftWidthMm.Value, 6);
                Assert.StartsWith("does not fit: needs car width 1350 mm", DoorFlow.FitMessage(user));
            }
        }

        [Fact]
        public void The_800_reference_fits_when_the_shaft_is_wide_enough()
        {
            foreach (string type in Types)
            {
                var p = Plan(1750, 2800, Door(type));
                Assert.Equal(1350, p.CarWidth);
                Assert.Equal(800, p.DoorWidth);
            }
        }

        [Fact]
        public void A_non_standard_size_is_reported_with_the_standard_ones()
        {
            var door = Door(DoorTypes.Automatic2S, new DoorSettings { OpeningMm = 850 });
            var l = DoorRules.Select(door, Plan(1900, 2800, door));
            Assert.False(l.Fits);
            Assert.Equal("opening-size", l.Problems[0].Code);
            Assert.Contains("700, 800, 900, 1000, 1200", l.Problems[0].Message);
        }

        // ---------- the front zone sets the car depth ----------
        [Fact]
        public void Front_zone_sets_car_front_and_car_depth()
        {
            var manual = Plan(1900, 2800, null);
            var s2 = Plan(1900, 2800, Door(DoorTypes.Automatic2S));
            var c2 = Plan(1900, 2800, Door(DoorTypes.Automatic2C));
            Assert.Equal(125, manual.CarFront);
            Assert.Equal(245, s2.CarFront);
            Assert.Equal(220, c2.CarFront);
            Assert.Equal(manual.CarDepth - 120, s2.CarDepth, 6);
            Assert.Equal(manual.CarDepth - 95, c2.CarDepth, 6);
            Assert.Equal(manual.CarWidth, s2.CarWidth);
            // Everything behind the front moves with the car depth.
            Assert.Equal(s2.CarFront + s2.CarDepth, s2.CarRear, 6);
            Assert.Equal(s2.CarDepth / 2 + Ctx.Equipment.TzAdditionMm, s2.TZ, 6);
        }

        [Fact]
        public void A_locked_front_zone_value_changes_the_car_depth()
        {
            var s = new DoorSettings { CarZoneMm = 150 };
            var p = Plan(1900, 2800, Door(DoorTypes.Automatic2S, s));
            Assert.Equal(265, p.CarFront);
        }

        // ---------- Manual doors and existing projects are exactly as before ----------
        [Fact]
        public void Manual_door_recipe_is_unchanged()
        {
            foreach (double sw in new double[] { 1500, 1720, 1900, 2200 })
            {
                var a = DesignRules.Calculate(sw, 2800, Ctx.Equipment, Ctx.CounterweightDepthMm);
                var b = DesignRules.Calculate(sw, 2800, Ctx.Equipment, Ctx.CounterweightDepthMm, null);
                Assert.Equal(CoreJson.Create().Serialize(a), CoreJson.Create().Serialize(b));
                Assert.Equal(Ctx.Equipment.CarFrontAllowanceMm, a.CarFront);
            }
            Assert.Null(DoorInput.From(NewProject(), null));
        }

        [Fact]
        public void A_manual_project_stores_no_door_data_and_keeps_its_json()
        {
            var p = NewProject();
            string before = JsonMembers.Sort(ProjectJson.Serialize(p));
            Assert.False(p.Extensions.ContainsKey("doors"));
            Assert.Null(p.Doors.Type);
            // Choosing Automatic and going back to Manual restores the same project bytes.
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            Assert.Equal(DoorTypes.Automatic2S, p.Doors.Type);
            DoorFlow.SetType(p, DoorTypes.Manual);
            Assert.Equal(before, JsonMembers.Sort(ProjectJson.Serialize(p)));
            // The design of a Manual project is the plain recipe.
            var r = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan);
            Assert.Equal(Ctx.Equipment.CarFrontAllowanceMm, r.Plan.CarFront);
        }

        // ---------- clash checks ----------
        [Fact]
        public void Car_door_panels_clash_with_a_guide_rail_when_the_rail_is_in_the_front_zone()
        {
            // Stacking left puts the parked panels over the left rail and bracket; the rail row is moved into the door zone.
            var plan = Reference(245);
            plan.CarRow = 200;
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S, DoorSettings_(DoorTypes.Left)), plan, 800, false);
            Assert.False(l.Fits);
            Assert.Contains(l.Problems, p => p.Code == "clash" && p.Message.Contains("car guide rail"));
            // Behind the zone there is no clash.
            Assert.True(DoorRules.Layout(Door(DoorTypes.Automatic2S, DoorSettings_(DoorTypes.Left)), Reference(245), 800, false).Fits);
        }

        [Fact]
        public void Door_clashes_with_the_counterweight_and_the_governor()
        {
            var plan = Reference(245);
            plan.CwFront = 100;
            plan.CwRear = 400;
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S), plan, 800, false);
            Assert.Contains(l.Problems, p => p.Code == "clash" && p.Message.Contains("counterweight"));
            // Governor on the right, stacking right, opening centred: the parked panels reach it.
            var c = Catalog();
            c.GovernorSide = DoorTypes.Right;
            c.Envelopes.GovernorFromGuideAxisMm = -200;
            var gp = Reference(245);
            gp.CarRow = 300;
            var s = new DoorSettings { Handing = DoorTypes.Right, OpeningOffsetMm = 0 };
            var g = DoorRules.Layout(Door(DoorTypes.Automatic2S, s, c), gp, 800, false);
            Assert.Contains(g.Problems, p => p.Code == "clash" && p.Message.Contains("speed governor"));
        }

        [Fact]
        public void Counterweight_rails_are_checked()
        {
            var plan = Reference(245);
            plan.CwRow = 150;
            plan.CwFront = 2400;
            plan.CwRear = 2550;
            plan.CwDbg = 1000;
            // Stacking left over a rail at x < c - 500 = 360 .. 0: the panels start at 535 - 407.5 = 127.5 < 360.
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S, DoorSettings_(DoorTypes.Left)), plan, 800, false);
            Assert.Contains(l.Problems, p => p.Code == "clash" && p.Message.Contains("counterweight guide rail"));
        }

        // ---------- EN 81-20 ----------
        [Fact]
        public void Sill_gap_and_front_distance_limits()
        {
            var ok = DoorRules.Layout(Door(DoorTypes.Automatic2S), Reference(245), 800, false);
            Assert.True(ok.Fits);
            // 35 mm is allowed, 36 is not.
            var edge = DoorRules.Layout(Door(DoorTypes.Automatic2S, new DoorSettings { SillGapMm = 35 }), Reference(255), 800, false);
            Assert.DoesNotContain(edge.Problems, p => p.Code == "sill-gap");
            var gap = DoorRules.Layout(Door(DoorTypes.Automatic2S, new DoorSettings { SillGapMm = 36 }), Reference(256), 800, false);
            Assert.Contains(gap.Problems, p => p.Code == "sill-gap" && p.Message.Contains("35"));
            // Landing zone + sill gap above 150 mm.
            var far = DoorRules.Layout(Door(DoorTypes.Automatic2S, new DoorSettings { LandingZoneMm = 126 }), Reference(281), 800, false);
            Assert.Contains(far.Problems, p => p.Code == "car-to-wall" && p.Message.Contains("151"));
            var near = DoorRules.Layout(Door(DoorTypes.Automatic2S, new DoorSettings { LandingZoneMm = 125 }), Reference(280), 800, false);
            Assert.DoesNotContain(near.Problems, p => p.Code == "car-to-wall");
        }

        // ---------- lock / follow / reset ----------
        [Fact]
        public void Opening_follows_the_shaft_until_locked_and_reset_unlocks()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            p.Shaft.WidthMm = 1750;
            Assert.Equal(800, Opening(p));
            p.Shaft.WidthMm = 1900;
            Assert.Equal(900, Opening(p)); // 1.5 * 900 + 150 = 1500 <= car 1500
            // Locked 700 stays when the shaft changes.
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 700);
            Assert.True(DoorFlow.IsLocked(p, DoorSettings.OpeningKey));
            p.Shaft.WidthMm = 2200;
            Assert.Equal(700, Opening(p));
            Assert.Contains(DoorSettings.OpeningKey, p.UserEditedFields);
            // Reset: the opening follows again.
            DoorFlow.Reset(p, DoorSettings.OpeningKey);
            Assert.False(DoorFlow.IsLocked(p, DoorSettings.OpeningKey));
            Assert.Equal(1000, Opening(p));
            Assert.False(p.Extensions.ContainsKey("doors"));
        }

        static double Opening(ElevatorProject p)
        {
            var r = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan);
            return r.Plan.DoorWidth;
        }

        [Fact]
        public void Locked_clearance_replaces_the_preset_and_follows_nothing_until_reset()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            p.Shaft.WidthMm = 1900;
            var before = Layout(p);
            Assert.Equal(200, before.FixedReturnMm);
            DoorFlow.SetNumber(p, DoorSettings.FixedReturnKey, 260);
            var after = Layout(p);
            Assert.Equal(260, after.FixedReturnMm);
            // The opening offset follows the returns while it is not locked: (350 + ... - 260) / 2.
            Assert.Equal((after.StackReturnMm - 260) / 2, after.OpeningOffsetMm, 6);
            DoorFlow.SetNumber(p, DoorSettings.OffsetKey, 40);
            Assert.Equal(40, Layout(p).OpeningOffsetMm);
            DoorFlow.Reset(p, DoorSettings.FixedReturnKey);
            DoorFlow.Reset(p, DoorSettings.OffsetKey);
            Assert.Equal(200, Layout(p).FixedReturnMm);
            Assert.Equal(before.OpeningOffsetMm, Layout(p).OpeningOffsetMm, 6);
            Assert.Empty(DoorFlow.LockedKeys(p));
        }

        static DoorLayout Layout(ElevatorProject p)
        {
            var r = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan);
            return DoorRules.Evaluate(p, null, r.Plan);
        }

        [Fact]
        public void Handing_is_locked_and_reset_like_any_other_value()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            p.Shaft.WidthMm = 1900;
            Assert.Equal(DoorTypes.Right, Layout(p).StackingSide);
            DoorFlow.SetHanding(p, DoorTypes.Left);
            Assert.True(DoorFlow.IsLocked(p, DoorSettings.HandingKey));
            Assert.Equal(DoorTypes.Left, Layout(p).StackingSide);
            DoorFlow.SetHanding(p, DoorTypes.Auto);
            Assert.False(DoorFlow.IsLocked(p, DoorSettings.HandingKey));
            Assert.Equal(DoorTypes.Right, Layout(p).StackingSide);
        }

        [Fact]
        public void Door_settings_round_trip_through_the_project_file()
        {
            var p = NewProject();
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 900);
            DoorFlow.SetHanding(p, DoorTypes.Left);
            DoorFlow.SetNumber(p, DoorSettings.SillGapKey, 30);
            var back = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(DoorTypes.Automatic2S, back.Doors.Type);
            var s = DoorSettings.Read(back);
            Assert.Equal(900, s.OpeningMm);
            Assert.Equal(DoorTypes.Left, s.Handing);
            Assert.Equal(30, s.SillGapMm);
            Assert.Null(s.CarZoneMm);
            Assert.Equal(JsonMembers.Sort(ProjectJson.Serialize(p)), JsonMembers.Sort(ProjectJson.Serialize(back)));
        }

        // ---------- the catalog ----------
        [Fact]
        public void The_shipped_presets_file_holds_the_default_catalog()
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            Assert.NotNull(lib.Doors);
            Assert.Empty(lib.Doors.Validate());
            var json = CoreJson.Create();
            Assert.Equal(JsonMembers.Sort(json.Serialize(DoorCatalog.Default())), JsonMembers.Sort(json.Serialize(lib.Doors)));
            Assert.Same(lib.Doors, lib.DoorsOrDefault());
        }

        [Fact]
        public void A_library_without_doors_uses_the_default_and_a_broken_catalog_is_rejected()
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            lib.Doors = null;
            Assert.Empty(lib.Validate());
            Assert.NotNull(lib.DoorsOrDefault());
            lib.Doors = DoorCatalog.Default();
            lib.Doors.Automatic2S.Sizes.RemoveAll(r => r.OpeningMm == 900);
            Assert.Contains(lib.Validate(), m => m.Contains("Automatic2S") && m.Contains("900"));
        }

        [Fact]
        public void Catalog_edit_changes_the_presets()
        {
            var c = Catalog();
            c.Automatic2S.CarZoneMm = 140;
            var p = Plan(1900, 2800, Door(DoorTypes.Automatic2S, null, c));
            Assert.Equal(255, p.CarFront);
        }

        // ---------- the design engine and the preset flow ----------
        [Fact]
        public void Design_engine_and_ff_geometry_follow_the_door()
        {
            var p = NewProject();
            p.Shaft.WidthMm = 1900;
            p.Shaft.DepthMm = 2800;
            var manual = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan).Plan;
            DoorFlow.SetType(p, DoorTypes.Automatic2S);
            var auto = new ElevatorDesignEngine().Calculate(p, Ctx, DrawingViews.Plan).Plan;
            Assert.Equal(manual.CarDepth - 120, auto.CarDepth, 6);
            var ff = new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, Ctx);
            Assert.Equal(auto.CarDepth, ff.InternalDepthMm.Value, 6);
        }

        [Fact]
        public void Preset_flow_wires_the_library_catalog_into_the_context()
        {
            var ctx = new DesignContext { Equipment = Ctx.Equipment, CounterweightDepthMm = Ctx.CounterweightDepthMm };
            var flow = PresetFlow.Load(Path.Combine(TestData.Bin, "presets.json"), ctx);
            Assert.Same(flow.Library.Doors, ctx.Doors);
        }

        [Fact]
        public void Design_note_names_the_needed_widths()
        {
            var door = Door(DoorTypes.Automatic2C, new DoorSettings { OpeningMm = 1200 });
            var plan = Plan(1720, 2800, door);
            var l = DoorRules.Select(door, plan);
            Assert.False(l.Fits);
            string note = DoorFlow.DesignNote(l);
            Assert.Contains("Automatic 2C", note);
            Assert.Contains("1200 mm clear opening does not fit", note);
            Assert.Contains("car width of at least 2120 mm", note);
            Assert.Contains("shaft width of at least 2520 mm", note);
            Assert.Null(DoorFlow.DesignNote(DoorRules.Select(Door(DoorTypes.Automatic2C), Plan(1900, 2800, Door(DoorTypes.Automatic2C)))));
        }
    }
}
