using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The guide-rail check of EN 81-50:2014 5.10 for the car rails (running, loading, safety device operation) and the counterweight
    /// rails (normal operation), with the load cases of EN 81-20:2014 Table 13. The equations and the choices are in
    /// docs/calculations/GUIDE-RAILS.md sections 4 to 7 and 13. Adopted source rules, not certification.
    /// </summary>
    public static class GuideRailCheckEngine
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string Running = "running";
        public const string Loading = "loading";
        public const string SafetyDevice = "safety";

        static string F(double v)
        {
            return v.ToString("0.###", Inv);
        }

        static string N(double v)
        {
            return F(v) + " N (" + LoadUnits.KgWithUnit(v) + ")";
        }

        /// <summary>Calculates the check of a project with the effective profiles and spacings of its rail fields.</summary>
        public static GuideRailCheckResult Calculate(ElevatorProject p, DesignContext ctx, PresetLibrary lib)
        {
            var r = new GuideRailCheckResult();
            var g = GuideRailInputs.Build(p, ctx, lib, r.Issues);
            if (g == null)
                return r;
            r.Inputs = g;
            var c = new PresetContext { Project = p, Library = lib, Design = ctx };
            RailProfile carProfile = Profile(c, "Rails.CarProfile", g, r.Issues), cwProfile = Profile(c, "Rails.CwProfile", g, r.Issues);
            double? carSpacing = Spacing(c, "Rails.CarSpacingMm", r.Issues), cwSpacing = Spacing(c, "Rails.CwSpacingMm", r.Issues);
            if (r.Issues.Count > 0)
                return r;
            r.Car = Line(g, true, carProfile, carSpacing.Value);
            r.Counterweight = Line(g, false, cwProfile, cwSpacing.Value);
            r.Status = r.Ok ? "OK" : "NOT_OK";
            return r;
        }

        static RailProfile Profile(PresetContext c, string key, GuideRailInputs g, List<CalculationIssue> issues)
        {
            string name = ProjectPresets.Find(key).Effective(c) as string;
            var profile = String.IsNullOrWhiteSpace(name) ? null : g.Preset.Profile(name);
            if (profile == null)
                issues.Add(new CalculationIssue(key, "Rail profile " + (name ?? "") + " is not in the catalog."));
            return profile;
        }

        static double? Spacing(PresetContext c, string key, List<CalculationIssue> issues)
        {
            // A steel shaft: the rails are bracketed on the beams of the side and back faces, so the check uses their largest gap
            // (not more than the typed or recommended spacing by construction of the beam levels).
            var derived = Steel.SteelStructure.CheckSpacing(c);
            if (derived.HasValue && derived.Value > 0)
                return derived.Value;
            var v = ProjectPresets.Find(key).Effective(c);
            double d = v == null ? 0 : Convert.ToDouble(v, Inv);
            if (v == null || Double.IsNaN(d) || Double.IsInfinity(d) || d <= 0)
            {
                issues.Add(new CalculationIssue(key, "Bracket spacing is required."));
                return null;
            }
            return d;
        }

        /// <summary>One rail line (car or counterweight) with a profile and a bracket spacing.</summary>
        public static RailLineResult Line(GuideRailInputs g, bool car, RailProfile profile, double spacingMm)
        {
            var pr = g.Preset;
            profile = g.ForLine(car, profile);
            var line = new RailLineResult { Name = car ? "Car" : "Counterweight", Profile = profile, SpacingMm = spacingMm };
            line.LineLengthMm = g.WellHeightMm;
            line.LineMassKg = profile.MassKgPerM * g.WellHeightMm / 1000.0;
            if (g.TravelMm / 1000.0 > pr.PushThroughMaxTravelM + 1e-9)
            {
                // Fp = nb·Fr for rails standing in the pit (car); nb·Fr / 3 for rails not secured at any point (counterweight).
                line.BracketCount = (int)Math.Ceiling(g.WellHeightMm / spacingMm - 1e-9) + 1;
                line.PushThroughN = line.BracketCount * pr.ClipPushThroughN / (car ? 1.0 : 3.0);
            }
            double mgN = line.LineMassKg * GuideRailMath.Gn;
            string mgText = "Mg·gn + Fp = " + F(line.LineMassKg) + " × 9.81 + " + F(line.PushThroughN);
            double fvRail = mgN + line.PushThroughN;
            if (car)
            {
                line.Cases.Add(Evaluate(RunningCase(g, profile, spacingMm, fvRail, mgText), pr));
                line.Cases.Add(Evaluate(LoadingCase(g, profile, spacingMm, fvRail, mgText), pr));
                line.Cases.Add(Evaluate(SafetyCase(g, profile, spacingMm, fvRail, mgText), pr));
            }
            else
                line.Cases.Add(Evaluate(CounterweightCase(g, profile, spacingMm, fvRail, mgText), pr));
            return line;
        }

        static RailCaseInput Base(string id, string title, string clause, RailProfile profile, double l, bool safety, double dperm, double fv, string fvEquation)
        {
            double? st = profile.SafetyFactor(safety);
            return new RailCaseInput
            {
                Id = id,
                Title = title,
                Clause = clause,
                Profile = profile,
                SpacingMm = l,
                SafetyDevice = safety,
                DeflectionPermMm = dperm,
                SafetyFactor = st ?? Double.NaN,
                FvN = fv,
                FvEquation = fvEquation
            };
        }

        // Car, normal operation, running (EN 81-50 C.2.2.1, P and S at the rail cross): Dim/8 eccentricity, kr = 1.2.
        static RailCaseInput RunningCase(GuideRailInputs g, RailProfile profile, double l, double fvRail, string mgText)
        {
            var pr = g.Preset;
            var input = Base(Running, "Normal operation, running", "EN 81-20 5.7.2.2 Table 13; EN 81-50 C.2.2", profile, l, false,
                pr.CarDeflectionLimitMm, fvRail, "Fv = " + mgText);
            double kr = pr.RunningImpactFactor, n = pr.RailCount, h = g.ShoeSpacingMm;
            double fx = kr * GuideRailMath.Gn * g.CapacityKg * (g.CarDepthMm / 8.0) / (n * h);
            double fy = kr * GuideRailMath.Gn * g.CapacityKg * (g.CarWidthMm / 8.0) / ((n / 2.0) * h);
            input.Forces.Add(new RailForce
            {
                Name = "load Dx/8 off in X",
                FxN = fx,
                FyN = 0,
                FxEquation = "Fx = kr·gn·Q·(Dx/8) / (n·h) = " + F(kr) + " × 9.81 × " + F(g.CapacityKg) + " × (" + F(g.CarDepthMm) + " / 8) / (" + F(n) + " × " + F(h) + ")",
                FyEquation = "Fy = 0"
            });
            input.Forces.Add(new RailForce
            {
                Name = "load Dy/8 off in Y",
                FxN = 0,
                FyN = fy,
                FxEquation = "Fx = 0",
                FyEquation = "Fy = kr·gn·Q·(Dy/8) / ((n/2)·h) = " + F(kr) + " × 9.81 × " + F(g.CapacityKg) + " × (" + F(g.CarWidthMm) + " / 8) / (" + F(n / 2.0) + " × " + F(h) + ")"
            });
            SetAux(input, g, "kr", pr.RunningImpactFactor);
            return input;
        }

        // Car, loading and unloading (EN 81-50 C.2.3.1): the car is empty, Fs = 0.4·gn·Q at the centred front entrance, xi = Dx/2.
        static RailCaseInput LoadingCase(GuideRailInputs g, RailProfile profile, double l, double fvRail, string mgText)
        {
            var pr = g.Preset;
            var input = Base(Loading, "Normal operation, loading", "EN 81-20 5.7.2.2 Table 13, 5.7.2.3.6; EN 81-50 C.2.3", profile, l, false,
                pr.CarDeflectionLimitMm, fvRail, "Fv = " + mgText);
            double fs = pr.SillForceFactor * GuideRailMath.Gn * g.CapacityKg, n = pr.RailCount, h = g.ShoeSpacingMm;
            double fx = fs * (g.CarDepthMm / 2.0) / (n * h);
            input.Forces.Add(new RailForce
            {
                Name = "sill force at the front entrance",
                FxN = fx,
                FyN = 0,
                FxEquation = "Fx = Fs·(Dx/2) / (n·h) = " + F(fs) + " × (" + F(g.CarDepthMm) + " / 2) / (" + F(n) + " × " + F(h) + "), Fs = " + F(pr.SillForceFactor) + " × 9.81 × " + F(g.CapacityKg),
                FyEquation = "Fy = 0"
            });
            SetAux(input, g, "kr", pr.RunningImpactFactor);
            return input;
        }

        // Car, safety device operation: the FF1 / FF2 design values of the application (FF1 = Fy, FF2 = Fx).
        static RailCaseInput SafetyCase(GuideRailInputs g, RailProfile profile, double l, double fvRail, string mgText)
        {
            var pr = g.Preset;
            double n = pr.RailCount;
            double fvSafety = g.K1 * GuideRailMath.Gn * (g.CarMassKg + g.CapacityKg) / n + fvRail;
            var input = Base(SafetyDevice, "Safety device operation", "EN 81-20 5.7.2.2 Table 13, 5.7.2.3.5; EN 81-50 C.2.1", profile, l, true,
                pr.CarDeflectionLimitMm, fvSafety,
                "Fv = k1·gn·(P + Q) / n + " + mgText + " = " + F(g.K1) + " × 9.81 × (" + F(g.CarMassKg) + " + " + F(g.CapacityKg) + ") / " + F(n) + " + " + F(fvRail));
            input.Forces.Add(new RailForce
            {
                Name = "FF1 / FF2",
                FxN = g.Ff2DesignN,
                FyN = g.Ff1DesignN,
                FxEquation = "Fx = FF2 = " + F(g.Ff2DesignN) + " N",
                FyEquation = "Fy = FF1 = " + F(g.Ff1DesignN) + " N"
            });
            SetAux(input, g, "k1", g.K1);
            return input;
        }

        // Counterweight rails, normal operation (EN 81-20 5.7.2.3.3 eccentricities 10 % of the depth and 5 % of the width, GUIDE-RAILS.md 13.5).
        static RailCaseInput CounterweightCase(GuideRailInputs g, RailProfile profile, double l, double fvRail, string mgText)
        {
            var pr = g.Preset;
            var input = Base(Running, "Normal operation, counterweight", "EN 81-20 5.7.2.3.3; EN 81-50 5.10", profile, l, false,
                pr.CwDeflectionLimitMm, fvRail, "Fv = " + mgText);
            double kr = pr.RunningImpactFactor, n = pr.RailCount, h = g.CwShoeSpacingMm;
            double ex = g.CwDepthMm * pr.CwEccentricityDepthPercent / 100.0, ey = g.CwWidthMm * pr.CwEccentricityWidthPercent / 100.0;
            double fx = kr * GuideRailMath.Gn * g.CounterweightMassKg * ex / (n * h);
            double fy = kr * GuideRailMath.Gn * g.CounterweightMassKg * ey / ((n / 2.0) * h);
            input.Forces.Add(new RailForce
            {
                Name = "eccentric counterweight",
                FxN = fx,
                FyN = fy,
                FxEquation = "Fx = kr·gn·Mcwt·ex / (n·hcw) = " + F(kr) + " × 9.81 × " + F(g.CounterweightMassKg) + " × " + F(ex) + " / (" + F(n) + " × " + F(h) + "), ex = " + F(pr.CwEccentricityDepthPercent) + " % × " + F(g.CwDepthMm),
                FyEquation = "Fy = kr·gn·Mcwt·ey / ((n/2)·hcw) = " + F(kr) + " × 9.81 × " + F(g.CounterweightMassKg) + " × " + F(ey) + " / (" + F(n / 2.0) + " × " + F(h) + "), ey = " + F(pr.CwEccentricityWidthPercent) + " % × " + F(g.CwWidthMm)
            });
            SetAux(input, g, "kr", pr.RunningImpactFactor);
            return input;
        }

        // k3·Maux = k·gn·(P + Q + Mmachine + Mrope) / n_m: k = k1 in the safety-device case; the running factor kr (an adopted choice)
        // in running, loading and the counterweight rails. Added to the vertical force of every rail that carries the machine frame.
        static void SetAux(RailCaseInput input, GuideRailInputs g, string symbol, double factor)
        {
            if (g.MachineLoadMassKg <= 0)
                return;
            int nm = g.Preset.MachineRailCount;
            input.AuxN = factor * GuideRailMath.Gn * g.MachineLoadMassKg / nm;
            input.AuxEquation = "k3·Maux = " + symbol + "·gn·(P + Q + Mmachine + Mrope) / nm = " + F(factor) + " × 9.81 × (" + F(g.CarMassKg) + " + " + F(g.CapacityKg)
                + " + " + F(g.MachineMassKg) + " + " + F(g.RopeMassKg) + ") / " + F(nm) + " = " + N(input.AuxN);
        }

        sealed class Sub
        {
            public RailForce Force;
            public double Sx, Sy, Sm, SigmaF, Dx, Dy;
        }

        /// <summary>Evaluates one load case: every item with its value, permissible value and result.</summary>
        public static RailLoadCaseResult Evaluate(RailCaseInput c, GuideRailPreset pr)
        {
            var pf = c.Profile;
            double l = c.SpacingMm, e = pr.ElasticModulusNmm2;
            double sigmaPerm = pf.RmNmm2 / c.SafetyFactor;
            var result = new RailLoadCaseResult
            {
                Id = c.Id,
                Title = c.Title,
                Clause = c.Clause,
                SafetyFactor = c.SafetyFactor,
                SigmaPermNmm2 = sigmaPerm,
                FvN = c.FvN,
                AuxN = c.AuxN,
                AuxEquation = c.AuxEquation
            };
            var subs = new List<Sub>();
            foreach (var f in c.Forces)
            {
                var s = new Sub { Force = f };
                s.Sy = GuideRailMath.BendingStress(f.FxN, l, pf.WyyMm3);
                s.Sx = GuideRailMath.BendingStress(f.FyN, l, pf.WxxMm3);
                s.Sm = s.Sx + s.Sy;
                s.SigmaF = GuideRailMath.FlangeBending(pf, f.FxN);
                s.Dx = GuideRailMath.Deflection(f.FxN, l, e, pf.IyyMm4) + pr.StructureDeflectionXMm;
                s.Dy = GuideRailMath.Deflection(f.FyN, l, e, pf.IxxMm4) + pr.StructureDeflectionYMm;
                subs.Add(s);
            }
            Func<Func<Sub, double>, Sub> worst = key => subs.OrderByDescending(key).First();
            var sx = worst(s => s.Sx);
            var sy = worst(s => s.Sy);
            var sm = worst(s => s.Sm);
            var sf = worst(s => s.SigmaF);
            var dx = worst(s => s.Dx);
            var dy = worst(s => s.Dy);
            var fxs = worst(s => s.Force.FxN);
            var fys = worst(s => s.Force.FyN);
            double lambda = l / pf.MinimumRadiusMm, lambdaUsed = Math.Max(lambda, pr.MinSlenderness);
            double omega = GuideRailMath.Omega(lambdaUsed, pf.RmNmm2);
            double axial = c.FvN + c.AuxN;
            double sigmaK = axial * omega / pf.AreaMm2;
            double sigmaComp = sm.Sm + axial / pf.AreaMm2;
            double sigmaBuck = sigmaK + 0.9 * sm.Sm;
            string aux = F(c.AuxN);
            Action<string, string, string, string, double, double?, string, bool> add =
                (id, title, clause, equation, value, perm, unit, primary) =>
            {
                result.Items.Add(new RailItem
                {
                    Id = id,
                    Title = title,
                    Clause = clause,
                    Equation = equation,
                    Value = value,
                    Permissible = perm,
                    Unit = unit,
                    Ok = perm.HasValue ? (bool?)(value <= perm.Value + 1e-9) : null,
                    Primary = primary
                });
            };
            add("Fx", "Guiding force Fx", "EN 81-50 C.2", fxs.Force.FxEquation + " = " + N(fxs.Force.FxN), fxs.Force.FxN, null, "N", true);
            add("Fy", "Guiding force Fy", "EN 81-50 C.2", fys.Force.FyEquation + " = " + N(fys.Force.FyN), fys.Force.FyN, null, "N", true);
            add("Fv", "Vertical force Fv", "EN 81-20 5.7.2.3.5", c.FvEquation + " = " + N(c.FvN), c.FvN, null, "N", true);
            add("sx", "Bending stress σx", "EN 81-50 5.10.2",
                "σx = 3·Fy·l / (16·Wx) = 3 × " + F(sx.Force.FyN) + " × " + F(l) + " / (16 × " + F(pf.WxxMm3) + ")", sx.Sx, sigmaPerm, "N/mm²", false);
            add("sy", "Bending stress σy", "EN 81-50 5.10.2",
                "σy = 3·Fx·l / (16·Wy) = 3 × " + F(sy.Force.FxN) + " × " + F(l) + " / (16 × " + F(pf.WyyMm3) + ")", sy.Sy, sigmaPerm, "N/mm²", false);
            add("sm", "Bending stress σm", "EN 81-50 5.10.4.1", SigmaMEquation(subs, sm), sm.Sm, sigmaPerm, "N/mm²", true);
            add("lambda", "Slenderness λ", "EN 81-50 5.10.3",
                "λ = lk / i = " + F(l) + " / " + F(pf.MinimumRadiusMm) + (lambda < pr.MinSlenderness ? ", calculated with λ = " + F(pr.MinSlenderness) : ""),
                lambda, pr.MaxSlenderness, "", true);
            add("omega", "Omega ω", "EN 81-50 5.10.3", OmegaEquation(lambdaUsed, pf.RmNmm2, omega), omega, null, "", false);
            add("sk", "Buckling stress σk", "EN 81-50 5.10.3",
                "σk = (Fv + k3·Maux)·ω / A = (" + F(c.FvN) + " + " + aux + ") × " + F(omega) + " / " + F(pf.AreaMm2), sigmaK, sigmaPerm, "N/mm²", false);
            add("scomp", "Bending and compression σ", "EN 81-50 5.10.4.2",
                "σ = σm + (Fv + k3·Maux) / A = " + F(sm.Sm) + " + (" + F(c.FvN) + " + " + aux + ") / " + F(pf.AreaMm2), sigmaComp, sigmaPerm, "N/mm²", true);
            add("sbuck", "Bending and buckling σ", "EN 81-50 5.10.4.3",
                "σ = σk + 0.9·σm = " + F(sigmaK) + " + 0.9 × " + F(sm.Sm), sigmaBuck, sigmaPerm, "N/mm²", true);
            add("sf", "Flange bending σF", "EN 81-50 5.10.5", FlangeEquation(pf, sf.Force.FxN), sf.SigmaF, sigmaPerm, "N/mm²", true);
            add("dx", "Deflection δx", "EN 81-50 5.10.6",
                "δx = 0.7·Fx·l³ / (48·E·Iy) + δstr-x = 0.7 × " + F(dx.Force.FxN) + " × " + F(l) + "³ / (48 × " + F(e) + " × " + F(pf.IyyMm4) + ") + " + F(pr.StructureDeflectionXMm),
                dx.Dx, c.DeflectionPermMm, "mm", true);
            add("dy", "Deflection δy", "EN 81-50 5.10.6",
                "δy = 0.7·Fy·l³ / (48·E·Ix) + δstr-y = 0.7 × " + F(dy.Force.FyN) + " × " + F(l) + "³ / (48 × " + F(e) + " × " + F(pf.IxxMm4) + ") + " + F(pr.StructureDeflectionYMm),
                dy.Dy, c.DeflectionPermMm, "mm", true);
            foreach (var i in result.Items)
                i.Equation = i.Equation + (i.IsForce ? "" : " = " + F(i.Value) + (i.Unit == "" ? "" : " " + i.Unit));
            return result;
        }

        static string SigmaMEquation(List<Sub> subs, Sub governing)
        {
            if (subs.Count == 1)
                return "σm = σx + σy = " + F(governing.Sx) + " + " + F(governing.Sy);
            return "σm = max(" + String.Join("; ", subs.Select(s => F(s.Sx) + " + " + F(s.Sy)).ToArray()) + ")";
        }

        static string OmegaEquation(double lambda, double rm, double omega)
        {
            if (rm <= 370)
            {
                string x = F(lambda);
                if (lambda <= 60)
                    return "ω = 0.00012920 × " + x + "^1.89 + 1";
                if (lambda <= 85)
                    return "ω = 0.00004627 × " + x + "^2.14 + 1";
                if (lambda <= 115)
                    return "ω = 0.00001711 × " + x + "^2.35 + 1.04";
                return "ω = 0.00016887 × " + x + "²";
            }
            return "ω = ω370 + (ω520 − ω370)·(Rm − 370) / (520 − 370), λ = " + F(lambda) + ", Rm = " + F(rm);
        }

        static string FlangeEquation(RailProfile p, double fx)
        {
            if (p.ShoeType == RailProfile.Roller)
                return "σF = 1.85·Fx / c² = 1.85 × " + F(fx) + " / " + F(p.NeckMm) + "²";
            return "σF = 6·Fx·(h1 − b − f) / (c²·(lL + 2·(h1 − f))) = 6 × " + F(fx) + " × (" + F(p.HeightMm) + " − " + F(p.ShoeHalfWidthMm) + " − " + F(p.FootMm)
                + ") / (" + F(p.NeckMm) + "² × (" + F(p.ShoeLiningLengthMm) + " + 2 × (" + F(p.HeightMm) + " − " + F(p.FootMm) + ")))";
        }
    }
}
