using System;
using Elevator.Core.Layouts;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Company
{
    /// <summary>
    /// The one set of company and report data of a project's documents (calculation sheet, Design Notes, drawing title block and file
    /// names): the company name and logo (company settings, the name following the preset library until the user enters one), the client,
    /// prepared / checked / approved and the revision (the project's Report group). The report texts are trimmed; the company name is kept
    /// exactly as typed (the file names keep it); a missing text is "".
    /// </summary>
    public sealed class DocumentData
    {
        /// <summary>The company name exactly as typed: the one entered in the company settings, else the preset library's.</summary>
        public string CompanyName = "";
        /// <summary>The logo file name of the company settings; "" when none.</summary>
        public string LogoFile = "";
        public string Client = "";
        public string PreparedBy = "";
        public string CheckedBy = "";
        public string ApprovedBy = "";
        /// <summary>The revision number (blank: 0); null when the stored revision is not a number (see <see cref="RevisionIssue"/>).</summary>
        public int? RevisionNumber;
        /// <summary>The revision as NN ("00", "01" …); "" when the stored revision is not a number.</summary>
        public string Revision = "";
        /// <summary>The validation issue of a stored revision that is not a number; null when it is valid. The stored text is kept.</summary>
        public ValidationIssue RevisionIssue;

        static string Clean(string v)
        {
            return String.IsNullOrWhiteSpace(v) ? "" : v.Trim();
        }

        /// <summary>
        /// The company name in effect: the company settings' name when it is not blank, otherwise the library's company name (the shipped
        /// default when the library has none); "" without either.
        /// </summary>
        public static string Company(CompanySettings company, PresetLibrary library)
        {
            if (company != null && !String.IsNullOrWhiteSpace(company.CompanyName))
                return company.CompanyName;
            return library == null ? "" : library.CompanyNameOrDefault();
        }

        /// <summary>Resolves the document data of a project (project may be null: company data only).</summary>
        public static DocumentData Resolve(ElevatorProject project, PresetLibrary library, CompanySettings company)
        {
            var d = new DocumentData
            {
                CompanyName = Company(company, library),
                LogoFile = company == null ? "" : Clean(company.LogoFile)
            };
            var report = project == null ? null : project.Report;
            if (report != null)
            {
                d.Client = Clean(report.ClientName);
                d.PreparedBy = Clean(report.PreparedBy);
                d.CheckedBy = Clean(report.CheckedBy);
                d.ApprovedBy = Clean(report.ApprovedBy);
            }
            string stored = report == null ? null : report.Revision;
            d.RevisionNumber = DrawingRevision.Number(stored);
            d.Revision = d.RevisionNumber.HasValue ? DrawingRevision.Text(d.RevisionNumber.Value) : "";
            d.RevisionIssue = DrawingRevision.Validate(stored);
            return d;
        }
    }
}
