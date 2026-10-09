using System.Collections.Generic;

namespace Elevator.Reporting.Docx
{
    // Header title block content: logo | company over title | project rows | document rows + Page X of Y.
    public sealed class DocxTitleBlock
    {
        public DocxImage Logo;
        public string Company;
        public string Title = "Elevator Calculation Sheet";
        public List<KeyValuePair<string, string>> ProjectRows = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> DocumentRows = new List<KeyValuePair<string, string>>();
    }
}
