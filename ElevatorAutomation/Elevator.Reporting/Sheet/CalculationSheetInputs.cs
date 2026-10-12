using System;
using System.Collections.Generic;
using Elevator.Core.Presets;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Sheet
{
    /// <summary>Inputs of the calculation sheet and Design Notes that are not part of the project.</summary>
    public sealed class CalculationSheetInputs
    {
        // Malformed UI text clears the canonical value; it makes the dependent calculation unavailable, never zero.
        public Dictionary<string, string> ChassisDraftErrors = new Dictionary<string, string>();
        public Dictionary<string, string> SafetyPitDraftErrors = new Dictionary<string, string>();
        public string CarGuideForceDraftError;
        public DateTime GeneratedLocal = ReportClock.Now();

        // Company-level settings (application, not project; Elevator.Core.Company.CompanySettings, stored by CompanyReportSettings);
        // both optional: a blank company name is left out.
        public string CompanyName;
        public DocxImage Logo;

        // The resolved company and report data (Elevator.Core.Company.DocumentData: company settings + the project's Report group).
        // The desktop wiring (Shell/ReportExport.cs) sets it; null resolves from CompanyName and the project, as before.
        public Elevator.Core.Company.DocumentData Document;

        // Editable preset library (config/presets.json); null loads presets.json beside the application.
        public PresetLibrary Presets;
    }
}
