namespace Elevator.Core.MachineRoom
{
    public interface IMachineBeamSelector
    {
        MachineBeamSelection Select(double capacityKg, double spanMm);
    }
}
