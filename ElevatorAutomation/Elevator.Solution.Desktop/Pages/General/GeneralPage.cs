using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Project;
using Elevator.Reporting.Sheet;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // GENERAL: project name and details, capacity (with persons) and speed, and the calculation-sheet details.
 sealed class GeneralPage:PageView
 {
  readonly GeneralViewModel vm;
  public GeneralPage(MainWindow shell):base(shell){vm=new GeneralViewModel(shell.Presets);}
  public GeneralViewModel ViewModel{get{return vm;}}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);
   Heading("Project");Form.Field("Project name","Info.Name");ProjectDetails();Heading("Elevator");
   Form.Field("Capacity (kg)","Elevator.CapacityKg");TextBox capacity;if(Form.Editors.TryGetValue("Elevator.CapacityKg",out capacity))PersonsLabel(capacity);Form.Field("Speed (m/s)","Elevator.SpeedMetresPerSecond");
   CalculationSheetDetails();
  }
  void ProjectDetails()
  {
   var box=new StackPanel{Margin=new Thickness(0,8,0,10)};box.Children.Add(new TextBlock{Text="Project identifier",FontSize=12,Foreground=Brushes.SlateGray});
   box.Children.Add(new TextBox{Text=Session.Project.Info.ProjectId,IsReadOnly=true,Background=new SolidColorBrush(Color.FromRgb(232,238,245)),Margin=new Thickness(0,5,0,8)});box.Children.Add(Form.Button("Copy identifier",()=>{Clipboard.SetText(Session.Project.Info.ProjectId);Shell.SetStatus("Project identifier copied.");}));
   Content.Children.Add(new Expander{Header="Project details",Content=box,Margin=new Thickness(0,0,0,12)});
  }
  // Persons shown next to Q: floor(Q / 75 kg).
  void PersonsLabel(TextBox capacity)
  {
   var panel=capacity==null?null:capacity.Parent as StackPanel;if(panel==null)return;
   Form.PersonsLabel=new TextBlock{FontSize=12,Foreground=Ink,Margin=new Thickness(1,5,0,0)};panel.Children.Add(Form.PersonsLabel);RefreshPersons();
   capacity.TextChanged+=(s,e)=>RefreshPersons();
  }
  // The typed capacity while it parses, else the project value.
  public void RefreshPersons()
  {
   var label=Form.PersonsLabel;if(label==null)return;TextBox box;double q;double? value=Session.Project.Elevator.CapacityKg;
   if(Form.Editors.TryGetValue("Elevator.CapacityKg",out box)&&Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out q))value=q;
   vm.UpdatePersons(value);label.Text=vm.PersonsText;
  }
  // Optional project-level sheet details. Editing them never invalidates calculations.
  void CalculationSheetDetails()
  {
   if(Session.Project.Report==null)Session.Project.Report=new ProjectReportDetails();
   Heading("Calculation sheet details");
   Form.Fields(GeneralViewModel.ReportFields);
   Note(GeneralViewModel.CompanySummary(CompanyReportSettings.Load()));
   Content.Children.Add(Form.Button("Company / report settings…",ShowCompanySettings));
  }
  void ShowCompanySettings()
  {
   var dialog=new CompanySettingsWindow(CompanyReportSettings.Load()){Owner=Shell,FontFamily=Shell.FontFamily,Foreground=Ink};
   dialog.ShowDialog();
   if(dialog.Saved){Shell.SetStatus("Company / report settings saved.");if(Shell.ActivePage==Key){Shell.Commit();Shell.ShowPage(Key);}}
  }
 }
}
