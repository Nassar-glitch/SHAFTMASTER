using System;
using Elevator.Core.Layouts;
using Elevator.Core.Project;

namespace Elevator.Core.Company
{
    /// <summary>
    /// The one set of company and report data of a project's documents (calculation sheet, Design Notes, drawing title block and file
    /// names): the company name and logo (the company settings) and the client, document number, prepared / checked / approved and
    /// revision (the project's Report group). Nothing is filled in: a value the user has not entered is "" and the documents leave it out.
    /// The report texts are trimmed; the company name is kept exactly as typed (the file names keep it).
    /// </summary>
    public sealed class DocumentData
    {
        /// <summary>The company name exactly as typed in the company settings; "" when none is entered.</summary>
        public string CompanyName = "";
        /// <summary>The logo file name of the company settings; "" when none.</summary>
        public string LogoFile = "";
        public string Client = "";
        public string DocumentNumber = "";
        public string PreparedBy = "";
        public string CheckedBy = "";
        public string ApprovedBy = "";
        /// <summary>The revision shown on every document (<see cref="DrawingRevision.Display(string)"/>): "" when blank, NN for a number, else the stored text.</summary>
        public string Revision = "";
        /// <summary>The revision number of the file names (blank: 0); null when the stored revision is not a number.</summary>
        public int? RevisionNumber;
        /// <summary>The validation issue of a stored revision that is not a number; null when it is valid. The stored text is kept.</summary>
        public ValidationIssue RevisionIssue;

        static string Clean(string v)
        {
            return String.IsNullOrWhiteSpace(v) ? "" : v.Trim();
        }

        /// <summary>The company name: the company settings' name as typed; "" when none is entered.</summary>
        public static string Company(CompanySettings company)
        {
            return company == null || String.IsNullOrWhiteSpace(company.CompanyName) ? "" : company.CompanyName;
        }

        /// <summary>Resolves the document data of a project (project may be null: company data only).</summary>
        public static DocumentData Resolve(ElevatorProject project, CompanySettings company)
        {
            var d = new DocumentData
            {
                CompanyName = Company(company),
                LogoFile = company == null ? "" : Clean(company.LogoFile)
            };
            var report = project == null ? null : project.Report;
            if (report != null)
            {
                d.Client = Clean(report.ClientName);
                d.DocumentNumber = Clean(report.DocumentNumber);
                d.PreparedBy = Clean(report.PreparedBy);
                d.CheckedBy = Clean(report.CheckedBy);
                d.ApprovedBy = Clean(report.ApprovedBy);
            }
            string stored = report == null ? null : report.Revision;
            d.Revision = DrawingRevision.Display(stored);
            d.RevisionNumber = DrawingRevision.Number(stored);
            d.RevisionIssue = DrawingRevision.Validate(stored);
            return d;
        }
    }
}
