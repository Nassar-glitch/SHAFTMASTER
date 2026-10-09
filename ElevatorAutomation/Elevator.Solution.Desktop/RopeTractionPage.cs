using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  PresetLibrary presets;string presetError;
  RopeTractionResult ropeTractionResult;StackPanel ropeTractionResults;
  readonly List<Action> presetRefreshers=new List<Action>();
  readonly Dictionary<string,ComboBox> presetChoices=new Dictionary<string,ComboBox>();
  static readonly CultureInfo PresetInv=CultureInfo.InvariantCulture;
  void LoadPresets()
  {
   try{presets=PresetLibrary.Load(Path.Combine(bin,"presets.json"));presetError=null;}
   catch(Exception e){presets=null;presetError=e.Message;}
  }
  PresetContext PresetCtx(){return new PresetContext{Project=session.Project,Library=presets,DeflectorPitchMm=RopeTractionCalculationEngine.DeflectorPitch(session.Project,context)};}
  void ResetRopeTraction(){ropeTractionResult=null;ropeTractionResults=null;}
  void InvalidateRopeTractionDisplay(){ropeTractionResult=null;if(activePage=="CALCULATIONS")RenderRopeTractionResults();ScheduleLiveCalculation();}
  void RefreshPresetEditors(){foreach(var a in presetRefreshers.ToList())a();}
  // ---------- generic preset editors (lock on edit, follow while unlocked, Reset to preset) ----------
  ContextMenu ResetMenu(PresetField field,Action after)
  {
   var menu=new ContextMenu();var item=new MenuItem{Header="Reset to preset"};
   item.Click+=(s,e)=>Guard(()=>{field.Reset(session.Project);session.Dirty=true;after();RefreshPresetEditors();InvalidateRopeTractionDisplay();RefreshProjectStatus();});
   menu.Items.Add(item);return menu;
  }
  void PresetNumber(PresetField field,Func<double,string> validate,Func<bool> visible=null)
  {
   var box=new TextBox{MinWidth=130};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);editors[field.Key]=box;
   var hint=new TextBlock{FontSize=11,Foreground=Brushes.DarkRed,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};
   var panel=new StackPanel{MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(box);panel.Children.Add(hint);var row=FormRow(field.Label,panel);
   bool programmatic=false;string error=null;
   Action show=()=>{programmatic=true;var v=field.Effective(PresetCtx());box.Text=v==null?"":Convert.ToDouble(v,PresetInv).ToString("0.###",PresetInv);programmatic=false;error=null;hint.Visibility=Visibility.Collapsed;};
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;double v;error=null;
    if(String.IsNullOrWhiteSpace(box.Text)){field.Edit(session.Project,null);}
    else if(!Double.TryParse(box.Text,NumberStyles.Float,PresetInv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))error="Enter a number.";
    else{error=validate(v);if(error==null)field.Edit(session.Project,v);}
    hint.Text=error??"";hint.Visibility=error==null?Visibility.Collapsed:Visibility.Visible;
    session.Dirty=true;InvalidateRopeTractionDisplay();RefreshProjectStatus();
   };
   committers.Add(()=>{if(error!=null)throw new ArgumentException(field.Label+": "+error);});
   box.ContextMenu=ResetMenu(field,show);
   presetRefreshers.Add(()=>{if(!field.IsEdited(session.Project))show();if(visible!=null)row.Visibility=visible()?Visibility.Visible:Visibility.Collapsed;});
   show();if(visible!=null)row.Visibility=visible()?Visibility.Visible:Visibility.Collapsed;content.Children.Add(row);
  }
  void PresetText(PresetField field)
  {
   var box=new TextBox{MinWidth=260};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);editors[field.Key]=box;
   bool programmatic=false;
   Action show=()=>{programmatic=true;box.Text=(field.Effective(PresetCtx()) as string)??"";programmatic=false;};
   box.TextChanged+=(s,e)=>{if(programmatic)return;field.Edit(session.Project,String.IsNullOrWhiteSpace(box.Text)?null:box.Text.Trim());session.Dirty=true;InvalidateRopeTractionDisplay();RefreshProjectStatus();};
   box.ContextMenu=ResetMenu(field,show);presetRefreshers.Add(()=>{if(!field.IsEdited(session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(box);content.Children.Add(FormRow(field.Label,panel));
  }
  void PresetChoice(PresetField field,string[] values,Func<string,string> label)
  {
   var combo=new ComboBox{ItemsSource=values.Select(v=>new Choice{Id=v,Name=label(v)}).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,5,8,5)};
   System.Windows.Automation.AutomationProperties.SetName(combo,field.Label);presetChoices[field.Key]=combo;
   bool programmatic=false;
   Action show=()=>{programmatic=true;combo.SelectedValue=field.Effective(PresetCtx()) as string;programmatic=false;};
   combo.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=combo.SelectedItem as Choice;field.Edit(session.Project,choice==null?null:choice.Id);session.Dirty=true;RefreshPresetEditors();InvalidateRopeTractionDisplay();RefreshProjectStatus();};
   combo.ContextMenu=ResetMenu(field,show);presetRefreshers.Add(()=>{if(!field.IsEdited(session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(combo);content.Children.Add(FormRow(field.Label,panel));
  }
  string EffectiveGroove(){return PresetFields.GrooveType.Effective(PresetCtx()) as string;}
  double? EffectiveAngle(PresetField f){var v=f.Effective(PresetCtx());return v==null?(double?)null:Convert.ToDouble(v,PresetInv);}
  // Suspension page: rope and traction data.
  void RopeTractionInputs()
  {
   presetRefreshers.Clear();presetChoices.Clear();
   Heading("Rope and traction data");
   if(presets==null){Note("The preset library could not be read: "+presetError);}
   Func<double,string> positive=v=>v>0?null:"Enter a positive value.";
   PresetText(PresetFields.RopeDesignation);
   PresetNumber(PresetFields.MinimumBreakingLoad,positive);PresetNumber(PresetFields.RopeMass,positive);
   PresetChoice(PresetFields.GrooveType,GrooveTypes.All,GrooveTypes.Label);
   PresetNumber(PresetFields.UndercutAngle,v=>{var g=EffectiveGroove();if(v<0||v>105)return "β must be 0–105° (EN 81-50 5.11.2.3.1).";if(g==GrooveTypes.VUnhardened&&v<=0)return "An unhardened V-groove needs an undercut (β > 0).";return null;},()=>GrooveTypes.NeedsUndercut(EffectiveGroove()));
   PresetNumber(PresetFields.GrooveAngle,v=>{var g=EffectiveGroove();double min=g==GrooveTypes.UndercutU?25:35;return v<min||v>=180?"γ must be at least "+min.ToString(PresetInv)+"° (EN 81-50 5.11.2.3.1).":null;},()=>GrooveTypes.NeedsGrooveAngle(EffectiveGroove()));
   PresetNumber(PresetFields.TravellingCable,positive);
   PresetNumber(PresetFields.BrakingRetardation,v=>v>=TractionRules.MinimumBrakingRetardationMps2?null:"a must be at least 0.5 m/s² (EN 81-50 5.11.2.2.2).");
   PresetNumber(PresetFields.DeflectorMass,positive,()=>PresetCtx().DeflectorPitchMm.HasValue);
   PresetChoice(PresetFields.TractionLimitingMethod,TractionLimitingMethods.All,TractionLimitingMethods.Label);
   // Unlocked rope data follow the rope diameter as it is typed.
   TextBox diameter;
   if(editors.TryGetValue("Suspension.RopeDiameterMm",out diameter))diameter.TextChanged+=(s,e)=>{double v;
    if(String.IsNullOrWhiteSpace(diameter.Text))session.Project.Suspension.RopeDiameterMm=null;
    else if(Double.TryParse(diameter.Text,NumberStyles.Float,PresetInv,out v)&&!Double.IsNaN(v)&&!Double.IsInfinity(v))session.Project.Suspension.RopeDiameterMm=v;
    else return;
    RefreshPresetEditors();};
  }
  // ---------- Calculations page panel ----------
  void RopeTractionSection()
  {
   CalculationGroup("Ropes and traction",()=>{
    ropeTractionResults=new StackPanel{Margin=new Thickness(0,12,0,0)};content.Children.Add(ropeTractionResults);RenderRopeTractionResults();
   },true);
  }
  void RenderRopeTractionResults()
  {
   if(ropeTractionResults==null)return;ropeTractionResults.Children.Clear();var parent=content;content=ropeTractionResults;
   try{
    var r=ropeTractionResult;
    if(r==null||!RopeTractionCalculationEngine.IsCurrent(r,session.Project,context,presets))return;
    Func<bool?,Brush> colour=ok=>ok==false?(Brush)Brushes.DarkRed:ink;
    foreach(var c in r.Checks)content.Children.Add(new TextBlock{Text=c.Title+" · "+(c.Ok==null?c.Required:c.Actual+" (required "+c.Required+") · "+(c.Ok.Value?"OK":"NOT OK")),Foreground=colour(c.Ok),FontWeight=c.Ok==false?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    foreach(var t in r.Traction)content.Children.Add(new TextBlock{Text="Traction, "+(t.Title.Substring(0,1).ToLowerInvariant()+t.Title.Substring(1))+" · T1/T2 "+t.Ratio.ToString("0.###",PresetInv)+" "+t.Condition+" e^(fα) "+t.EFAlpha.ToString("0.###",PresetInv)+" · "+(t.Ok?"OK":"NOT OK"),Foreground=colour(t.Ok),FontWeight=t.Ok?FontWeights.Normal:FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    foreach(var o in r.Omissions)content.Children.Add(new TextBlock{Text=o.Item+" · "+MessageText(o.Reason),Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    CalculationGroup("Equations",()=>{
     if(r.TmaxN.HasValue)Note("Tmax = ((P + Q)/n + H·w)·gn = "+r.TmaxN.Value.ToString("0.##",PresetInv)+" N · Sf = MBL/Tmax = "+r.SfActual.Value.ToString("0.##",PresetInv)+"\nNequiv = "+r.Nequiv.Value.ToString("0.####",PresetInv)+" (Nequiv(t) "+r.NequivT.Value.ToString("0.##",PresetInv)+(r.Geometry.DeflectorPresent?" + Kp "+r.Kp.Value.ToString("0.####",PresetInv)+" × 1":"")+") · Sf(EN 81-50) = "+r.Sf8150.Value.ToString("0.##",PresetInv)+" · Sf required = "+r.SfRequired.Value.ToString("0.##",PresetInv));
     foreach(var c in r.Checks)Note(c.Title+": "+c.Equation+" · "+c.Substitution);
     foreach(var t in r.Traction){Note(t.Title+": "+t.MuEquation+" · "+t.FEquation+" = "+t.F.ToString("0.####",PresetInv)+" · α = "+t.AlphaDeg.ToString("0.##",PresetInv)+"° · e^(fα) = "+t.EFAlpha.ToString("0.###",PresetInv));foreach(var q in t.Positions)Note(q.Name+"\n"+q.T1Equation+"\n"+q.T2Equation);}
    },false);
   }finally{content=parent;}
  }
  // ---------- Design Notes export ----------
  string designNotesFolder;
  void ExportDesignNotes()
  {
   if(busy)return;Commit();
   var company=CompanyReportSettings.Load();
   var notes=DesignNotes.Build(session.Project,context,new CalculationSheetInputs{GeneratedLocal=ReportClock.Now(),CompanyName=company.CompanyName,Logo=company.Logo(),Presets=presets});
   var dialog=new SaveFileDialog{Title="Export design notes",Filter="Word document (*.docx)|*.docx",DefaultExt=".docx",AddExtension=true,OverwritePrompt=true,FileName=DesignNotes.SuggestedFileName(session.Project.Info.Name),
    InitialDirectory=designNotesFolder!=null&&Directory.Exists(designNotesFolder)?designNotesFolder:!String.IsNullOrEmpty(session.FilePath)?Path.GetDirectoryName(session.FilePath):Directory.Exists(outputFolder)?outputFolder:null};
   if(dialog.ShowDialog(this)!=true)return;
   notes.Save(dialog.FileName);designNotesFolder=Path.GetDirectoryName(dialog.FileName);
   SetStatus("Design notes exported · "+dialog.FileName+" · warnings: "+notes.Warnings.Count);
   MessageBox.Show(this,"Design notes saved.\n"+dialog.FileName,"Design notes exported",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  // Writes the design notes without dialogs; used by packaged tests driving the real window state.
  DesignNotes WriteDesignNotes(string path)
  {
   var company=CompanyReportSettings.Load();
   var notes=DesignNotes.Build(session.Project,context,new CalculationSheetInputs{GeneratedLocal=ReportClock.Now(),CompanyName=company.CompanyName,Logo=company.Logo(),Presets=presets});
   notes.Save(path);return notes;
  }
 }
}
