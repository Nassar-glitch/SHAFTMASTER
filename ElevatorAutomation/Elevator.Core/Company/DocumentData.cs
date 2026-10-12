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
        /// <summary>The project phase (extension "report.phase" of the project file); "" when none.</summary>
        public string Phase = "";
        /// <summary>The revision shown on every document: NN for a number ("00" when blank, the first issue), else the stored text as it is
        /// (<see cref="DrawingRevision.Display(string)"/>).</summary>
        public string Revision = "";
        /// <summary>The revision number of the file names (blank: 0); null when the stored revision is not a number.</summary>
        public int? RevisionNumber;
        /// <summary>The project extension that holds the phase (no member of the Report group: older files stay byte for byte the same).</summary>
        public const string PhaseExtensionKey = "report.phase";

        /// <summary>The phase of a project; "" when none.</summary>
        public static string PhaseOf(ElevatorProject project)
        {
            object raw;
            if (project == null || project.Extensions == null || !project.Extensions.TryGetValue(PhaseExtensionKey, out raw))
                return "";
            return Clean(raw as string);
        }

        /// <summary>Stores the phase of a project; blank removes it.</summary>
        public static void SetPhase(ElevatorProject project, string phase)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            if (String.IsNullOrWhiteSpace(phase))
            {
                if (project.Extensions != null)
                    project.Extensions.Remove(PhaseExtensionKey);
                return;
            }
            if (project.Extensions == null)
                project.Extensions = new System.Collections.Generic.Dictionary<string, object>();
            project.Extensions[PhaseExtensionKey] = phase.Trim();
        }

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
            // A name the project does not hold follows the company default (an edited project value wins).
            if (company != null)
            {
                if (d.PreparedBy.Length == 0)
                    d.PreparedBy = Clean(company.PreparedBy);
                if (d.CheckedBy.Length == 0)
                    d.CheckedBy = Clean(company.CheckedBy);
                if (d.ApprovedBy.Length == 0)
                    d.ApprovedBy = Clean(company.ApprovedBy);
            }
            d.Phase = PhaseOf(project);
            string stored = report == null ? null : report.Revision;
            // Blank is the first issue: 00 on every document (an older non-number such as "A" is shown as stored and reported).
            d.Revision = String.IsNullOrWhiteSpace(stored) ? DrawingRevision.Text(0) : DrawingRevision.Display(stored);
            d.RevisionNumber = DrawingRevision.Number(stored);
            d.RevisionIssue = DrawingRevision.Validate(stored);
            return d;
        }
    }
}
