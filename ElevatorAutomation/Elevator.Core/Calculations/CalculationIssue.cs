namespace Elevator.Core.Calculations
{
    /// <summary>A problem found by a calculation: the input field and a short message.</summary>
    public sealed class CalculationIssue
    {
        public string Field;
        public string Message;

        public CalculationIssue()
        {
        }

        public CalculationIssue(string f, string m)
        {
            Field = f;
            Message = m;
        }
    }
}
