using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // SHAFT: shaft dimensions and the calculated geometry.
 sealed class ShaftPage:PageView
 {
  readonly ShaftViewModel vm=new ShaftViewModel();
  public ShaftPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);Heading("Shaft dimensions");Form.Fields(ShaftViewModel.Fields);
   Heading("Calculated geometry");var derived=DerivedGeometry.Text(Session,Live,Context);if(derived!=null)Note(derived);
  }
 }
}
