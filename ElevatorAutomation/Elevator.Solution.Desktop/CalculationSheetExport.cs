using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  string calculationSheetFolder,calculationSheetMessage;Button calculationSheetButton;
  Button CalculationSheetButton()
  {
   calculationSheetButton=Button("Export Calculation Sheet…",ExportCalculationSheet);calculationSheetButton.ToolTip="Export Calculation Sheet… · editable .docx of the current calculations";
   System.Windows.Automation.AutomationProperties.SetName(calculationSheetButton,"Export Calculation Sheet");return calculationSheetButton;
  }
  void ExportCalculationSheet()
  {
   if(busy)return;Commit();
   var sheet=BuildCalculationSheet();
   if(!ConfirmCalculationSheet(sheet,ShowOmissionDialog))return;
   var dialog=new SaveFileDialog{Title="Export calculation sheet",Filter="Word document (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,OverwritePrompt=true,
    FileName=CalculationSheet.SuggestedFileName(session.Project.Info.Name),
    InitialDirectory=calculationSheetFolder!=null&&Directory.Exists(calculationSheetFolder)?calculationSheetFolder:!String.IsNullOrEmpty(session.FilePath)?Path.GetDirectoryName(session.FilePath):Directory.Exists(outputFolder)?outputFolder:null};
   if(dialog.ShowDialog(this)!=true)return;
   sheet.Save(dialog.FileName);calculationSheetFolder=Path.GetDirectoryName(dialog.FileName);
   SetStatus("Calculation sheet exported · "+dialog.FileName+(sheet.Omitted.Count>0?" · omitted: "+String.Join("; ",sheet.Omitted.Select(o=>o.Name)):""));
   MessageBox.Show(this,"Calculation sheet saved.\n"+dialog.FileName,"Calculation sheet exported",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  Button DesignNotesButton()
  {
   var b=Button("Export Design Notes…",ExportDesignNotes);b.ToolTip="Export Design Notes… · warnings and recommendations (.docx)";
   System.Windows.Automation.AutomationProperties.SetName(b,"Export Design Notes");return b;
  }
  CalculationSheet BuildCalculationSheet()
  {
   var company=CompanyReportSettings.Load();
   return CalculationSheet.Build(session.Project,context,new CalculationSheetInputs{
    ChassisDraftErrors=new Dictionary<string,string>(RelevantDraftErrors),SafetyPitDraftErrors=RelevantSafetyPitDraftErrors,CarGuideForceDraftError=carGuideForceDraftError,GeneratedLocal=ReportClock.Now(),
    CompanyName=company.CompanyName,Logo=company.Logo(),Presets=presets});
  }
  static string OmissionText(CalculationSheet sheet)
  {return String.Join("\n",sheet.Omitted.Select(o=>"• "+o.Name+"\n"+String.Join("\n",o.Reasons.Select(r=>"     – "+r))));}
  // False when nothing can be exported (the reason is shown in the application) or the user cancels.
  bool ConfirmCalculationSheet(CalculationSheet sheet,Func<Window,bool> show)
  {
   calculationSheetMessage=null;
   if(!sheet.HasContent)
   {
    calculationSheetMessage="No calculation sheet was exported: none of the calculations has complete, valid inputs.\n\n"+OmissionText(sheet);
    SetStatus("Calculation sheet not exported · no calculation has complete, valid inputs.");
    if(!smoke)MessageBox.Show(this,calculationSheetMessage,"Nothing to export",MessageBoxButton.OK,MessageBoxImage.Warning);
    return false;
   }
   if(sheet.Omitted.Count==0&&sheet.NotOk.Count==0)return true;
   bool accepted=show(OmissionDialog(sheet));
   if(!accepted)SetStatus("Calculation sheet export cancelled.");
   return accepted;
  }
  bool ShowOmissionDialog(Window dialog){dialog.Owner=this;dialog.ShowDialog();return dialog.Tag is bool&&(bool)dialog.Tag;}
  Window OmissionDialog(CalculationSheet sheet)
  {
   var window=new Window{Title=sheet.Omitted.Count>0?"Some calculations will be omitted":"Some checks are NOT OK",Width=560,SizeToContent=SizeToContent.Height,MaxHeight=620,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=FontFamily,Foreground=ink,Background=Brushes.White,Tag=false};
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
  CalculationSheet WriteCalculationSheet(string path){var sheet=BuildCalculationSheet();sheet.Save(path);return sheet;}
 }
}
