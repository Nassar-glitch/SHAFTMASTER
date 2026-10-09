using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // RAILS: bracket spacing.
 sealed class RailsPage:PageView
 {
  readonly RailsViewModel vm=new RailsViewModel();
  public RailsPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build(){Form.FillBlanksButton(Key);Form.Fields(RailsViewModel.Fields);}
 }
}
