namespace Elevator.Core.Project
{
    /// <summary>A validation result for one field.</summary>
    public sealed class ValidationIssue
    {
        public string Code;
        public string Field;
        public string Message;
        public ValidationStatus Status;
        public bool BlocksGeneration;
    }
}
