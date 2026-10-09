using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop
{
 // DESIGN-FLOW stage 2: project fields follow config/presets.json until the user edits them (lock); Reset unlocks.
 public sealed partial class MainWindow
 {
  readonly Dictionary<string,Action> managedRefreshers=new Dictionary<string,Action>();
  TextBlock personsLabel;RopeSizing lastRopeSizing;
  static PresetField Managed(string path){return ProjectPresets.Fields.FirstOrDefault(f=>f.Key==path);}
  // Writes the presets of every unlocked field and refreshes the visible editors (except the one being typed in).
  // Rope sizing runs the rope / traction checks, so typing skips it; Commit and page changes run it.
  void ApplyProjectPresets(string typing=null,bool sizeRopes=true)
  {
   if(presets==null||!ProjectPresets.Follows(session.Project))return;
   if(sizeRopes)lastRopeSizing=ProjectPresets.Apply(session.Project,presets,context);
   else{var c=new PresetContext{Project=session.Project,Library=presets};foreach(var f in ProjectPresets.Fields)if(!f.IsEdited(session.Project))f.Store(session.Project,f.Preset(c));}
   foreach(var pair in managedRefreshers.ToList())if(pair.Key!=typing)pair.Value();
   RefreshPresetEditors();RefreshPersons();
  }
  void ManagedEdited(PresetField f)
  {
   if(f==null)return;
   // Lock and follow only. The editor's own handler invalidates the results it affects; results whose inputs change
   // through a follower are caught by their input fingerprints (currency checks), so unrelated results stay current.
   if(ProjectPresets.Follows(session.Project))ProjectPresets.Lock(session.Project,f.Key);
   session.Dirty=true;
   ApplyProjectPresets(f.Key,false);
   RefreshProjectStatus();
  }
  ContextMenu ManagedResetMenu(PresetField f)
  {
   var menu=new ContextMenu();var item=new MenuItem{Header="Reset to preset"};
   item.Click+=(s,e)=>Guard(()=>{
    f.Reset(session.Project);session.Dirty=true;design=null;calculationDrafts.Remove(f.Key);calculationDraftErrors.Remove(f.Key);
    if(f.Key=="Calculations.CarGuideForces.GuideShoeSpacingMm"){carGuideForceDraft=null;carGuideForceDraftError=null;}
    ApplyProjectPresets();InvalidateCalculationDisplay();InvalidateRopeTractionDisplay();InvalidateCarGuideForceDisplay();RefreshProjectStatus();
    // Pages whose layout depends on the value (counts, Basic weights) are rebuilt.
    if(activePage=="CALCULATIONS")ShowPage("CALCULATIONS");
   });
   menu.Items.Add(item);return menu;
  }
  static string ManagedText(object v){return v==null?"":v is double?((double)v).ToString("0.###",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture);}
  // Persons shown next to Q: floor(Q / 75 kg).
  void PersonsLabel(TextBox capacity)
  {
   var panel=capacity==null?null:capacity.Parent as StackPanel;if(panel==null)return;
   personsLabel=new TextBlock{FontSize=12,Foreground=ink,Margin=new Thickness(1,5,0,0)};panel.Children.Add(personsLabel);RefreshPersons();
   capacity.TextChanged+=(s,e)=>RefreshPersons();
  }
  void RefreshPersons()
  {
   if(personsLabel==null)return;TextBox box;double q;double? value=session.Project.Elevator.CapacityKg;
   if(editors.TryGetValue("Elevator.CapacityKg",out box)&&Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out q))value=q;
   var n=presets==null?null:presets.PersonsFor(value);personsLabel.Text=n.HasValue?n.Value+(n.Value==1?" person":" persons"):"";
  }
  // One action per page (shown while the page has blank preset fields) and one global action.
  void FillBlanksButton(string page)
  {
   if(presets==null||!ProjectPresets.AnyBlank(session.Project,page))return;
   var b=Button("Fill blanks from presets",()=>FillBlanks(page));b.HorizontalAlignment=HorizontalAlignment.Left;b.Margin=new Thickness(0,0,0,14);
   System.Windows.Automation.AutomationProperties.SetName(b,"Fill blanks from presets ("+page+")");content.Children.Add(b);
  }
  // ---------- live calculation ----------
  // Every input change restarts a 300 ms timer; when it fires, the presets are applied and every calculation runs again,
  // so the results are always current. Invalid entries keep their field message and the dependent result shows nothing.
  System.Windows.Threading.DispatcherTimer liveTimer;bool liveRunning;
  void ScheduleLiveCalculation()
  {
   if(liveRunning)return;
   if(liveTimer==null){liveTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};liveTimer.Tick+=(s,e)=>{try{RunLiveCalculation();}catch(Exception ex){liveRunning=false;SetStatus(ex.Message);}};}
   liveTimer.Stop();liveTimer.Start();
  }
  void RunLiveCalculation()
  {
   if(liveTimer!=null)liveTimer.Stop();
   if(liveRunning||busy||session==null||session.Project==null)return;
   liveRunning=true;
   try{
    var p=session.Project;if(p.Calculations==null)p.Calculations=new Elevator.Core.Calculations.ChassisCalculationInputs();
    try{ApplyProjectPresets();}catch(Exception){}
    // Only results whose inputs changed are recalculated; independent current results stay as they are.
    try{if(RelevantDraftErrors.Count>0)calculations.Invalidate();else if(calculations.Result==null||!calculations.IsCurrent(p,context))calculations.Calculate(p,context);}catch(Exception){calculations.Invalidate();}
    try{if(!Elevator.Core.Calculations.RopeTractionCalculationEngine.IsCurrent(ropeTractionResult,p,context,presets))ropeTractionResult=new Elevator.Core.Calculations.RopeTractionCalculationEngine().Calculate(p,context,presets);}catch(Exception){ropeTractionResult=null;}
    try{if(!Elevator.Core.Calculations.SafetyPitCalculationEngine.IsCurrent(safetyPitResult,p)){safetyPitResult=new Elevator.Core.Calculations.SafetyPitCalculationEngine().Calculate(p);ApplySafetyPitDraftValidation(safetyPitResult);}}catch(Exception){safetyPitResult=null;}
    try{if(carGuideForceDraftError!=null||!Elevator.Core.Calculations.CarGuideForceCalculationEngine.IsCurrent(carGuideForceResult,p,context)){EnsureCarGuideForceGeometry();carGuideForceResult=new Elevator.Core.Calculations.CarGuideForceCalculationEngine().Calculate(p,carGuideForceGeometry,context);ApplyCarGuideForceDraftValidation();}}catch(Exception){carGuideForceResult=null;}
   }finally{liveRunning=false;}
   if(activePage=="CALCULATIONS"){RenderCalculationResults();RenderRopeTractionResults();RenderSafetyPitResults();RenderCarGuideForceResults();}
  }
  // Short words for engine status codes; the UI and the status bar never show UPPER_SNAKE_CASE codes.
  static string StatusWord(string code)
  {
   switch(code)
   {
    case null:return "";
    case "CALCULATED":case "CALCULATED_UNDER_ASSUMPTIONS":case "CALCULATED_UNDER_NOTE_ASSUMPTIONS":return "Calculated";
    case "MISSING_INPUT":return "Input missing";
    case "INVALID_INPUT":return "Invalid input";
    case "UPLIFT_REVIEW_REQUIRED":return "Uplift at a bearing";
    case "UNSUPPORTED_CONFIGURATION":return "Not supported";
    case "STALE_GEOMETRY":return "Geometry changed";
    case "INVALID_GEOMETRY":return "Invalid geometry";
    case "INVALID_GEOMETRY_OR_LOAD":return "Invalid geometry or load";
    case "NOT_APPLICABLE":return "Not applicable";
    case "NOT_YET_EVALUABLE":return "Not evaluated";
    case "REVIEW_REQUIRED":return "Check";
   }
   var t=code.Replace('_',' ').ToLowerInvariant();return t.Length==0?t:Char.ToUpperInvariant(t[0])+t.Substring(1);
  }
  static string Plain(string text)
  {
   if(text==null)return null;
   return System.Text.RegularExpressions.Regex.Replace(text,@"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b",m=>StatusWord(m.Value));
  }
  Button FillAllButton(){var b=Button("Fill blanks from presets",()=>FillBlanks(null));System.Windows.Automation.AutomationProperties.SetName(b,"Fill blanks from presets (all pages)");return b;}
  void FillBlanks(string page)
  {
   if(presets==null)throw new ArgumentException("The preset library could not be read: "+presetError);
   Commit();
   lastRopeSizing=ProjectPresets.FillBlanks(session.Project,presets,context,page);
   session.Dirty=true;design=null;calculations.Invalidate();ResetSafetyPit();ResetRopeTraction();ResetCarGuideForces();calculationDrafts.Clear();calculationDraftErrors.Clear();
   ShowPage(page??activePage);SetStatus(page==null?"Blank fields filled from presets.":"Blank fields on this page filled from presets.");
  }
 }
}
