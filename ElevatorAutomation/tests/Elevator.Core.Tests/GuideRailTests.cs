using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Xunit;

namespace Elevator.Core.Tests
{
    // The guide-rail check of EN 81-50 5.10 (docs/calculations/GUIDE-RAILS.md 11 and 13): the hand checks of the published example
    // (S6), the project fixtures, the recommendation properties, lock and follow, and the JSON round trip of the new inputs.
    public class GuideRailTests
    {
        static readonly Lazy<PresetLibrary> Lib = new Lazy<PresetLibrary>(() => PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")));
        static GuideRailPreset Preset { get { return Lib.Value.GuideRailsOrDefault(); } }

        // S6 section 3, rail T127-1/B (370 N/mm², conventional guiding): section values read back from its printed stresses.
        static RailProfile S6Rail()
        {
            return new RailProfile
            {
                Name = "S6",
                HeightMm = 89,
                WidthMm = 127,
                BladeMm = 16,
                NeckMm = 10,
                FootMm = 11,
                AreaMm2 = 2274,
                MassKgPerM = 17.9,
                IxxMm4 = 1860000,
                WxxMm3 = 31000,
                IxMm = 28.6,
                IyyMm4 = 1480000,
                WyyMm3 = 23600,
                IyMm = 23.61,
                RmNmm2 = 370,
                A5Band = RailProfile.Over12,
                ShoeType = RailProfile.Roller,
                ShoeLiningLengthMm = 140,
                ShoeHalfWidthMm = 19
            };
        }

        static RailCaseInput S6Case(RailProfile rail, double l, double fvN, params RailForce[] forces)
        {
            var c = new RailCaseInput
            {
                Id = "safety",
                Title = "S6",
                Profile = rail,
                SpacingMm = l,
                FvN = fvN,
                FvEquation = "Fv",
                SafetyDevice = true,
                DeflectionPermMm = 5,
                SafetyFactor = 1.8
            };
            c.Forces.AddRange(forces);
            return c;
        }

        static RailForce Force(double fx, double fy)
        {
            return new RailForce { FxN = fx, FyN = fy, FxEquation = "Fx", FyEquation = "Fy" };
        }

        static void Near(double expected, double actual, double tolerance, string what)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, what + ": expected " + expected + " ± " + tolerance + ", got " + actual);
        }

        [Fact]
        public void Omega_polynomials_match_the_published_values()
        {
            Near(4.85, GuideRailMath.Omega370(169.4), 0.01, "omega at lambda 169.4");
            Near(2.02, GuideRailMath.Omega370(106), 0.01, "omega at lambda 106");
            // the ranges join: the last two polynomials differ by less than 0.5 % at 115
            Near(GuideRailMath.Omega370(115), 0.00016887 * 115 * 115, 0.01, "omega at 115");
            Assert.Equal(GuideRailMath.Omega370(120), GuideRailMath.Omega(120, 370), 12);
            Assert.True(GuideRailMath.Omega(120, 445) > GuideRailMath.Omega370(120) - 1e-9);
        }

        [Fact]
        public void S6_example_1_conventional_guiding()
        {
            // k1 = 3, h = 2800, Q = 1000, Dx = 1400, Dy = 1600, n = 2: case 1 (Fx only) and case 2 (Fy only).
            double fx = 3 * 9.81 * (1000 * 175) / (2 * 2800.0), fy = 3 * 9.81 * (1000 * 200) / (1 * 2800.0);
            Near(920, fx, 1, "Fx case 1");
            Near(2102, fy, 1, "Fy case 2");
            var r = GuideRailCheckEngine.Evaluate(S6Case(S6Rail(), 4000, 36611, Force(fx, 0), Force(0, fy)), Preset);
            Near(169.4, r.Item("lambda").Value, 0.1, "lambda");
            Near(4.85, r.Item("omega").Value, 0.01, "omega");
            Near(78, r.Item("sk").Value, 1, "sigma k");
            Near(29, r.Item("sy").Value, 1, "sigma y");
            Near(51, r.Item("sx").Value, 1, "sigma x");
            Near(51, r.Item("sm").Value, 1, "sigma m (worst case)");
            Near(67, r.Item("scomp").Value, 1, "bending and compression");
            Near(124, r.Item("sbuck").Value, 1, "bending and buckling (sigma k + 0.9 sigma m)");
            Near(205.56, r.SigmaPermNmm2, 0.01, "sigma perm 370 / 1.8");
            Near(2.8, r.Item("dx").Value, 0.1, "deflection dx");
            Near(5.0, r.Item("dy").Value, 0.1, "deflection dy");
        }

        [Fact]
        public void S6_example_2_cantilever_guiding()
        {
            double fx1 = 3 * 9.81 * (1000 * 888 + 1250 * 500) / (2 * 2800.0), fx2 = 3 * 9.81 * (1000 * 750 + 1250 * 500) / (2 * 2800.0);
            double fy2 = 3 * 9.81 * (1000 * 263 + 0) / (1 * 2800.0);
            Near(7951, fx1, 2, "Fx case 1");
            Near(7226, fx2, 2, "Fx case 2");
            Near(2764, fy2, 15, "Fy case 2");
            var roller = S6Rail();
            var r = GuideRailCheckEngine.Evaluate(S6Case(roller, 2500, 36611, Force(fx1, 0), Force(fx2, fy2)), Preset);
            Near(106, r.Item("lambda").Value, 0.5, "lambda");
            Near(2.02, r.Item("omega").Value, 0.01, "omega");
            Near(33, r.Item("sk").Value, 1, "sigma k");
            Near(158, r.Item("sy").Value, 1.5, "sigma y case 1");
            Near(185, r.Item("sm").Value, 1.5, "sigma m case 2");
            Near(201, r.Item("scomp").Value, 1.5, "bending and compression");
            Near(200, r.Item("sbuck").Value, 1.5, "bending and buckling");
            Near(147, r.Item("sf").Value, 1, "flange bending, roller shoe");
            Near(5.8, r.Item("dx").Value, 0.1, "deflection dx");
            var sliding = S6Rail();
            sliding.ShoeType = RailProfile.Sliding;
            Near(96, GuideRailMath.FlangeBending(sliding, 7951), 1, "flange bending, sliding shoe");
        }

        [Fact]
        public void Project_fixture_T70_at_2000_with_the_application_FF_values()
        {
            var ff = CarGuideForceCalculationEngine.EvaluateNoteRule(480, 1100, 2100, 2800);
            Near(942.857, ff[1], 0.001, "FF1 design");
            Near(900, ff[3], 0.001, "FF2 design");
            var t70 = Preset.Profile("T70/65/9");
            var r = GuideRailCheckEngine.Evaluate(S6Case(t70, 2000, 5000, Force(ff[3], ff[1])), Preset);
            Near(63.1, r.Item("sy").Value, 0.05, "sigma y");
            Near(38.3, r.Item("sx").Value, 0.05, "sigma x");
            Near(101.4, r.Item("sm").Value, 0.1, "sigma m");
            Near(2.68, r.Item("dx").Value, 0.01, "deflection dx");
            Assert.True(r.Item("sm").Ok == true && r.Item("dx").Ok == true);
            // the note's own fixture, h = 1200 mm: sigma m of T70/65/9 at 2000 mm is 236.5 N/mm², NOT OK
            var ff2 = CarGuideForceCalculationEngine.EvaluateNoteRule(480, 1100, 2100, 1200);
            var bad = GuideRailCheckEngine.Evaluate(S6Case(t70, 2000, 5000, Force(ff2[3], ff2[1])), Preset);
            Near(236.5, bad.Item("sm").Value, 0.2, "sigma m at h = 1200");
            Assert.False(bad.Item("sm").Ok.Value);
            Assert.False(bad.Ok);
        }

        [Fact]
        public void Slenderness_below_20_uses_20_and_above_250_is_not_ok()
        {
            var t50 = Preset.Profile("T50/50/5");
            var low = GuideRailCheckEngine.Evaluate(S6Case(t50, 100, 1000, Force(100, 100)), Preset);
            Near(GuideRailMath.Omega370(20), low.Item("omega").Value, 1e-12, "omega at lambda 20");
            Assert.True(low.Item("lambda").Ok == true);
            var high = GuideRailCheckEngine.Evaluate(S6Case(t50, 2700, 1000, Force(100, 100)), Preset);
            Assert.True(high.Item("lambda").Value > 250);
            Assert.False(high.Item("lambda").Ok.Value);
            Assert.False(high.Ok);
        }

        [Fact]
        public void The_safety_factor_follows_the_elongation_band()
        {
            var p = GuideRailDefaults.Create().Profile("T70/65/9");
            Assert.Equal(2.25, p.SafetyFactor(false));
            Assert.Equal(1.8, p.SafetyFactor(true));
            p.A5Band = RailProfile.Between8And12;
            Assert.Equal(3.75, p.SafetyFactor(false));
            Assert.Equal(3.0, p.SafetyFactor(true));
        }

        [Fact]
        public void Library_has_the_four_profiles_in_mass_order_and_the_defaults_equal_the_file()
        {
            var g = Lib.Value.GuideRails;
            Assert.NotNull(g);
            Assert.Equal(new[] { "T50/50/5", "T70/65/9", "T89/62/16", "T90/75/16" }, g.Profiles.Select(p => p.Name).ToArray());
            Assert.True(g.Profiles.Zip(g.Profiles.Skip(1), (a, b) => b.MassKgPerM > a.MassKgPerM).All(x => x));
            Assert.Empty(g.Validate());
            Assert.Equal(CalculationFingerprintHash(g), CalculationFingerprintHash(GuideRailDefaults.Create()));
            Assert.All(g.Profiles, p => Assert.Equal(370, p.RmNmm2));
            Assert.Equal(210000, g.ElasticModulusNmm2);
            Assert.Equal(new[] { 2000d, 1950d }, g.Spacings().Take(2).ToArray());
            Assert.Equal(500, g.Spacings().Last());
        }


        // ---------- the project: recommendation, lock and follow, machine on the rails, push-through, round trip ----------
        static ElevatorProject NewProject(double? capacityKg)
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            var p = ProjectPresets.CreateNew(Lib.Value, TestData.Context(), seed.Counterweight, seed.Rails.BracketSource);
            if (capacityKg.HasValue)
            {
                ProjectPresets.Find("Elevator.CapacityKg").Edit(p, capacityKg.Value);
                ProjectPresets.Apply(p, Lib.Value, TestData.Context());
            }
            return p;
        }

        static GuideRailCheckResult Check(ElevatorProject p)
        {
            return GuideRailCheckEngine.Calculate(p, TestData.Context(), Lib.Value);
        }

        static void Edit(ElevatorProject p, string key, object value)
        {
            ProjectPresets.Find(key).Edit(p, value);
            ProjectPresets.Apply(p, Lib.Value, TestData.Context());
        }

        [Fact]
        public void A_new_project_gets_the_rail_inputs_from_the_recommendation_and_every_item_is_reported()
        {
            var p = NewProject(null);
            Assert.False(p.Rails.MachineOnRails.Value);
            Assert.False(String.IsNullOrEmpty(p.Rails.CarProfile));
            Assert.False(String.IsNullOrEmpty(p.Rails.CwProfile));
            Assert.True(p.Rails.CarSpacingMm >= 500 && p.Rails.CarSpacingMm <= 2000);
            Assert.True(p.Rails.CwSpacingMm >= 500 && p.Rails.CwSpacingMm <= 2000);
            var r = Check(p);
            Assert.True(r.Available, string.Join("; ", r.Issues.Select(i => i.Message)));
            Assert.True(r.Ok);
            Assert.Equal(new[] { "running", "loading", "safety" }, r.Car.Cases.Select(c => c.Id).ToArray());
            Assert.Equal(new[] { "running" }, r.Counterweight.Cases.Select(c => c.Id).ToArray());
            var ids = new[] { "Fx", "Fy", "Fv", "sx", "sy", "sm", "lambda", "omega", "sk", "scomp", "sbuck", "sf", "dx", "dy" };
            foreach (var c in r.Car.Cases.Concat(r.Counterweight.Cases))
            {
                Assert.Equal(ids, c.Items.Select(i => i.Id).ToArray());
                foreach (var i in c.Items)
                {
                    Assert.True(!Double.IsNaN(i.Value) && !Double.IsInfinity(i.Value) && i.Value >= 0, i.Id);
                    Assert.Equal(i.Id == "omega" || i.IsForce, !i.Permissible.HasValue);
                    Assert.Equal(i.Permissible.HasValue, i.Ok.HasValue);
                    Assert.False(String.IsNullOrEmpty(i.Equation));
                }
            }
            // the safety-device case takes the FF1 / FF2 design values of the application and 205.56 = 370 / 1.8
            var safety = r.Car.Case("safety");
            Near(205.5556, safety.SigmaPermNmm2, 0.001, "sigma perm safety device");
            Near(164.4444, r.Car.Case("running").SigmaPermNmm2, 0.001, "sigma perm normal operation");
            Near(r.Inputs.Ff2DesignN, safety.Item("Fx").Value, 1e-9, "Fx = FF2");
            Near(r.Inputs.Ff1DesignN, safety.Item("Fy").Value, 1e-9, "Fy = FF1");
            Near(5, 5, 0, "deflection limit car");
        }

        [Fact]
        public void The_recommended_profile_is_the_smallest_that_passes_and_the_spacing_the_largest()
        {
            foreach (double q in new[] { 320.0, 630, 1000, 1275, 1600 })
            {
                var p = NewProject(q);
                var r = Check(p);
                Assert.True(r.Available);
                foreach (bool car in new[] { true, false })
                {
                    var line = car ? r.Car : r.Counterweight;
                    var g = r.Inputs;
                    var list = g.Preset.Profiles;
                    int index = list.FindIndex(x => x.Name == line.Profile.Name);
                    bool anyPasses = list.Any(x => GuideRailCheckEngine.Line(g, car, x, g.Preset.MaxSpacingMm).Ok);
                    if (anyPasses)
                    {
                        Assert.True(GuideRailCheckEngine.Line(g, car, line.Profile, g.Preset.MaxSpacingMm).Ok);
                        for (int k = 0; k < index; k++)
                            Assert.False(GuideRailCheckEngine.Line(g, car, list[k], g.Preset.MaxSpacingMm).Ok, "a smaller profile passes at 2000: " + q);
                    }
                    else
                        Assert.Equal(list.Count - 1, index);
                    if (line.Ok)
                    {
                        if (line.SpacingMm < g.Preset.MaxSpacingMm)
                            Assert.False(GuideRailCheckEngine.Line(g, car, line.Profile, line.SpacingMm + g.Preset.SpacingStepMm).Ok);
                    }
                    else
                        Assert.Equal(g.Preset.MinSpacingMm, line.SpacingMm);
                    Assert.Equal(0, (line.SpacingMm - g.Preset.MinSpacingMm) % g.Preset.SpacingStepMm);
                }
            }
        }

        // Deterministic pseudo-random inputs over the design range of the products (travel up to 40 m: no push-through force).
        static IEnumerable<GuideRailInputs> Samples(int count)
        {
            var rnd = new Random(20261013);
            for (int k = 0; k < count; k++)
            {
                var g = new GuideRailInputs { Preset = GuideRailDefaults.Create() };
                g.CapacityKg = 300 + rnd.Next(0, 1400);
                g.CarMassKg = 400 + rnd.Next(0, 1300);
                g.CounterweightMassKg = g.CarMassKg + 0.5 * g.CapacityKg;
                g.CarDepthMm = 900 + rnd.Next(0, 1500);
                g.CarWidthMm = 900 + rnd.Next(0, 1500);
                g.CwDepthMm = 150 + rnd.Next(0, 250);
                g.CwWidthMm = 600 + rnd.Next(0, 900);
                g.ShoeSpacingMm = 1500 + rnd.Next(0, 2500);
                g.CwShoeSpacingMm = g.Preset.CwShoeSpacingMm;
                g.K1 = new[] { 2.0, 3, 5 }[rnd.Next(0, 3)];
                var ff = CarGuideForceCalculationEngine.EvaluateNoteRule(g.CapacityKg, g.CarWidthMm, g.CarDepthMm, g.ShoeSpacingMm);
                g.Ff1DesignN = ff[1];
                g.Ff2DesignN = ff[3];
                g.TravelMm = 3000 + rnd.Next(0, 37000);
                g.WellHeightMm = 1300 + g.TravelMm + 4200;
                g.AuxPerRailN = rnd.Next(0, 2) == 0 ? 0 : rnd.Next(1000, 12000);
                yield return g;
            }
        }

        [Fact]
        public void Property_a_larger_profile_never_fails_where_a_smaller_one_passes_and_a_smaller_spacing_never_fails_where_a_larger_passes()
        {
            int profilePairs = 0, spacingPairs = 0;
            foreach (var g in Samples(400))
            {
                var list = g.Preset.Profiles;
                foreach (bool car in new[] { true, false })
                {
                    foreach (double l in g.Preset.Spacings())
                    {
                        bool previous = false;
                        for (int k = 0; k < list.Count; k++)
                        {
                            bool ok = GuideRailCheckEngine.Line(g, car, list[k], l).Ok;
                            if (previous) { Assert.True(ok, "profile order: " + list[k].Name + " fails at " + l + " where " + list[k - 1].Name + " passes"); profilePairs++; }
                            previous = previous || ok;
                        }
                    }
                    foreach (var profile in list)
                    {
                        bool passed = false;
                        foreach (double l in g.Preset.Spacings().OrderByDescending(x => x))
                        {
                            bool ok = GuideRailCheckEngine.Line(g, car, profile, l).Ok;
                            if (passed) { Assert.True(ok, "spacing order: " + profile.Name + " fails at " + l); spacingPairs++; }
                            passed = passed || ok;
                        }
                    }
                }
            }
            Assert.True(profilePairs > 1000 && spacingPairs > 1000, profilePairs + " " + spacingPairs);
        }

        [Fact]
        public void Lock_and_follow_for_the_rail_inputs_and_a_failing_choice_is_kept()
        {
            var p = NewProject(null);
            string before = p.Rails.CarProfile;
            Edit(p, "Elevator.CapacityKg", 1600.0);
            Assert.NotEqual(before, p.Rails.CarProfile);          // unlocked: follows the rated load
            Assert.Null(p.UserEditedFields.FirstOrDefault(k => k == ProjectPresets.CarProfileKey));
            // a typed profile is locked and kept; the spacing (unlocked) follows the chosen profile
            Edit(p, ProjectPresets.CarProfileKey, "T50/50/5");
            Assert.Contains(ProjectPresets.CarProfileKey, p.UserEditedFields);
            Edit(p, "Elevator.CapacityKg", 320.0);
            Assert.Equal("T50/50/5", p.Rails.CarProfile);
            double followed = p.Rails.CarSpacingMm.Value;
            Edit(p, "Elevator.CapacityKg", 1600.0);
            Assert.Equal("T50/50/5", p.Rails.CarProfile);          // a failing choice is kept
            Assert.False(Check(p).Car.Ok);
            Assert.Equal(Lib.Value.GuideRailsOrDefault().MinSpacingMm, p.Rails.CarSpacingMm);
            Assert.NotEqual(followed, p.Rails.CarSpacingMm.Value);
            // a typed spacing is locked and kept
            Edit(p, ProjectPresets.CarSpacingKey, 1234.0);
            Edit(p, "Elevator.CapacityKg", 320.0);
            Assert.Equal(1234.0, p.Rails.CarSpacingMm);
            Assert.Equal(1234.0, Check(p).Car.SpacingMm);
            // Reset to preset: the profile and spacing follow again
            ProjectPresets.Find(ProjectPresets.CarProfileKey).Reset(p);
            ProjectPresets.Find(ProjectPresets.CarSpacingKey).Reset(p);
            ProjectPresets.Apply(p, Lib.Value, TestData.Context());
            Assert.DoesNotContain(ProjectPresets.CarProfileKey, p.UserEditedFields ?? new List<string>());
            Assert.NotEqual("T50/50/5", p.Rails.CarProfile);
            Assert.True(Check(p).Car.Ok);
            // the counterweight rails follow in the same way
            Edit(p, ProjectPresets.CwProfileKey, "T90/75/16");
            Edit(p, "Elevator.CapacityKg", 1600.0);
            Assert.Equal("T90/75/16", p.Rails.CwProfile);
        }

        [Fact]
        public void Machine_on_the_rails_adds_a_quarter_of_the_machine_and_rope_force_to_every_rail()
        {
            var p = NewProject(1000);
            var off = Check(p);
            Edit(p, ProjectPresets.MachineOnRailsKey, true);
            var on = Check(p);
            Assert.True(on.Available);
            var g = on.Inputs;
            Assert.True(g.MachineOnRails);
            double expected = (g.MachineMassKg + g.CarMassKg + g.CapacityKg + g.CounterweightMassKg + g.RopeMassKg) * 9.81;
            Near(expected, g.MachineForceN, 1e-6, "Fm");
            Near(expected / 4, g.AuxPerRailN, 1e-6, "Maux per rail");
            Assert.Equal(0, off.Inputs.AuxPerRailN);
            // the car and CW rails carry it with k3 in the buckling and compression terms of the same profile and spacing
            var sameCar = GuideRailCheckEngine.Line(off.Inputs, true, on.Car.Profile, on.Car.SpacingMm);
            foreach (var c in on.Car.Cases)
            {
                var o = sameCar.Case(c.Id);
                Near(1.2 * g.AuxPerRailN, c.AuxN, 1e-6, "k3 Maux");
                Near(1.2 * g.AuxPerRailN * c.Item("omega").Value / on.Car.Profile.AreaMm2, c.Item("sk").Value - o.Item("sk").Value, 1e-6, "sigma k grows by k3 Maux omega / A");
                Near(1.2 * g.AuxPerRailN / on.Car.Profile.AreaMm2, c.Item("scomp").Value - o.Item("scomp").Value, 1e-6, "compression grows by k3 Maux / A");
                Near(o.Item("sm").Value, c.Item("sm").Value, 1e-12, "bending is unchanged");
            }
        }

        [Fact]
        public void The_push_through_force_applies_above_40_m_of_travel_only()
        {
            var g = Samples(1).First();
            var t89 = g.Preset.Profile("T89/62/16");
            g.TravelMm = 40000;
            g.WellHeightMm = 40000 + 5500;
            var at40 = GuideRailCheckEngine.Line(g, true, t89, 1500);
            Assert.Equal(0, at40.PushThroughN);
            g.TravelMm = 40001;
            var over = GuideRailCheckEngine.Line(g, true, t89, 1500);
            int nb = (int)Math.Ceiling(g.WellHeightMm / 1500) + 1;
            Assert.Equal(nb, over.BracketCount);
            Near(nb * g.Preset.ClipPushThroughN, over.PushThroughN, 1e-9, "Fp = nb Fr");
            Near(over.PushThroughN, over.Case("running").FvN - (t89.MassKgPerM * g.WellHeightMm / 1000.0 * 9.81), 1e-6, "Fv = Mg gn + Fp");
        }

        [Fact]
        public void The_rail_inputs_survive_the_project_file_and_an_older_file_has_none()
        {
            var p = NewProject(1000);
            Edit(p, ProjectPresets.MachineOnRailsKey, true);
            Edit(p, ProjectPresets.CwProfileKey, "T70/65/9");
            var back = ProjectJson.Deserialize(ProjectJson.Serialize(p));
            Assert.Equal(p.Rails.MachineOnRails, back.Rails.MachineOnRails);
            Assert.Equal(p.Rails.CarProfile, back.Rails.CarProfile);
            Assert.Equal(p.Rails.CarSpacingMm, back.Rails.CarSpacingMm);
            Assert.Equal("T70/65/9", back.Rails.CwProfile);
            Assert.Equal(p.Rails.CwSpacingMm, back.Rails.CwSpacingMm);
            Assert.Equal(p.UserEditedFields.OrderBy(x => x).ToArray(), back.UserEditedFields.OrderBy(x => x).ToArray());
            Assert.Contains(ProjectPresets.CwProfileKey, back.UserEditedFields);
            var old = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            Assert.Null(old.Rails.CarProfile);
            Assert.Null(old.Rails.MachineOnRails);
            Assert.False(GuideRailCheckEngine.Calculate(old, TestData.Context(), Lib.Value).Available);
        }

        static string CalculationFingerprintHash(GuideRailPreset g)
        {
            return Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Serialize(g);
        }
    }
}
