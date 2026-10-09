namespace Elevator.Core.MachineRoom
{
    /// <summary>Selects the machine beam for a rated capacity and span.</summary>
    public interface IMachineBeamSelector
    {
        MachineBeamSelection Select(double capacityKg, double spanMm);
    }
}
