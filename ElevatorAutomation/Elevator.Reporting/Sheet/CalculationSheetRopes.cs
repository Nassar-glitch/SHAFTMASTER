using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Suspension;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Reporting.Sheet
{
    // "Suspension rope and traction checks" section (EN 81-20 5.5; EN 81-50 5.11 / 5.12). Clean client format: equation,
    // substitution, required, actual, OK / NOT OK. Remarks and explanations go to the Design Notes file only.
    public sealed partial class CalculationSheet
    {
        public RopeTractionResult RopeTraction { get; private set; }
        public PresetLibrary Presets { get; private set; }

        // NOT OK results, listed for the user before export (application only).
        public List<string> NotOk { get; private set; }

        // Present when a substantive check exists (D/d, safety factor or traction), not for a rope diameter / count alone.
        public bool RopeTractionAvailable
        {
            get
            {
                return RopeTraction != null
                    && (RopeTraction.Traction.Count > 0 || RopeTraction.Checks.Any(c => c.Id == "safety-factor" || c.Id.StartsWith("dd-")));
            }
        }

        public const string RopeTractionSectionTitle = "Suspension rope and traction checks";

        public static PresetLibrary LoadPresets(string folder)
        {
            try
            {
                var path = Path.Combine(folder ?? AppDomain.CurrentDomain.BaseDirectory, "presets.json");
                return File.Exists(path) ? PresetLibrary.Load(path) : null;
            }
            catch (ArgumentException) { return null; }
        }

        void BuildRopeTraction(DesignContext context)
        {
            Presets = Inputs.Presets ?? LoadPresets(null);
            RopeTraction = new RopeTractionCalculationEngine().Calculate(Project, context, Presets);
            NotOk = new List<string>();
            foreach (var c in RopeTraction.Checks.Where(c => c.Ok == false))
                NotOk.Add(c.Title + ": " + c.Actual + " (required " + c.Required + ")");
            foreach (var t in RopeTraction.Traction.Where(t => !t.Ok))
                NotOk.Add("Traction, "
                    + (t.Title.Substring(0, 1).ToLowerInvariant() + t.Title.Substring(1))
                    + ": T1/T2 "
                    + t.Ratio.ToString("0.###", Inv)
                    + " (required "
                    + t.Condition
                    + " "
                    + t.EFAlpha.ToString("0.###", Inv)
                    + ")");
            var omitted = RopeTraction.Omissions.Where(o => o.Kind != "NOT_APPLICABLE").ToList();
            if (omitted.Count > 0)
            {
                var o = new OmittedCalculation { Name = RopeTractionSectionTitle + (RopeTractionAvailable ? " (part)" : "") };
                foreach (var x in omitted)
                    o.Reasons.Add(x.Item + ": " + x.Reason);
                Omitted.Add(o);
            }
        }

        static string L3(double v)
        {
            return v.ToString("0.###", Inv);
        }

        static string Res(bool? ok)
        {
            return ok == null ? "" : ok.Value ? "OK" : "NOT OK";
        }

        static string G(string groove)
        {
            return GrooveTypes.Label(groove);
        }

        string RopeSummary()
        {
            var r = RopeTraction;
            var parts = new List<string>();
            var sf = r.Checks.FirstOrDefault(c => c.Id == "safety-factor");
            if (sf != null)
                parts.Add(
                    "Rope Sf " + L(sf.ActualValue.Value) + (sf.Ok == true ? " ≥ " : " < ") + L(sf.RequiredValue.Value) + " " + Res(sf.Ok));
            var dd = r.Checks.Where(c => c.Id.StartsWith("dd-")).ToList();
            if (dd.Count > 0)
                parts.Add("D/d " + (dd.All(c => c.Ok == true) ? "OK" : "NOT OK"));
            var basic = r.Checks.Where(c => c.Id == "rope-diameter" || c.Id == "rope-count").ToList();
            if (basic.Count > 0 && basic.Any(c => c.Ok == false))
                parts.Add("rope diameter / count NOT OK");
            if (r.Traction.Count > 0)
                parts.Add("traction " + (r.Traction.All(t => t.Ok) ? "OK" : "NOT OK"));
            return String.Join("; ", parts);
        }

        void RopeTractionInputRows(List<string[]> rows)
        {
            // Only the inputs used by the rope / traction checks that appear in this document.
            var r = RopeTraction;
            var x = r.Inputs;
            var g = r.Geometry;
            bool sf = r.TmaxN.HasValue, traction =
                r.Traction.Count > 0, braking = r.Traction.Any(t => t.Id == "braking"), stalled = r.Traction.Any(
                t => t.Id == "stalled"), term = r.Checks.Any(c => c.Id == "termination"), dd = r.Checks.Any(c => c.Id.StartsWith("dd-"));
            Action<string, double?, string> num =
                (label, v, unit) => { if (Positive(v)) rows.Add(new[] { label, L(v.Value) + (unit == null ? "" : " " + unit) }); };
            if (!String.IsNullOrWhiteSpace(x.RopeDesignation) && (sf || term))
                rows.Add(new[] { "Rope designation", x.RopeDesignation });
            num("Rope nominal diameter d", x.RopeDiameterMm, "mm");
            if ((sf || traction)
                && x.RopeCount.HasValue
                && x.RopeCount.Value > 0
                && !rows.Any(rw => rw[0] == "Number of suspension ropes n"))
                rows.Add(new[] { "Number of suspension ropes n", x.RopeCount.Value.ToString(Inv) });
            if (sf || term)
                num("Rope minimum breaking load MBL", x.MinimumBreakingLoadKn, "kN");
            if ((sf || traction) && Positive(x.RopeMassKgPerM))
                rows.Add(new[] { "Rope mass per metre w", L3(x.RopeMassKgPerM.Value) + " kg/m" });
            if (dd || sf)
                num("Traction sheave pitch diameter Dt", x.SheavePitchDiameterMm, "mm");
            if ((sf || traction) && x.GrooveType != null)
            {
                rows.Add(new[] { "Traction sheave groove", G(x.GrooveType) });
                if (GrooveTypes.NeedsUndercut(x.GrooveType) && x.UndercutAngleDeg.HasValue)
                    rows.Add(new[] { "Undercut angle β", L(x.UndercutAngleDeg.Value) + "°" });
                if (GrooveTypes.NeedsGrooveAngle(x.GrooveType) && x.GrooveAngleDeg.HasValue)
                    rows.Add(new[] { "Groove angle γ", L(x.GrooveAngleDeg.Value) + "°" });
            }
            if (traction)
                num("Travelling cable mass per metre", x.TravellingCableKgPerM, "kg/m");
            if (braking)
            {
                num("Rated speed v", x.SpeedMps, "m/s");
                num("Emergency braking retardation a", x.BrakingRetardationMps2, "m/s²");
                if (g.DeflectorPresent)
                    num("Deflector sheave mass", x.DeflectorMassKg, "kg");
            }
            if (stalled && x.TractionLimitingMethod != null)
                rows.Add(new[] { "Traction limiting method", TractionLimitingMethods.Label(x.TractionLimitingMethod) });
            if (sf || traction)
                num("Overhead", x.OverheadMm, "mm");
        }

        void RopeTractionSection(DocxDocument d, int n)
        {
            var r = RopeTraction;
            var x = r.Inputs;
            var g = r.Geometry;
            d.Heading(n + "  " + RopeTractionSectionTitle, 1);
            d.Flow = true; // the section's equation blocks and tables may flow across pages
            d.Paragraph("Suspension ropes and traction of the 1:1 rope drive "
                + (g.DeflectorPresent ? "with deflector sheave" : "without deflector sheave")
                + " (EN 81-20:2014 5.5; EN 81-50:2014 5.11 and 5.12).", null, null, true);
            // Geometry used by the checks.
            var geo = new List<string[]>();
            if (r.TravelM.HasValue)
                geo.Add(new[] { "Travel height", "Htr", L(r.TravelM.Value) + " m" });
            if (r.HangingLengthM.HasValue)
                geo.Add(new[] { "Suspended rope length (travel + overhead)", "H", L(r.HangingLengthM.Value) + " m" });
            if (g.DeflectorPresent && g.DeflectorPitchMm.HasValue)
                geo.Add(new[] { "Deflector sheave pitch diameter", "Dp", L(g.DeflectorPitchMm.Value) + " mm" });
            if (g.WrapAngleDeg.HasValue && (!g.DeflectorPresent || g.DeflectorPathValid))
                geo.Add(new[] { "Angle of wrap on the traction sheave", "α", L(g.WrapAngleDeg.Value) + "°" });
            if (r.MdpKg.HasValue)
                geo.Add(new[] { "Reduced mass of the deflector sheave (m/2)", "mDP", L(r.MdpKg.Value) + " kg" });
            if (geo.Count > 0)
                d.Table(new[] { "Quantity", "Symbol", "Value" }, geo, new[] { 5200, 1400, 3038 }, new HashSet<int> { 0 });
            // Rope checks.
            d.Paragraph("Rope checks", "Heading2");
            var lines = new List<string>();
            if (r.TmaxN.HasValue)
            {
                lines.Add("Tmax = ((P + Q) / n + H · w) · gn = (("
                    + L(x.CarMassKg.Value)
                    + " + "
                    + L(x.CapacityKg.Value)
                    + ") / "
                    + x.RopeCount.Value
                    + " + "
                    + L(r.HangingLengthM.Value)
                    + " × "
                    + L3(x.RopeMassKgPerM.Value)
                    + ") × 9.81 = "
                    + L(r.TmaxN.Value)
                    + " N");
                lines.Add("Sf = MBL / Tmax = "
                    + L(x.MinimumBreakingLoadKn.Value * 1000)
                    + " / "
                    + L(r.TmaxN.Value)
                    + " = "
                    + L(r.SfActual.Value));
                lines.Add("Nequiv(t) = "
                    + L(r.NequivT.Value)
                    + " (EN 81-50 Table 2, "
                    + G(x.GrooveType)
                    + (GrooveTypes.NeedsUndercut(x.GrooveType)
                    && x.GrooveType == GrooveTypes.UndercutU
                        ? ", β = " + L(x.UndercutAngleDeg.Value) + "°"
                        : ", γ = " + L(x.GrooveAngleDeg.Value) + "°") + ")");
                if (g.DeflectorPresent)
                    lines.Add("Kp = (Dt / Dp)^4 = ("
                        + L(x.SheavePitchDiameterMm.Value)
                        + " / "
                        + L(g.DeflectorPitchMm.Value)
                        + ")^4 = "
                        + r.Kp.Value.ToString("0.####", Inv)
                        + ";   Nequiv(p) = Kp · (Nps + 4 · Npr) = "
                        + r.Kp.Value.ToString("0.####", Inv)
                        + " × (1 + 4 × 0) = "
                        + r.NequivP.Value.ToString("0.####", Inv));
                lines.Add("Nequiv = Nequiv(t) + Nequiv(p) = " + r.Nequiv.Value.ToString("0.####", Inv));
                lines.Add("Sf(EN 81-50) = 10^(2.6834 − log(695.85·10⁶ · Nequiv / (Dt/d)^8.567) / log(77.09 · (Dt/d)^−2.894)), Dt/d = "
                    + L(r.DtOverDr.Value)
                    + "  →  Sf(EN 81-50) = "
                    + L(r.Sf8150.Value));
                lines.Add("Sf required = max(" + L(r.SfMinimum8120.Value) + "; " + L(r.Sf8150.Value) + ") = " + L(r.SfRequired.Value));
            }
            var term = r.Checks.FirstOrDefault(c => c.Id == "termination");
            if (term != null)
                lines.Add("Ftermination ≥ 0.8 × MBL = " + term.Substitution);
            if (lines.Count > 0)
                d.Equations(true, lines);
            var rows = r.Checks
                .Where(c => c.Id != "termination")
                .Select(c => new[] { c.Title, c.Clause, c.Equation, c.Required, c.Actual, Res(c.Ok) })
                .ToList();
            if (term != null)
                rows.Add(new[] { term.Title,
                    term.Clause,
                    term.Equation,
                    "0.8 × MBL = " + L(term.RequiredValue.Value) + " kN",
                    "MBL = " + L(x.MinimumBreakingLoadKn.Value) + " kN",
                    "Required strength" });
            d.Table(
                new[] { "Check", "Clause", "Equation", "Required", "Actual", "Result" },
                rows,
                new[] { 1900, 1550, 2000, 1400, 1400, 1388 },
                new HashSet<int> { 0, 5 });
            // Traction.
            if (r.Traction.Count > 0)
            {
                d.Paragraph("Traction", "Heading2");
                d.Equations(
                    true,
                    new[]{"T1 / T2 ≤ e^(f·α) for car loading and emergency braking;   T1 / T2 ≥ e^(f·α) for the stalled condition (EN "
                        + "81-50 5.11.2.1)",
     "α = "+L(r.Traction[0].AlphaDeg)+"° = "+(r.Traction[0].AlphaDeg*Math.PI/180).ToString("0.####",Inv)+" rad;   gn = 9.81 m/s²"});
                foreach (var t in r.Traction)
                {
                    d.Paragraph(t.Title + " — " + t.Clause, "Heading3", null, true);
                    var eq = new List<string> { t.MuEquation, t.FEquation + " = " + t.F.ToString("0.####", Inv), "e^(f·α) = e^("
                        + t.F.ToString("0.####", Inv)
                        + " × "
                        + (t.AlphaDeg * Math.PI / 180).ToString("0.####", Inv)
                        + ") = "
                        + t.EFAlpha.ToString("0.###", Inv) };
                    foreach (var q in t.Positions)
                    {
                        eq.Add(q.Name + ":");
                        eq.Add("   " + q.T1Equation);
                        eq.Add("   " + q.T2Equation);
                    }
                    d.Equations(true, eq);
                    d.Table(
                        new[] { "Position", "T1 (N)", "T2 (N)", "T1 / T2", "Required", "Result" },
                        t.Positions.Select(q => new[] { q.Name,
                        L(q.T1N),
                        L(q.T2N),
                        q.Ratio.ToString("0.###", Inv),
                        t.Condition + " " + t.EFAlpha.ToString("0.###", Inv),
                        (t.Condition == "≤" ? q.Ratio <= t.EFAlpha * (1 + 1e-12) : q.Ratio >= t.EFAlpha * (1 - 1e-12)) ? "OK" : "NOT OK" })
                            .ToList(), new[] { 3438, 1300, 1300, 1100, 1300, 1200 }, new HashSet<int> { 0, 5 });
                }
            }
            d.Flow = false;
            if (x.GrooveType != null && (r.Traction.Count > 0 || r.TmaxN.HasValue))
                AddFigure(d, () => CalculationFigures.TractionSheave(r));
        }
    }
}
