namespace Elevator.Core.Project
{
    /// <summary>Generates the drawings of a project.</summary>
    public interface IProjectDrawingGenerator
    {
        DrawingGenerationResult GenerateProjectDrawings(ElevatorProject project, GenerationOptions options);
    }
}
