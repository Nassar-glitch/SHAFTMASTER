using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Elevator.Core
{
    public static class PlanAnnotationRules
    {
        public static Dictionary<string, string> Values(ProjectInput input, ElevatorParameters p, Dictionary<string, double> clearances)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            values["ProjectName"] = input.ProjectName ?? "";
            values["PlanTitle"] = String.IsNullOrWhiteSpace(input.PlanTitle) ? "SHAFT AND CAR" : input.PlanTitle;
            values["ElevatorType"] = input.ElevatorType.ToString();
            foreach (var field in typeof(ElevatorParameters).GetFields())
                if (field.FieldType == typeof(double))
                    values[field.Name] = Number((double)field.GetValue(p));
            // Accept the conventional capitalized DBG spelling as well as model property names.
            values["CarDBG"] = values["CarDbg"];
            values["CWDBG"] = values["CwDbg"];
            foreach (var pair in clearances)
                values["Clearance." + pair.Key] = Number(pair.Value);
            if (input.Capacity.HasValue)
                values["Capacity"] = Number(input.Capacity.Value);
            if (input.Stops.HasValue)
                values["Stops"] = input.Stops.Value.ToString(CultureInfo.InvariantCulture);
            return values;
        }
        public static string Render(string template, Dictionary<string, string> values, Func<string, string> escapeValue)
        {
            if (template == null)
                throw new ArgumentNullException("template");
            return Regex.Replace(template, @"\{\{([^{}]+)\}\}", match =>
            {
                string value;
                if (!values.TryGetValue(match.Groups[1].Value, out value))
                    throw new ArgumentException("Unknown or unset annotation value: " + match.Groups[1].Value);
                return escapeValue(value);
            });
        }
        private static string Number(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                throw new ArgumentException("Annotation value is not finite.");
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
