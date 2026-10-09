namespace Elevator.Core.Project
{
    public sealed class ValidationIssue
    {
        public string Code;
        public string Field;
        public string Message;
        public ValidationStatus Status;
        public bool BlocksGeneration;
    }
}
