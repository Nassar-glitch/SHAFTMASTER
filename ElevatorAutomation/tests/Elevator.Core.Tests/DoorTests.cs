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
        // Recipe car: the car side wall stands g = 200 inside the shaft wall (150 + 50). Minimums are 0 for the returns and the fixed
        // jamb, 25 frame to wall: the opening goes flush with the car side wall.
        const double G = 200;

        static double NeededCar(double o) { return o; }

        static double NeededShaft(string type, double o, double g = G)
        {
            double pocket = o / 2 + 25, wc = 25;
            if (type == DoorTypes.Automatic2C)
                return o + 2 * Math.Max(g, pocket + wc);
            return g + o + Math.Max(pocket + wc, g);
        }

        [Theory]
        [InlineData(1100, 2600)]
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
                Assert.Equal(sw - 400, cw);
                foreach (double o in Sizes)
                {
                    var door = Door(type, new DoorSettings { OpeningMm = o });
                    var p = Plan(sw, sd, door);
                    Assert.Equal(o, p.DoorWidth);
                    var l = DoorRules.Select(door, p);
                    bool carFits = NeededCar(o) <= cw + 1e-7, landingFits = NeededShaft(type, o) <= sw + 1e-7;
                    Assert.Equal(carFits && landingFits, l.Fits);
                    Assert.Equal(!carFits, l.Problems.Any(x => x.Code == "car-width"));
                    Assert.Equal(!landingFits, l.Problems.Any(x => x.Code == "landing-width"));
                    if (!landingFits)
                    {
                        var prob = l.Problems.First(x => x.Code == "landing-width");
                        Assert.Equal(NeededShaft(type, o), prob.NeededShaftWidthMm.Value, 6);
                        Assert.Equal(cw + NeededShaft(type, o) - sw, prob.NeededCarWidthMm.Value, 6);
                    }
                    if (l.Fits && type == DoorTypes.Automatic2S)
                    {
                        // Whatever the shift, both returns and the landing stay within their minimums.
                        Assert.True(l.ActualFixedReturnMm >= -1e-7 && l.ActualStackReturnMm >= -1e-7);
                        Assert.True(l.LandingLeftX >= -1e-7 && l.LandingRightX <= sw - 25 + 1e-7);
                    }
                }
                // Auto: the largest standard size that fits after shifting, else the smallest with its problems.
                var best = Sizes.OrderByDescending(o => o).FirstOrDefault(o => NeededCar(o) <= cw + 1e-7 && NeededShaft(type, o) <= sw + 1e-7);
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
        public void On_the_reference_shaft_the_recipe_selects_900_for_2S_and_800_for_2C()
        {
            // The equipment profile (150 mm rail allowance per side) gives a 1320 mm car in 1720; the car side gap is 200 mm.
            var s = Door(DoorTypes.Automatic2S);
            var ps = Plan(1720, 2800, s);
            Assert.Equal(1320, ps.CarWidth);
            Assert.Equal(900, ps.DoorWidth); // 1000 needs a 1750 mm shaft
            var c = Door(DoorTypes.Automatic2C);
            var pc = Plan(1720, 2800, c);
            Assert.Equal(800, pc.DoorWidth); // 900 needs 1900
            var user = DoorRules.Select(Door(DoorTypes.Automatic2S, new DoorSettings { OpeningMm = 1000 }), ps);
            Assert.False(user.Fits);
            Assert.Equal(1750, user.NeededShaftWidthMm.Value, 6);
            Assert.Equal(1350, user.NeededCarWidthMm.Value, 6);
            Assert.Equal("does not fit: needs car width 1350 mm, shaft width 1750 mm", DoorFlow.FitMessage(user));
        }

        [Fact]
        public void Preferred_layout_is_kept_when_it_fits_and_shifted_when_it_does_not()
        {
            // 2S 1000 at the recipe minimum shaft 1750: preferred opening X 250..1250 does not fit, the opening is shifted to X 200
            // (fixed return 0, flush with the car side wall); at 1900 the preferred layout fits.
            var door = Door(DoorTypes.Automatic2S, new DoorSettings { OpeningMm = 1000 });
            var tight = DoorRules.Select(door, Plan(1750, 2800, door));
            Assert.True(tight.Fits);
            Assert.True(tight.Shifted);
            Assert.Equal(0, tight.ActualFixedReturnMm, 6);
            Assert.Equal(200, tight.OpeningLeftX, 6);
            Assert.Equal(125, tight.PreferredOffsetMm, 6);
            Assert.Equal(1750 - 25, tight.LandingRightX, 6);
            var wide = DoorRules.Select(door, Plan(1900, 2800, door));
            Assert.True(wide.Fits);
            Assert.False(wide.Shifted);
            Assert.Equal(125, wide.OpeningOffsetMm, 6);
            Assert.Equal(125, wide.ActualFixedReturnMm, 6); // opening X 325: the preferred offset, 125 beyond the car side wall
        }

        // ---------- minimum shaft widths ----------
        // The opening can reach the shaft wall only when the car side wall does: car width = shaft width (gap 0).
        static ElevatorParameters FlushPlan(double sw)
        {
            var p = Reference(245);
            p.SW = sw;
            p.CarWidth = sw;
            p.CarDbg = sw;
            p.CwDbg = sw;
            p.CwWidth = sw - 40;
            return p;
        }

        [Theory]
        [InlineData(700, 1100)]
        [InlineData(800, 1250)]
        [InlineData(900, 1400)]
        [InlineData(1000, 1550)]
        [InlineData(1200, 1850)]
        public void Minimum_shaft_width_2S_with_the_opening_free_to_the_shaft_wall(double o, double min)
        {
            var d = Door(DoorTypes.Automatic2S);
            var at = DoorRules.Layout(d, FlushPlan(min), o, false);
            Assert.True(at.Fits, DoorFlow.FitMessage(at));
            Assert.Equal(0, at.ActualFixedReturnMm, 6);
            Assert.Equal(0, at.OpeningLeftX, 6);
            Assert.Equal(min - 25, at.LandingRightX, 6);
            var below = DoorRules.Layout(d, FlushPlan(min - 1), o, false);
            Assert.False(below.Fits);
            Assert.Equal(min, below.NeededShaftWidthMm.Value, 6);
        }

        [Theory]
        [InlineData(700, 1300)]
        [InlineData(800, 1450)]
        [InlineData(900, 1600)]
        [InlineData(1000, 1750)]
        [InlineData(1200, 2050)]
        public void Minimum_shaft_width_2S_with_the_recipe_car(double o, double min)
        {
            // g = 200 between the car side wall and the shaft wall: the opening edge can only reach the car side wall.
            var d = Door(DoorTypes.Automatic2S, new DoorSettings { OpeningMm = o });
            var at = DoorRules.Select(d, Plan(min, 2800, d));
            Assert.True(at.Fits, DoorFlow.FitMessage(at));
            Assert.Equal(0, at.ActualFixedReturnMm, 6);
            var below = DoorRules.Select(d, Plan(min - 1, 2800, d));
            Assert.False(below.Fits);
            Assert.Equal(min, below.NeededShaftWidthMm.Value, 6);
        }

        [Fact]
        public void Two_S_700_fits_a_1100_shaft_with_a_fixed_return_of_0()
        {
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S), FlushPlan(1100), 700, false);
            Assert.True(l.Fits);
            Assert.Equal(0, l.ActualFixedReturnMm, 6);
            Assert.True(l.Shifted);
            Assert.Equal(0, l.LandingFixedJambMm, 6); // the jamb went down to its minimum: flush with the shaft wall
            Assert.Equal(0, l.LandingLeftX, 6);
        }

        [Theory]
        [InlineData(700, 1500)]
        [InlineData(800, 1700)]
        [InlineData(900, 1900)]
        [InlineData(1000, 2100)]
        [InlineData(1200, 2500)]
        public void Minimum_shaft_width_2C_is_opening_plus_two_pockets_and_two_wall_clearances(double o, double min)
        {
            var d = Door(DoorTypes.Automatic2C, new DoorSettings { OpeningMm = o });
            foreach (var plan in new[] { FlushPlan(min), Plan(min, 2800, d) })
            {
                var at = DoorRules.Select(d, plan);
                Assert.True(at.Fits, DoorFlow.FitMessage(at));
                Assert.Equal(25, at.LandingLeftX, 6);
                Assert.Equal(min - 25, at.LandingRightX, 6);
            }
            var below = DoorRules.Select(d, Plan(min - 1, 2800, d));
            Assert.False(below.Fits);
            Assert.Equal(min, below.NeededShaftWidthMm.Value, 6);
            // The returns shrink to their minimum: the opening stays centred.
            Assert.Equal(0, DoorRules.Layout(d, FlushPlan(min), o, false).CentreShiftMm, 6);
        }

        [Fact]
        public void A_user_minimum_return_raises_the_needed_width()
        {
            var s = new DoorSettings { MinFixedReturnMm = 100, LandingWallClearanceMm = 40 };
            var l = DoorRules.Layout(Door(DoorTypes.Automatic2S, s), FlushPlan(1250), 800, false);
            // fixed edge >= g + 100 = 100; 100 + 800 + 425 + 40 = 1365
            Assert.False(l.Fits);
            Assert.Equal(1365, l.NeededShaftWidthMm.Value, 6);
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
            p.Shaft.WidthMm = 1500;
            Assert.Equal(800, Opening(p)); // 800 needs a 1450 mm shaft, 900 needs 1600
            p.Shaft.WidthMm = 1750;
            Assert.Equal(1000, Opening(p)); // 1000 needs 1750
            // Locked 700 stays when the shaft changes.
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 700);
            Assert.True(DoorFlow.IsLocked(p, DoorSettings.OpeningKey));
            p.Shaft.WidthMm = 2200;
            Assert.Equal(700, Opening(p));
            Assert.Contains(DoorSettings.OpeningKey, p.UserEditedFields);
            // Reset: the opening follows again.
            DoorFlow.Reset(p, DoorSettings.OpeningKey);
            Assert.False(DoorFlow.IsLocked(p, DoorSettings.OpeningKey));
            Assert.Equal(1200, Opening(p)); // 1200 needs 2050
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
            // The preferred offset follows the preferred returns while it is not locked.
            Assert.Equal((after.StackReturnMm - 260) / 2, after.PreferredOffsetMm, 6);
            // The Doors Offset: 40 mm Left. With the stack on the right the left is the fixed side.
            DoorFlow.SetOffset(p, 40, DoorTypes.Left);
            Assert.Equal(40, Layout(p).PreferredOffsetMm);
            DoorFlow.Reset(p, DoorSettings.FixedReturnKey);
            DoorFlow.Reset(p, DoorSettings.OffsetKey);
            Assert.Equal(200, Layout(p).FixedReturnMm);
            Assert.Equal(before.PreferredOffsetMm, Layout(p).PreferredOffsetMm, 6);
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
            Assert.Contains("car width of at least 2100 mm", note);
            Assert.Contains("shaft width of at least 2500 mm", note);
            Assert.Null(DoorFlow.DesignNote(DoorRules.Select(Door(DoorTypes.Automatic2C), Plan(1900, 2800, Door(DoorTypes.Automatic2C)))));
        }
    }
}
