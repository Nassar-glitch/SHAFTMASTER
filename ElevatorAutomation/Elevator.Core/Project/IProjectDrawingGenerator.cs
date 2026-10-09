namespace Elevator.Core.Project
{
    public interface IProjectDrawingGenerator
    {
        DrawingGenerationResult GenerateProjectDrawings(ElevatorProject project, GenerationOptions options);
    }
}
