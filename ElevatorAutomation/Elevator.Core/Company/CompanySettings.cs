namespace Elevator.Core.Company
{
    /// <summary>
    /// The company values the user enters once for all projects (never stored in a project file): the company name and the logo. A
    /// portable model; where it is stored is an adapter (the application keeps it in %AppData%\ShaftMaster\report-settings.json through
    /// Elevator.Reporting.Sheet.CompanyReportSettings, whose file format is unchanged).
    /// </summary>
    public sealed class CompanySettings
    {
        /// <summary>The company name as typed; null or blank: the company name of the preset library.</summary>
        public string CompanyName;

        /// <summary>The logo file name inside the settings folder; null: no logo.</summary>
        public string LogoFile;
    }
}
