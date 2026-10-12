using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Company;
using Elevator.Core.Layouts;
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
   RevisionMessage();Phase();TitleBlockFields();
   Note(GeneralViewModel.ReportNote);
   Note(GeneralViewModel.CompanySummary(CompanyReportSettings.Load()));
   Content.Children.Add(Form.Button("Company / report settings…",ShowCompanySettings));
  }
  // The revision is a number (00, 01, 02 …). A stored non-number is kept as it is and shows the validation message; typing accepts digits only.
  void RevisionMessage()
  {
   TextBox revision;StackPanel panel;
   if(!Form.Editors.TryGetValue("Report.Revision",out revision)||(panel=revision.Parent as StackPanel)==null)return;
   var message=new TextBlock{FontSize=11,Foreground=Brushes.DarkRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(1,5,0,0),Tag="revision-message"};panel.Children.Add(message);
   Action show=()=>{var issue=DrawingRevision.Validate(revision.Text);message.Text=issue==null?"":issue.Message;message.Visibility=issue==null?Visibility.Collapsed:Visibility.Visible;};
   revision.TextChanged+=(s,e)=>show();revision.PreviewTextInput+=(s,e)=>{if(e.Text.Any(c=>c<'0'||c>'9'))e.Handled=true;};show();
  }
  // The project phase (a project extension, not a member of the Report group, so older project files stay byte for byte the same).
  void Phase()
  {
   var panel=new StackPanel();var row=Form.FormRow("Phase",panel);
   var box=new TextBox{Text=DocumentData.PhaseOf(Session.Project)};System.Windows.Automation.AutomationProperties.SetName(box,"Phase");panel.Children.Add(box);
   box.TextChanged+=(s,e)=>{Session.Dirty=true;Shell.RefreshProjectStatus();};
   Form.Committers.Add(()=>DocumentData.SetPhase(Session.Project,box.Text));
   Content.Children.Add(row);
  }
  // The title-block texts of the drawing sheet that follow the preset library until edited (control, supply, type of work, drawing name):
  // an edited value locks, a value equal to the preset (or blank) follows the library again. Arabic is typed as normal Arabic.
  void TitleBlockFields()
  {
   var library=Shell.Presets.Library;if(library==null)return;
   Heading("Drawing title block");
   foreach(var field in new[]{Elevator.Core.Layouts.TitleBlockFields.Control,Elevator.Core.Layouts.TitleBlockFields.Supply,Elevator.Core.Layouts.TitleBlockFields.TypeOfWork,Elevator.Core.Layouts.TitleBlockFields.DrawingName})
   {
    var f=field;var panel=new StackPanel();var row=Form.FormRow(f.Label,panel);
    Func<string> effective=()=>Elevator.Core.Layouts.TitleBlockFields.Effective(f,Session.Project,library);
    Func<string> preset=()=>Convert.ToString(f.PresetValue(new Elevator.Core.Presets.PresetContext{Project=Session.Project,Library=library}))??"";
    var box=new TextBox{Text=effective()};System.Windows.Automation.AutomationProperties.SetName(box,f.Label);panel.Children.Add(box);
    box.TextChanged+=(s,e)=>{Session.Dirty=true;Shell.RefreshProjectStatus();};
    Form.Committers.Add(()=>
    {
     string typed=(box.Text??"").Trim();
     if(typed==effective())return;
     if(typed.Length==0||typed==preset())f.Reset(Session.Project);else f.Edit(Session.Project,typed);
    });
    Content.Children.Add(row);
   }
  }
  void ShowCompanySettings()
  {
   var dialog=new CompanySettingsWindow(CompanyReportSettings.Load()){Owner=Shell,FontFamily=Shell.FontFamily,Foreground=Ink};
   dialog.ShowDialog();
   if(dialog.Saved){Shell.SetStatus("Company / report settings saved.");if(Shell.ActivePage==Key){Shell.Commit();Shell.ShowPage(Key);}}
  }
 }
}
