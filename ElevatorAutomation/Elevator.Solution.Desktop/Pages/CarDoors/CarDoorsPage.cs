using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // CAR & DOORS: the landing shown with the car, and the calculated geometry.
 sealed class CarDoorsPage:PageView
 {
  readonly CarDoorsViewModel vm=new CarDoorsViewModel();
  public CarDoorsPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Heading("Car display landing");var select=new ComboBox{ItemsSource=CarDoorsViewModel.DisplayFloors(Session.Project),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=Session.Project.Car.DisplayFloorId,Padding=new Thickness(8),Margin=new Thickness(0,0,0,14)};
   select.SelectionChanged+=(s,e)=>{Session.Project.Car.DisplayFloorId=select.SelectedValue as string;Session.Dirty=true;Live.Design=null;};Content.Children.Add(select);
   Heading("Calculated geometry");var derived=DerivedGeometry.Text(Session,Live,Context);if(derived!=null)Note(derived);
  }
 }
}
