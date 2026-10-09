using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Suspension
{
    // Connects the rope / traction results to the machine-room code rules (MR-ROPE-MIN-001, MR-ROPE-001, MR-TRACTION-001).
    public static class RopeTractionRules
    {
        public static void Apply(IList<EngineeringRule> rules, RopeTractionResult r)
        {
            if (rules == null || r == null)
                return;
            Func<string, RopeCheck> check = id => r.Checks.FirstOrDefault(c => c.Id == id);
            Action<string, string, string> set = (id, status, value) =>
            {
                var rule = rules.FirstOrDefault(x => x.RuleId == id);
                if (rule
                != null)
                {
                    rule.Status = status;
                    rule.CalculatedValue = value;
                }
            };
            var dia = check("rope-diameter");
            if (dia != null)
                set("MR-ROPE-MIN-001", dia.Ok == true ? "PASS" : "FAIL", dia.Actual);
            // MR-ROPE-001 (code rule record): rope ≥ 8 mm and D/d ≥ 40 for the sheave and, as a value, the deflector.
            var dd = new[] { dia, check("dd-sheave") }.Where(c => c != null).ToList();
            if (dia != null && check("dd-sheave") != null && (!r.Geometry.DeflectorPresent || r.DpOverDr.HasValue))
                set(
                    "MR-ROPE-001",
                    dd.All(c => c.Ok == true) && (!r.DpOverDr.HasValue || r.DpOverDr.Value >= 40 - 1e-9) ? "PASS" : "FAIL",
                    String.Join("; ", dd.Select(
                    c => c.Title + " " + c.Actual)) + (r.DpOverDr.HasValue
                        ? "; Deflector D/d " + r.DpOverDr.Value.ToString("0.##", CultureInfo.InvariantCulture)
                        : ""));
            var sf = check("safety-factor");
            bool tractionComplete = r.Traction.Any(t => t.Id == "loading")
                && r.Traction.Any(t => t.Id == "braking")
                && (r.Traction.Any(t => t.Id == "stalled") || r.Omissions.Any(o => o.Kind == "NOT_APPLICABLE"));
            if (sf != null && tractionComplete)
                set(
                    "MR-TRACTION-001",
                    sf.Ok == true && r.Traction.All(t => t.Ok) ? "PASS" : "FAIL",
                    "Sf " + sf.Actual + " (required " + sf.Required + "); " + String.Join("; ", r.Traction.Select(
                    t => t.Title
                        + " "
                        + t.Ratio.ToString("0.###", CultureInfo.InvariantCulture)
                        + " "
                        + t.Condition
                        + " "
                        + t.EFAlpha.ToString("0.###", CultureInfo.InvariantCulture))));
        }
    }
}
