namespace Elevator.Core.Company
{
    /// <summary>
    /// The company values the user enters once for all projects (never stored in a project file): the company name and the logo. The one
    /// source of the company name of every document and file name; nothing is filled in when the user has entered none. A portable model; where it is stored is an adapter (the application keeps it in %AppData%\ShaftMaster\report-settings.json through
    /// Elevator.Reporting.Sheet.CompanyReportSettings, whose file format is unchanged).
    /// </summary>
    public sealed class CompanySettings
    {
        /// <summary>The company name as typed; null or blank: no company name (the documents leave it out).</summary>
        public string CompanyName;

        /// <summary>The logo file name inside the settings folder; null: no logo.</summary>
        public string LogoFile;

        /// <summary>The default prepared / checked / approved names of every project's documents. A project's own value (Report group)
        /// wins; blank means the document leaves the name empty.</summary>
        public string PreparedBy;
        public string CheckedBy;
        public string ApprovedBy;
    }
}
