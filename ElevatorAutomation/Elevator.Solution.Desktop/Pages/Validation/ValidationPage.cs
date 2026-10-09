using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // VALIDATION: engineering statuses and the validation rows of the current inputs.
 sealed class ValidationPage:PageView
 {
  readonly ValidationViewModel vm=new ValidationViewModel();
  public ValidationPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   if(Live.Design==null){try{Shell.Commands.Calculate();}catch(ArgumentException e){Note(e.Message);return;}}
   var design=Live.Design;
   Heading("Engineering statuses");
   foreach(var pair in ValidationViewModel.Statuses(design))
    Content.Children.Add(new TextBlock{Text=pair.Name+"   ·   "+pair.Id,FontWeight=FontWeights.SemiBold,Foreground=pair.Id=="OK"?Accent:Brushes.DarkRed,Margin=new Thickness(0,0,0,10)});
   var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,ItemsSource=ValidationViewModel.Rows(design),MinHeight=220,MaxHeight=420,Background=Brushes.White};
   foreach(var name in ValidationViewModel.Columns)grid.Columns.Add(new DataGridTextColumn{Header=name,Binding=new Binding(name),MinWidth=name=="Message"?280:70,Width=name=="Message"?new DataGridLength(1,DataGridLengthUnitType.Star):new DataGridLength(name=="Status"?170:100),ElementStyle=new Style(typeof(TextBlock)){Setters={new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap),new Setter(FrameworkElement.MarginProperty,new Thickness(4))}}});Content.Children.Add(grid);
  }
 }
}
