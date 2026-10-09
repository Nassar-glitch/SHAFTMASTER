using System;
using System.Windows;
using System.Windows.Input;
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
   vm.Load(Session,Presets,()=>{Live.Design=null;foreach(var row in vm.Rows)row.Refresh();});
   var grid=new DataGrid{ItemsSource=vm.Rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,MinHeight=220,MaxHeight=390,RowHeight=36,HeadersVisibility=DataGridHeadersVisibility.Column,Background=Brushes.White};Form.FloorGrid=grid;
   grid.Columns.Add(new DataGridTextColumn{Header="Floor name",Binding=new Binding("Name"){ValidatesOnExceptions=true},MinWidth=100,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   grid.Columns.Add(new DataGridTextColumn{Header="Height to next (mm)",Binding=new Binding("Height"){ValidatesOnExceptions=true},MinWidth=145,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
   grid.Columns.Add(new DataGridCheckBoxColumn{Header="Served",Binding=new Binding("Served"),Width=90});
   grid.Columns.Add(new DataGridTextColumn{Header="Elevation (mm) · calculated",Binding=new Binding("Elevation"),IsReadOnly=true,Width=240});Content.Children.Add(grid);
   var newName=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="New floor name"};var interval=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="Added interval in mm",Text=vm.NewFloorInterval};
   var bar=new WrapPanel{Margin=new Thickness(0,16,0,12)};bar.Children.Add(new TextBlock{Text="Name / interval (mm)  ",VerticalAlignment=VerticalAlignment.Center});bar.Children.Add(newName);bar.Children.Add(interval);
   // Each action commits the page, runs the view-model command on the selected row, and rebuilds the page.
   Func<ICommand,Action> floorAction=command=>()=>{Shell.Commit();vm.SelectedRow=grid.SelectedItem as FloorRow;vm.NewFloorName=newName.Text;vm.NewFloorInterval=interval.Text;command.Execute(null);Live.Design=null;Shell.ShowPage(Key);};
   bar.Children.Add(Form.Button("Add before selected",floorAction(vm.InsertBeforeCommand)));Content.Children.Add(bar);
   var edits=new WrapPanel();edits.Children.Add(Form.Button("Remove",floorAction(vm.RemoveCommand)));
   edits.Children.Add(Form.Button("Move down the shaft",floorAction(vm.MoveDownCommand)));edits.Children.Add(Form.Button("Move up the shaft",floorAction(vm.MoveUpCommand)));Content.Children.Add(edits);
  }
 }
}
