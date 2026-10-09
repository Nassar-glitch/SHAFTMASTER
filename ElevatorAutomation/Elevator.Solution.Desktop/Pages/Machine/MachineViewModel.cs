using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class MachineViewModel:PageViewModel
 {
  public MachineViewModel():base("MACHINE"){}
  public static readonly FieldSpec[] Fields={new FieldSpec("Traction sheave pitch diameter (mm)","Machine.TractionSheavePitchDiameterMm"),new FieldSpec("Minimum deflector/slab gap (mm)","MachineRoom.MinimumDeflectorSlabClearanceMm")};
 }
}
