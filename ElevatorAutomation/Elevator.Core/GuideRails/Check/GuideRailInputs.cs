using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Suspension;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The project quantities of the rail check that do not depend on the chosen profile and spacing (GUIDE-RAILS.md 13): loads, car and
    /// counterweight dimensions, guide-shoe spacings, factors, well height, machine force. Built from the effective project values
    /// (the project value when edited, otherwise the preset), so the check and the recommendation agree.
    /// </summary>
    public sealed class GuideRailInputs
    {
        public GuideRailPreset Preset;
        public bool MachineOnRails;
        public double CapacityKg;
        /// <summary>P: car mass plus the car-side additional suspended mass.</summary>
        public double CarMassKg;
        /// <summary>Mcwt: counterweight mass plus the counterweight-side additional suspended mass.</summary>
        public double CounterweightMassKg;
        /// <summary>Dx: car depth (X, perpendicular to the rail line).</summary>
        public double CarDepthMm;
        /// <summary>Dy: car width (Y, along the line joining the car rails).</summary>
        public double CarWidthMm;
        public double CwDepthMm;
        public double CwWidthMm;
        /// <summary>h: distance between the car guide shoes.</summary>
        public double ShoeSpacingMm;
        public double CwShoeSpacingMm;
        public string SafetyGearType;
        public double K1;
        public double Ff1DesignN;
        public double Ff2DesignN;
        public double TravelMm;
        public double WellHeightMm;
        /// <summary>P + Q + Mmachine + Mrope in kg (GUIDE-RAILS.md 13.4); 0 when the machine is not on the rails.</summary>
        public double MachineLoadMassKg;
        public double MachineMassKg;
        public double RopeMassKg;

        /// <summary>The effective value of a managed field, or null.</summary>
        static object Eff(PresetContext c, string key)
        {
            var f = ProjectPresets.Find(key);
            return f == null ? null : f.Effective(c);
        }

        static double? D(object v)
        {
            return v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        /// <summary>Builds the inputs; null with the reasons in issues when something needed is missing.</summary>
        public static GuideRailInputs Build(ElevatorProject p, DesignContext ctx, PresetLibrary lib, List<CalculationIssue> issues)
        {
            if (p == null || ctx == null || lib == null)
            {
                issues.Add(new CalculationIssue("Project", "Project, design context and preset library are required."));
                return null;
            }
            var preset = lib.GuideRailsOrDefault();
            var c = new PresetContext { Project = p, Library = lib, Design = ctx };
            var g = new GuideRailInputs { Preset = preset };
            double? q = D(Eff(c, "Elevator.CapacityKg")), car = D(Eff(c, "Calculations.CarMassKg")), cw = D(Eff(c, "Calculations.CounterweightMassKg"));
            double? h = D(Eff(c, "Calculations.CarGuideForces.GuideShoeSpacingMm"));
            double? pit = D(Eff(c, "Shaft.PitDepthMm")), overhead = D(Eff(c, "Shaft.OverheadMm"));
            string gear = Eff(c, "Calculations.SafetyPit.SafetyGearType") as string;
            Require(issues, q, "Elevator.CapacityKg", "Rated load");
            Require(issues, car, "Calculations.CarMassKg", "Car mass");
            Require(issues, cw, "Calculations.CounterweightMassKg", "Counterweight mass");
            Require(issues, h, "Calculations.CarGuideForces.GuideShoeSpacingMm", "Guide-shoe spacing h");
            Require(issues, pit, "Shaft.PitDepthMm", "Pit depth");
            Require(issues, overhead, "Shaft.OverheadMm", "Overhead");
            if (gear != ProjectPresets.Progressive && gear != ProjectPresets.Instantaneous)
                issues.Add(new CalculationIssue("SafetyPit.SafetyGearType", "Safety-gear type is required."));
            double travel = Travel(p, issues);
            var plan = Plan(p, ctx, issues);
            if (issues.Count > 0)
                return null;
            g.CapacityKg = q.Value;
            g.CarMassKg = car.Value + Additional(c, "Calculations.CarSideAdditionalSuspendedMassKg");
            g.CounterweightMassKg = cw.Value + Additional(c, "Calculations.CounterweightSideAdditionalSuspendedMassKg");
            g.ShoeSpacingMm = h.Value;
            g.CwShoeSpacingMm = preset.CwShoeSpacingMm;
            g.SafetyGearType = gear;
            g.K1 = gear == ProjectPresets.Progressive ? preset.ProgressiveK1 : preset.InstantaneousK1;
            g.CarDepthMm = plan.CarDepth;
            g.CarWidthMm = plan.CarWidth;
            g.CwDepthMm = plan.CwDepth;
            g.CwWidthMm = plan.CwWidth;
            g.TravelMm = travel;
            g.WellHeightMm = pit.Value + travel + overhead.Value;
            try
            {
                // FF1 / FF2 design values: the adopted note rule of the application (CarGuideForceCalculationEngine).
                var ff = CarGuideForceCalculationEngine.EvaluateNoteRule(g.CapacityKg, g.CarWidthMm, g.CarDepthMm, g.ShoeSpacingMm);
                g.Ff1DesignN = ff[1];
                g.Ff2DesignN = ff[3];
            }
            catch (ArgumentException e)
            {
                issues.Add(new CalculationIssue("FF.NumericalRange", e.Message));
                return null;
            }
            var railField = ProjectPresets.Find("Rails.MachineOnRails");
            var onRails = railField == null ? null : railField.Effective(c);
            g.MachineOnRails = onRails == null ? preset.MachineOnRails : Convert.ToBoolean(onRails);
            if (g.MachineOnRails && !Machine(p, c, lib, g, travel, overhead.Value, issues))
                return null;
            return g;
        }

        static void Require(List<CalculationIssue> issues, double? v, string field, string label)
        {
            if (!v.HasValue || Double.IsNaN(v.Value) || Double.IsInfinity(v.Value) || v.Value <= 0)
                issues.Add(new CalculationIssue(field, label + " is required."));
        }

        static double Additional(PresetContext c, string key)
        {
            var v = D(Eff(c, key));
            return v.HasValue && v.Value > 0 ? v.Value : 0;
        }

        static double Travel(ElevatorProject p, List<CalculationIssue> issues)
        {
            try
            {
                var served = ProjectFloors.Elevations(p.Building).Where(f => f.IsServed).ToList();
                if (served.Count < 2)
                    throw new ArgumentException("At least two served floors are required for the travel height.");
                return served.Last().ElevationMm - served.First().ElevationMm;
            }
            catch (ArgumentException e)
            {
                issues.Add(new CalculationIssue("Building.Floors", e.Message));
                return 0;
            }
        }

        static ElevatorParameters Plan(ElevatorProject p, DesignContext ctx, List<CalculationIssue> issues)
        {
            var source = CarGuideForceCalculationEngine.GeometryInputIssues(p, ctx);
            if (source.Count > 0)
            {
                issues.AddRange(source);
                return null;
            }
            try
            {
                return DesignRules.Calculate(p.Shaft.WidthMm, p.Shaft.DepthMm, ctx.Equipment, ctx.CounterweightDepthMm, DoorInput.From(p, ctx.Doors));
            }
            catch (ArgumentException e)
            {
                issues.Add(new CalculationIssue("Cabin.Geometry", e.Message));
                return null;
            }
        }

        // GUIDE-RAILS.md 13.4: k3·Maux = k·gn·(P + Q + Mmachine + Mrope) / n_m (Tavaslioglu, section D).
        static bool Machine(
            ElevatorProject p,
            PresetContext c,
            PresetLibrary lib,
            GuideRailInputs g,
            double travelMm,
            double overheadMm,
            List<CalculationIssue> issues)
        {
            double? machine = D(Eff(c, "Calculations.MachineMassKg")), ropes = D(Eff(c, ProjectPresets.RopeCountKey));
            double? w = D(PresetFields.RopeMass.Effective(c));
            Require(issues, machine, "Calculations.MachineMassKg", "Machine mass");
            Require(issues, ropes, ProjectPresets.RopeCountKey, "Number of ropes");
            Require(issues, w, "Calculations.RopeTraction.RopeMassKgPerM", "Rope mass per metre");
            if (issues.Count > 0)
                return false;
            double chassis = p.Calculations != null && p.Calculations.IncludeChassisSelfWeight == true && p.Calculations.ChassisMassKg.HasValue
                ? p.Calculations.ChassisMassKg.Value
                : 0;
            g.MachineMassKg = machine.Value + chassis;
            g.RopeMassKg = ropes.Value * w.Value * (travelMm + overheadMm) / 1000.0;
            g.MachineLoadMassKg = g.CarMassKg + g.CapacityKg + g.MachineMassKg + g.RopeMassKg;
            return true;
        }
    }
}
