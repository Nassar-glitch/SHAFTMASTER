using System;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// The revision (DESIGN-FLOW "Layouts and PDF"): one value, the project's Report.Revision, for the calculation sheet, the Design
    /// Notes, the title block and the file names. The revision is a number: a whole number 0, 1, 2 … is written with at least two digits
    /// (00, 01, 02 …). Blank is the first issue: the documents leave it out, the file names write 00. Any other text (an older file's "A")
    /// still loads, is kept and shown as stored, has no number, and is reported by <see cref="Validate(string)"/> for the application.
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

        /// <summary>Validation field of the revision.</summary>
        public const string Field = "Report.Revision";
        /// <summary>Validation code of a revision that is not a number.</summary>
        public const string NotANumberCode = "REVISION_NOT_A_NUMBER";

        /// <summary>The issue of a revision text that is not a number (00, 01, 02 …); null when it is valid (blank is valid: 00).</summary>
        public static ValidationIssue Validate(string revision)
        {
            if (Number(revision).HasValue)
                return null;
            return new ValidationIssue
            {
                Code = NotANumberCode,
                Field = Field,
                Message = "Revision must be a number: 00, 01, 02 …",
                Status = ValidationStatus.FAIL,
                BlocksGeneration = false
            };
        }

        /// <summary>The revision issue of a project; null when its revision is valid.</summary>
        public static ValidationIssue Validate(ElevatorProject project)
        {
            return Validate(project == null || project.Report == null ? null : project.Report.Revision);
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

        /// <summary>The revision shown on the documents: "" when blank, NN for a number, otherwise the stored text (trimmed) as it is.</summary>
        public static string Display(string revision)
        {
            if (String.IsNullOrWhiteSpace(revision))
                return "";
            int? n = Number(revision);
            return n.HasValue ? Text(n.Value) : revision.Trim();
        }
    }
}
