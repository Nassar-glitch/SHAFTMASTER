namespace Elevator.Core.Project
{
    public sealed class FamilyDefinition
    {
        public ElevatorType ElevatorType
        {
            get; set;
        }

        public string TemplateId
        {
            get; set;
        }

        public string TemplateFile
        {
            get; set;
        }

        public string ProfileFile
        {
            get; set;
        }

        public bool Enabled
        {
            get; set;
        }

        public string Status
        {
            get; set;
        }
    }
}
