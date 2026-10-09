using System;
using System.Collections.Generic;
using Elevator.Core.Presets;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Sheet
{
    public sealed class CalculationSheetInputs
    {
        // Malformed UI text clears the canonical value; it makes the dependent calculation unavailable, never zero.
        public Dictionary<string, string> ChassisDraftErrors = new Dictionary<string, string>();
        public Dictionary<string, string> SafetyPitDraftErrors = new Dictionary<string, string>();
        public string CarGuideForceDraftError;
        public DateTime GeneratedLocal = ReportClock.Now();

        // Company-level settings (application, not project); both optional.
        public string CompanyName;
        public DocxImage Logo;

        // Editable preset library (config/presets.json); null loads presets.json beside the application.
        public PresetLibrary Presets;
    }
}
