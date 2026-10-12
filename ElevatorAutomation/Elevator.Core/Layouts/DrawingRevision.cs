using System;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// The drawing revision NN (DESIGN-FLOW "Layouts and PDF"): the same revision field as the calculation sheet (Report.Revision). Blank
    /// is the first issue, 00; a whole number 0, 1, 2 … is written with at least two digits (00, 01, 02 …). Any other text is not a
    /// drawing revision: it has no number.
    /// </summary>
    public static class DrawingRevision
    {
        /// <summary>The revision number of a revision text; null when the text is not a whole number.</summary>
        public static int? Number(string revision)
        {
            if (String.IsNullOrWhiteSpace(revision))
                return 0;
            string t = revision.Trim();
            int n;
            if (t.Length > 9 || !t.All(c => c >= '0' && c <= '9') || !Int32.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out n))
                return null;
            return n;
        }

        /// <summary>The revision number of a project; null when its revision text is not a whole number.</summary>
        public static int? Number(ElevatorProject project)
        {
            return Number(project == null || project.Report == null ? null : project.Report.Revision);
        }

        /// <summary>A revision number as NN: "00", "01" … ("100" above 99).</summary>
        public static string Text(int number)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException("number", "The drawing revision cannot be negative.");
            return number.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>The NN text of a project's revision; "" when its revision text is not a whole number.</summary>
        public static string Text(ElevatorProject project)
        {
            int? n = Number(project);
            return n.HasValue ? Text(n.Value) : "";
        }
    }
}
