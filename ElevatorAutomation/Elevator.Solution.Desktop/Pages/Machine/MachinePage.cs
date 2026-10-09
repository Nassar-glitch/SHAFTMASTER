using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // MACHINE: traction sheave and machine-room clearance.
 sealed class MachinePage:PageView
 {
  readonly MachineViewModel vm=new MachineViewModel();
  public MachinePage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build(){Form.FillBlanksButton(Key);Form.Fields(MachineViewModel.Fields);}
 }
}
