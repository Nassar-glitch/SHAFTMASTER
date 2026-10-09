using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // FLOORS: the floor grid (name, interval, served, calculated elevation) and the floor actions.
 sealed class FloorsPage:PageView
 {
  readonly FloorsViewModel vm=new FloorsViewModel();
  public FloorsPage(MainWindow shell):base(shell){}
  public FloorsViewModel ViewModel{get{return vm;}}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);
   vm.Load(Session,()=>{Live.Design=null;foreach(var row in vm.Rows)row.Refresh();});
   var grid=new DataGrid{ItemsSource=vm.Rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,MinHeight=220,MaxHeight=390,RowHeight=36,HeadersVisibility=DataGridHeadersVisibility.Column,Background=Brushes.White};Form.FloorGrid=grid;
   grid.Columns.Add(new DataGridTextColumn{Header="Floor name",Binding=new Binding("Name"){ValidatesOnExceptions=true},MinWidth=100,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   grid.Columns.Add(new DataGridTextColumn{Header="Height to next (mm)",Binding=new Binding("Height"){ValidatesOnExceptions=true},MinWidth=145,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   grid.Columns.Add(new DataGridCheckBoxColumn{Header="Served",Binding=new Binding("Served"),Width=90});
   grid.Columns.Add(new DataGridTextColumn{Header="Elevation (mm) · calculated",Binding=new Binding("Elevation"),IsReadOnly=true,Width=240});Content.Children.Add(grid);
   var newName=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="New floor name"};var interval=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="Added interval in mm",Text=FloorsViewModel.NewFloorInterval(Presets)};
   var bar=new WrapPanel{Margin=new Thickness(0,16,0,12)};bar.Children.Add(new TextBlock{Text="Name / interval (mm)  ",VerticalAlignment=VerticalAlignment.Center});bar.Children.Add(newName);bar.Children.Add(interval);
   bar.Children.Add(Form.Button("Add before selected",()=>{Shell.Commit();vm.InsertBefore(Session,grid.SelectedItem as FloorRow,newName.Text,interval.Text);Live.Design=null;Shell.ShowPage(Key);}));Content.Children.Add(bar);
   var edits=new WrapPanel();edits.Children.Add(Form.Button("Remove",()=>{Shell.Commit();vm.Remove(Session,grid.SelectedItem as FloorRow);Live.Design=null;Shell.ShowPage(Key);}));
   foreach(int delta in new[]{-1,1}){int d=delta;edits.Children.Add(Form.Button(d<0?"Move down the shaft":"Move up the shaft",()=>{Shell.Commit();vm.Move(Session,grid.SelectedItem as FloorRow,d);Live.Design=null;Shell.ShowPage(Key);}));}Content.Children.Add(edits);
  }
 }
}
