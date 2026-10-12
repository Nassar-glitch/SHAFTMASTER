using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Elevator.Core.Company;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
namespace Elevator.Solution.Desktop
{
 // Toolbar exports of the calculation sheet and the Design Notes (Elevator.Reporting): dialogs, omission check, status.
 sealed class ReportExport
 {
  readonly MainWindow shell;
  string calculationSheetFolder,designNotesFolder;
  // The message of the last sheet that could not be exported (nothing had complete, valid inputs).
  public string CalculationSheetMessage;
  public Button SheetButton;
  public ReportExport(MainWindow shell){this.shell=shell;}
  string InitialFolder(string last)
  {
   var session=shell.Session;string output=shell.Drawings.ViewModel.OutputFolder;
   return last!=null&&Directory.Exists(last)?last:!String.IsNullOrEmpty(session.FilePath)?Path.GetDirectoryName(session.FilePath):Directory.Exists(output)?output:null;
  }
  // ---------- calculation sheet ----------
  public Button CalculationSheetButton()
  {
   SheetButton=shell.Form.Button("Export Calculation Sheet…",ExportCalculationSheet);SheetButton.ToolTip="Export Calculation Sheet… · editable .docx of the current calculations";
   System.Windows.Automation.AutomationProperties.SetName(SheetButton,"Export Calculation Sheet");return SheetButton;
  }
  void ExportCalculationSheet()
  {
   if(shell.Busy)return;shell.Commit();
   var sheet=BuildCalculationSheet();
   if(!ConfirmCalculationSheet(sheet,ShowOmissionDialog))return;
   var dialog=new SaveFileDialog{Title="Export calculation sheet",Filter="Word document (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,OverwritePrompt=true,
    FileName=CalculationSheet.SuggestedFileName(shell.Session.Project.Info.Name),InitialDirectory=InitialFolder(calculationSheetFolder)};
   if(dialog.ShowDialog(shell)!=true)return;
   sheet.Save(dialog.FileName);calculationSheetFolder=Path.GetDirectoryName(dialog.FileName);
   shell.SetStatus("Calculation sheet exported · "+dialog.FileName+(sheet.Omitted.Count>0?" · omitted: "+String.Join("; ",sheet.Omitted.Select(o=>o.Name)):""));
   MessageBox.Show(shell,"Calculation sheet saved.\n"+dialog.FileName,"Calculation sheet exported",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  CalculationSheetInputs Inputs()
  {
   var company=CompanyReportSettings.Load();var project=shell.Session.Project;
   return new CalculationSheetInputs{
    ChassisDraftErrors=new Dictionary<string,string>(shell.Live.RelevantDraftErrors(project)),SafetyPitDraftErrors=shell.Live.RelevantSafetyPitDraftErrors(project),CarGuideForceDraftError=shell.Live.CarGuideForceDraftError,GeneratedLocal=ReportClock.Now(),
    CompanyName=company.CompanyName,Logo=company.Logo(),Document=DocumentData.Resolve(project,company.ToCompanySettings()),Presets=shell.Presets.Library};
  }
  public CalculationSheet BuildCalculationSheet(){return CalculationSheet.Build(shell.Session.Project,shell.Context,Inputs());}
  static string OmissionText(CalculationSheet sheet)
  {return String.Join("\n",sheet.Omitted.Select(o=>"• "+o.Name+"\n"+String.Join("\n",o.Reasons.Select(r=>"     – "+r))));}
  // False when nothing can be exported (the reason is shown in the application) or the user cancels.
  public bool ConfirmCalculationSheet(CalculationSheet sheet,Func<Window,bool> show)
  {
   CalculationSheetMessage=null;
   if(!sheet.HasContent)
   {
    CalculationSheetMessage="No calculation sheet was exported: none of the calculations has complete, valid inputs.\n\n"+OmissionText(sheet);
    shell.SetStatus("Calculation sheet not exported · no calculation has complete, valid inputs.");
    if(!shell.Smoke)MessageBox.Show(shell,CalculationSheetMessage,"Nothing to export",MessageBoxButton.OK,MessageBoxImage.Warning);
    return false;
   }
   if(sheet.Omitted.Count==0&&sheet.NotOk.Count==0)return true;
   bool accepted=show(OmissionDialog(sheet));
   if(!accepted)shell.SetStatus("Calculation sheet export cancelled.");
   return accepted;
  }
  bool ShowOmissionDialog(Window dialog){dialog.Owner=shell;dialog.ShowDialog();return dialog.Tag is bool&&(bool)dialog.Tag;}
  public Window OmissionDialog(CalculationSheet sheet)
  {
   var window=new Window{Title=sheet.Omitted.Count>0?"Some calculations will be omitted":"Some checks are NOT OK",Width=560,SizeToContent=SizeToContent.Height,MaxHeight=620,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=shell.FontFamily,Foreground=shell.Ink,Background=Brushes.White,Tag=false};
   var panel=new StackPanel{Margin=new Thickness(20)};
   if(sheet.Omitted.Count>0){
   panel.Children.Add(new TextBlock{Text="These calculations do not have complete, valid inputs and will not appear in the calculation sheet:",TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});
   panel.Children.Add(new ScrollViewer{MaxHeight=sheet.NotOk.Count>0?220:380,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=OmissionText(sheet),TextWrapping=TextWrapping.Wrap}});}
   if(sheet.NotOk.Count>0){
   panel.Children.Add(new TextBlock{Text="These checks are NOT OK; the calculation sheet shows their results:",TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Foreground=Brushes.DarkRed,Margin=new Thickness(0,sheet.Omitted.Count>0?12:0,0,10)});
   panel.Children.Add(new ScrollViewer{MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=String.Join("\n",sheet.NotOk.Select(x=>"• "+x)),TextWrapping=TextWrapping.Wrap,Tag="not-ok"}});}
   panel.Children.Add(new TextBlock{Text="The remaining calculations can be exported now.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,14)});
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   var export=new Button{Content="Export anyway",Padding=new Thickness(14,6,14,6),Margin=new Thickness(0,0,8,0),IsDefault=true};
   var cancel=new Button{Content="Cancel",Padding=new Thickness(14,6,14,6),IsCancel=true};
   export.Click+=(s,e)=>{window.Tag=true;window.Close();};cancel.Click+=(s,e)=>{window.Tag=false;window.Close();};
   buttons.Children.Add(export);buttons.Children.Add(cancel);panel.Children.Add(buttons);window.Content=panel;
   return window;
  }
  // Writes the current sheet without dialogs; used by packaged tests driving the real window state.
  public CalculationSheet WriteCalculationSheet(string path){var sheet=BuildCalculationSheet();sheet.Save(path);return sheet;}
  // ---------- Design Notes ----------
  public Button DesignNotesButton()
  {
   var b=shell.Form.Button("Export Design Notes…",ExportDesignNotes);b.ToolTip="Export Design Notes… · warnings and recommendations (.docx)";
   System.Windows.Automation.AutomationProperties.SetName(b,"Export Design Notes");return b;
  }
  DesignNotes BuildDesignNotes()
  {
   var company=CompanyReportSettings.Load();var project=shell.Session.Project;
   return DesignNotes.Build(project,shell.Context,new CalculationSheetInputs{GeneratedLocal=ReportClock.Now(),CompanyName=company.CompanyName,Logo=company.Logo(),Document=DocumentData.Resolve(project,company.ToCompanySettings()),Presets=shell.Presets.Library});
  }
  void ExportDesignNotes()
  {
   if(shell.Busy)return;shell.Commit();
   var notes=BuildDesignNotes();
   var dialog=new SaveFileDialog{Title="Export design notes",Filter="Word document (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,OverwritePrompt=true,FileName=DesignNotes.SuggestedFileName(shell.Session.Project.Info.Name),
    InitialDirectory=InitialFolder(designNotesFolder)};
   if(dialog.ShowDialog(shell)!=true)return;
   notes.Save(dialog.FileName);designNotesFolder=Path.GetDirectoryName(dialog.FileName);
   shell.SetStatus("Design notes exported · "+dialog.FileName+" · warnings: "+notes.Warnings.Count);
   MessageBox.Show(shell,"Design notes saved.\n"+dialog.FileName,"Design notes exported",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  // Writes the design notes without dialogs; used by packaged tests driving the real window state.
  public DesignNotes WriteDesignNotes(string path){var notes=BuildDesignNotes();notes.Save(path);return notes;}
 }
}
