using System.Collections.Generic;

namespace Elevator.Reporting.Sheet
{
    // A calculation left out of the client document, with the reasons shown to the user in the application only.
    public sealed class OmittedCalculation
    {
        public string Name;
        public List<string> Reasons = new List<string>();
    }
}
