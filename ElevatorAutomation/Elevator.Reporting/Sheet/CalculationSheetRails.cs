using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Sheet
{
    /// <summary>
    /// "Guide rails" section (EN 81-50:2014 5.10; EN 81-20:2014 5.7): the inputs, the equations with their values and, for every item,
    /// the value, the permissible value and OK / NOT OK. Nothing else: the reasons and the needed profile or spacing go to the Design
    /// Notes file only (DESIGN-FLOW "Guide rails").
    /// </summary>
    public sealed partial class CalculationSheet
    {
        public GuideRailCheckResult GuideRails { get; private set; }

        public bool GuideRailsAvailable
        {
            get
            {
                return GuideRails != null && GuideRails.Available;
            }
        }

        /// <summary>Title of the guide-rail section.</summary>
        public const string GuideRailsSectionTitle = "Guide rails";

        // The rail inputs are chosen (stored in the project) but the check cannot be calculated: listed with the other omitted
        // calculations. A project with no rail inputs has no guide-rail check at all.
        void BuildGuideRails(DesignContext context)
        {
            var rails = Project.Rails;
            if (Presets == null || context == null || rails == null)
                return;
            GuideRails = GuideRailCheckEngine.Calculate(Project, context, Presets);
            bool chosen = !String.IsNullOrWhiteSpace(rails.CarProfile) && rails.CarSpacingMm.HasValue
                && !String.IsNullOrWhiteSpace(rails.CwProfile) && rails.CwSpacingMm.HasValue;
            if (!GuideRails.Available)
            {
                if (chosen)
                    Omit(GuideRailsSectionTitle, GuideRails.Issues);
                return;
            }
            if (NotOk == null)
                NotOk = new List<string>();
            foreach (var line in new[] { GuideRails.Car, GuideRails.Counterweight })
            {
                foreach (var f in line.Failures())
                {
                    NotOk.Add("Guide rails, " + (line.Name == "Car" ? "car" : "counterweight") + " rails, " + f.Key.Title.ToLowerInvariant() + ": "
                        + f.Value.Title + " " + RailValue(f.Value, f.Value.Value) + " (permissible " + RailValue(f.Value, f.Value.Permissible.Value) + ")");
                }
            }
        }

        // The summary row: the chosen rails and the overall result.
        string GuideRailsSummary()
        {
            var r = GuideRails;
            return "Car rails " + r.Car.Profile.Name + " at " + L(r.Car.SpacingMm) + " mm: " + (r.Car.Ok ? "OK" : "NOT OK")
                + "; counterweight rails " + r.Counterweight.Profile.Name + " at " + L(r.Counterweight.SpacingMm) + " mm: "
                + (r.Counterweight.Ok ? "OK" : "NOT OK");
        }

        static string RailValue(RailItem i, double v)
        {
            if (i.IsForce)
                return L(v) + " N (" + LoadUnits.KgText(v) + " kg)";
            if (i.Id == "lambda")
                return v.ToString("0.#", Inv);
            if (i.Id == "omega")
                return v.ToString("0.###", Inv);
            return L(v) + (i.Unit == "" ? "" : " " + i.Unit);
        }

        void GuideRailsSection(DocxDocument d, int n)
        {
            var r = GuideRails;
            d.Heading(n + "  " + GuideRailsSectionTitle, 1);
            d.Flow = true;
            d.Paragraph("Guide rails of the car and of the counterweight: bending, combined stress, buckling, flange bending and deflection "
                + "(EN 81-50:2014 5.10; EN 81-20:2014 5.7).", null, null, true);
            d.Table(new[] { "Input", "Value" }, GuideRailInputRows(r), TwoCol, new HashSet<int> { 0 });
            d.Paragraph("Rail data", "Heading2");
            d.Table(new[] { "Quantity", "Car rails", "Counterweight rails" }, GuideRailDataRows(r), new[] { 4200, 2719, 2719 }, new HashSet<int> { 0 });
            foreach (var line in new[] { r.Car, r.Counterweight })
            {
                foreach (var c in line.Cases)
                    GuideRailCase(d, line, c);
            }
            d.Flow = false;
        }

        List<string[]> GuideRailInputRows(GuideRailCheckResult r)
        {
            var g = r.Inputs;
            var p = g.Preset;
            var rows = new List<string[]>();
            rows.Add(new[] { "Machine supported on the guide rails", g.MachineOnRails ? "Yes" : "No" });
            rows.Add(new[] { "Car rail profile", r.Car.Profile.Name });
            rows.Add(new[] { "Car rail bracket spacing l", L(r.Car.SpacingMm) + " mm" });
            rows.Add(new[] { "Counterweight rail profile", r.Counterweight.Profile.Name });
            rows.Add(new[] { "Counterweight rail bracket spacing l", L(r.Counterweight.SpacingMm) + " mm" });
            rows.Add(new[] { "Number of guide rails per line n", p.RailCount.ToString(Inv) });
            rows.Add(new[] { "Rated load Q", L(g.CapacityKg) + " kg" });
            rows.Add(new[] { "Car mass P", L(g.CarMassKg) + " kg" });
            rows.Add(new[] { "Counterweight mass Mcwt", L(g.CounterweightMassKg) + " kg" });
            rows.Add(new[] { "Car depth Dx", L(g.CarDepthMm) + " mm" });
            rows.Add(new[] { "Car width Dy", L(g.CarWidthMm) + " mm" });
            rows.Add(new[] { "Guide-shoe spacing of the car h", L(g.ShoeSpacingMm) + " mm" });
            rows.Add(new[] { "Counterweight depth", L(g.CwDepthMm) + " mm" });
            rows.Add(new[] { "Counterweight width", L(g.CwWidthMm) + " mm" });
            rows.Add(new[] { "Guide-shoe spacing of the counterweight hcw", L(g.CwShoeSpacingMm) + " mm" });
            rows.Add(new[] { "Impact factor of the safety gear k1", L(g.K1) });
            rows.Add(new[] { "Impact factor of running kr", L(p.RunningImpactFactor) });
            rows.Add(new[] { "Well height (pit + travel + overhead)", L(g.WellHeightMm) + " mm" });
            rows.Add(new[] { "Travel", L(g.TravelMm) + " mm" });
            if (g.MachineOnRails)
            {
                rows.Add(new[] { "Machine mass", L(g.MachineMassKg) + " kg" });
                rows.Add(new[] { "Suspended rope mass", L(g.RopeMassKg) + " kg" });
                rows.Add(new[] { "Number of rails carrying the machine frame nm", p.MachineRailCount.ToString(Inv) });
            }
            rows.Add(new[] { "Modulus of elasticity E", L(p.ElasticModulusNmm2) + " N/mm²" });
            rows.Add(new[] { "Structure deflection δstr-x / δstr-y", L(p.StructureDeflectionXMm) + " mm / " + L(p.StructureDeflectionYMm) + " mm" });
            return rows;
        }

        static List<string[]> GuideRailDataRows(GuideRailCheckResult r)
        {
            RailProfile a = r.Car.Profile, b = r.Counterweight.Profile;
            var rows = new List<string[]>();
            Action<string, Func<RailProfile, string>> add = (label, f) => rows.Add(new[] { label, f(a), f(b) });
            add("Profile", x => x.Name);
            add("Cross-section A (mm²)", x => L(x.AreaMm2));
            add("Section modulus Wx (mm³)", x => L(x.WxxMm3));
            add("Section modulus Wy (mm³)", x => L(x.WyyMm3));
            add("Second moment Ix (mm⁴)", x => L(x.IxxMm4));
            add("Second moment Iy (mm⁴)", x => L(x.IyyMm4));
            add("Minimum radius of gyration i (mm)", x => L(x.MinimumRadiusMm));
            add("Mass per metre (kg/m)", x => L(x.MassKgPerM));
            rows.Add(new[] { "Mass of one rail line Mg (kg)", L(r.Car.LineMassKg), L(r.Counterweight.LineMassKg) });
            if (r.Car.PushThroughN > 0 || r.Counterweight.PushThroughN > 0)
                rows.Add(new[] { "Push-through force Fp (N)", L(r.Car.PushThroughN), L(r.Counterweight.PushThroughN) });
            add("Tensile strength Rm (N/mm²)", x => L(x.RmNmm2));
            add("Rail height h1 (mm)", x => L(x.HeightMm));
            add("Width of the connection c (mm)", x => L(x.NeckMm));
            add("Foot depth f (mm)", x => L(x.FootMm));
            add("Guide shoes", x => x.ShoeType == RailProfile.Roller ? "Roller" : "Sliding");
            add("Half width of the shoe lining b (mm)", x => L(x.ShoeHalfWidthMm));
            add("Length of the shoe lining lL (mm)", x => L(x.ShoeLiningLengthMm));
            return rows;
        }

        void GuideRailCase(DocxDocument d, RailLineResult line, RailLoadCaseResult c)
        {
            d.Paragraph((line.Name == "Car" ? "Car rails" : "Counterweight rails") + " — " + c.Title + " (" + c.Clause + ")", "Heading2");
            var equations = new List<string> { "σperm = Rm / St = " + L(line.Profile.RmNmm2) + " / " + L(c.SafetyFactor) + " = " + L(c.SigmaPermNmm2) + " N/mm²" };
            if (c.AuxEquation != null)
                equations.Add(c.AuxEquation);
            equations.AddRange(c.Items.Select(i => i.Equation));
            d.Equations(true, equations);
            var rows = c.Items.Select(i => new[]
            {
                i.Title,
                RailValue(i, i.Value),
                i.Permissible.HasValue ? RailValue(i, i.Permissible.Value) : "",
                i.Ok.HasValue ? (i.Ok.Value ? "OK" : "NOT OK") : ""
            }).ToList();
            d.Table(new[] { "Item", "Value", "Permissible", "Result" }, rows, new[] { 3300, 2638, 2200, 1500 }, new HashSet<int> { 0, 3 });
        }
    }
}
