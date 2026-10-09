namespace Elevator.Core.Calculations
{
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
