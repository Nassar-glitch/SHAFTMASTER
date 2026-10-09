using System.IO;
using System.Windows;
using System.Windows.Controls;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // DRAWINGS: views, output, generation and STEP export, and the last outputs.
 sealed class DrawingsPage:PageView
 {
  readonly DrawingsViewModel vm;
  public DrawingsPage(MainWindow shell,string outputFolder):base(shell){vm=new DrawingsViewModel(outputFolder);}
  public DrawingsViewModel ViewModel{get{return vm;}}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   foreach(var view in DrawingsViewModel.Views){var flag=DrawingsViewModel.Flag(view);var box=new CheckBox{Content=view.Name,IsChecked=(Session.Views&flag)!=0,Margin=new Thickness(0,0,0,12),FontSize=16};box.Click+=(s,e)=>{if(box.IsChecked==true)Session.Views|=flag;else Session.Views&=~flag;Live.Design=null;};Content.Children.Add(box);}
   Heading("Output");var folder=new TextBox{Text=vm.OutputFolder,Margin=new Thickness(0,0,0,10)};var file=new TextBox{Text=vm.OutputName,Margin=new Thickness(0,0,0,12)};Content.Children.Add(new TextBlock{Text="Output folder"});Content.Children.Add(folder);Content.Children.Add(new TextBlock{Text="DWG filename"});Content.Children.Add(file);
   Form.Committers.Add(()=>vm.SetOutput(folder.Text,file.Text));
   var arrange=new CheckBox{Content="Automatic coordinated arrangement (measured extents)",IsChecked=Session.Arrange,Margin=new Thickness(0,4,0,12)};arrange.Click+=(s,e)=>Session.Arrange=arrange.IsChecked==true;Content.Children.Add(arrange);
   Form.Field("View margin (mm)","Drawing.ViewMarginMm");Form.Field("Plan title","Drawing.PlanTitle");
   Content.Children.Add(Form.Button("GENERATE DRAWINGS",()=>Shell.Commands.StartGeneration(),true));
   Heading("3D model (STEP AP214, mm)");
   Content.Children.Add(Form.Button("Export STEP…",()=>Shell.Commands.StartStepExport()));
   var lastStep=Shell.Commands.LastStep;var last=Shell.Commands.Last;
   if(lastStep!=null){Note("STEP exported and verified · "+lastStep.Verification.ReopenedSolidCount+" solids");OutputCard("STEP model",lastStep.OutputPath,lastStep.SourceFolder);}
   if(last!=null&&last.Success){Heading("DRAWING GENERATED SUCCESSFULLY");OutputCard("Coordinated drawing",last.OutputPath,null);var actions=new WrapPanel();actions.Children.Add(Form.Button("Open Drawing",()=>System.Diagnostics.Process.Start(last.OutputPath)));actions.Children.Add(Form.Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(last.OutputPath))));Content.Children.Add(actions);}
  }
  void OutputCard(string label,string output,string source)
  {
   var panel=new StackPanel{Margin=new Thickness(0,0,0,15)};panel.Children.Add(new TextBlock{Text=label+" · "+Path.GetFileName(output),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
   var details=new StackPanel();details.Children.Add(new TextBox{Text=output,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)});if(source!=null)details.Children.Add(new TextBox{Text="Editable source: "+source,IsReadOnly=true,TextWrapping=TextWrapping.Wrap});panel.Children.Add(new Expander{Header="Output details",Content=details});
   panel.Children.Add(Form.Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(output))));Content.Children.Add(panel);
  }
 }
}
