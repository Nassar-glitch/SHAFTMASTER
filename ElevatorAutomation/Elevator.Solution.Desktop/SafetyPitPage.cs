using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  SafetyPitCalculationResult safetyPitResult;
  StackPanel safetyPitResults;
  TextBlock safetyPitMassSummary;
  readonly Dictionary<string,StackPanel> safetyPitAllocationPanels=new Dictionary<string,StackPanel>();
  readonly Dictionary<string,TextBlock> safetyPitAllocationSummaries=new Dictionary<string,TextBlock>();
  readonly Dictionary<string,ComboBox> safetyPitChoices=new Dictionary<string,ComboBox>();
  readonly Dictionary<string,string> safetyPitDraftErrors=new Dictionary<string,string>();
  readonly Dictionary<string,string> safetyPitDrafts=new Dictionary<string,string>();
  Dictionary<string,string> RelevantSafetyPitDraftErrors
  {
   get{
    var errors=new Dictionary<string,string>();
    foreach(string field in new[]{"CarMassKg","CounterweightMassKg","CarSideAdditionalSuspendedMassKg","CounterweightSideAdditionalSuspendedMassKg"}){
     string path="Calculations."+field,message;if(calculationDraftErrors.TryGetValue(path,out message))errors[path]=message+" Malformed text is not an omitted mass.";
    }
    var c=session.Project.Calculations;var s=c==null?null:c.SafetyPit;
    if(s!=null)foreach(var pair in safetyPitDraftErrors){bool car=pair.Key.EndsWith("CarEventAdditionalMassPercent");if(car?s.CarAdditionalMassAllocation=="FractionParticipates"&&c.CarSideAdditionalSuspendedMassKg>0:s.CounterweightAdditionalMassAllocation=="FractionParticipates"&&c.CounterweightSideAdditionalSuspendedMassKg>0)errors[pair.Key]=pair.Value;}
    return errors;
   }
  }
  void ApplySafetyPitDraftValidation(SafetyPitCalculationResult result)
  {
   if(result==null)return;
   foreach(var error in RelevantSafetyPitDraftErrors){
    bool cw=error.Key=="Calculations.CounterweightMassKg"||error.Key=="Calculations.CounterweightSideAdditionalSuspendedMassKg"||error.Key.EndsWith("CounterweightEventAdditionalMassPercent");
    foreach(var load in cw?new[]{result.P7}:new[]{result.P5,result.P6}){
     load.Status="INVALID_INPUT";load.GroupLoadN=null;load.PerComponentLoadN=null;load.ParticipatingMassKg=null;load.Details.Clear();
     load.Issues.RemoveAll(issue=>issue.Field==error.Key);load.Issues.Add(new CalculationIssue(error.Key,error.Value));
    }
   }
  }
  void ResetSafetyPit(){safetyPitResult=null;safetyPitResults=null;safetyPitMassSummary=null;safetyPitAllocationPanels.Clear();safetyPitAllocationSummaries.Clear();safetyPitChoices.Clear();safetyPitDraftErrors.Clear();safetyPitDrafts.Clear();}
  void InvalidateSafetyPitDisplay(){safetyPitResult=null;if(activePage=="CALCULATIONS")RenderSafetyPitResults();ScheduleLiveCalculation();}
  void SafetyPitSection()
  {
   var c=session.Project.Calculations;if(c.SafetyPit==null)c.SafetyPit=new SafetyPitInputs();var s=c.SafetyPit;safetyPitChoices.Clear();safetyPitAllocationPanels.Clear();safetyPitAllocationSummaries.Clear();
   bool expanded=s.SafetyGearType!=null||s.CarGuideCount.HasValue||s.CarBufferCount.HasValue||s.CounterweightBufferCount.HasValue;
   CalculationGroup("Safety gear & pit loads",()=>{
    safetyPitMassSummary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,12)};content.Children.Add(safetyPitMassSummary);RefreshSafetyPitMassSummary();
    PitChoice("Car safety gear","SafetyGearType",new[]{new Choice{Id=ProjectPresets.Progressive,Name="Progressive"},new Choice{Id=ProjectPresets.Instantaneous,Name="Instantaneous"},new Choice{Id="Other",Name="Other"}});
    PitChoice("Car guide rails","CarGuideCount",new[]{new Choice{Id="2",Name="2"},new Choice{Id="3",Name="3"},new Choice{Id="4",Name="4"}});
    PitChoice("Buffer type","BufferType",new[]{new Choice{Id=ProjectPresets.EnergyAccumulation,Name="Energy accumulation"},new Choice{Id=ProjectPresets.EnergyDissipation,Name="Energy dissipation"}});
    var quantities=Enumerable.Range(1,4).Select(n=>new Choice{Id=n.ToString(CultureInfo.InvariantCulture),Name=n.ToString(CultureInfo.InvariantCulture)}).ToArray();
    PitChoice("Car buffers","CarBufferCount",quantities);PitChoice("Counterweight buffers","CounterweightBufferCount",quantities);
    if(s.CarBufferCount>1)PitSharing("CarBufferSharing","Car buffer sharing");if(s.CounterweightBufferCount>1)PitSharing("CounterweightBufferSharing","CW buffer sharing");
    foreach(string side in new[]{"Car","Counterweight"}){var parent=content;var panel=new StackPanel();content=panel;PitAllocation(side);content=parent;parent.Children.Add(panel);safetyPitAllocationPanels[side]=panel;}
    safetyPitResults=new StackPanel{Margin=new Thickness(0,12,0,0)};content.Children.Add(safetyPitResults);RenderSafetyPitResults();
   },expanded);
  }
  void PitSharing(string field,string label)
  {PitChoice(label,field,new[]{new Choice{Id="EqualSharing",Name="Equal sharing"},new Choice{Id="Unresolved",Name="Unresolved"}});}
  void PitAllocation(string side)
  {
   var s=session.Project.Calculations.SafetyPit;string field=side+"AdditionalMassAllocation";
   var summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,12)};content.Children.Add(summary);safetyPitAllocationSummaries[side]=summary;
   PitChoice(side+" event mass allocation",field,new[]{new Choice{Id="AllParticipates",Name="All participates"},new Choice{Id="NoneParticipates",Name="None participates"},new Choice{Id="FractionParticipates",Name="Percentage participates"}});
   if((string)typeof(SafetyPitInputs).GetField(field).GetValue(s)=="FractionParticipates")PitPercentage(side+"EventAdditionalMassPercent",side+" participating mass (%)");
  }
  void PitChoice(string label,string name,IEnumerable<Choice> choices)
  {
   var field=typeof(SafetyPitInputs).GetField(name);var owner=session.Project.Calculations.SafetyPit;var value=field.GetValue(owner);string selected=value==null?null:Convert.ToString(value,CultureInfo.InvariantCulture);
   var list=new List<Choice>{new Choice{Id=null,Name="—"}};list.AddRange(choices);if(selected!=null&&!list.Any(c=>c.Id==selected))list.Add(new Choice{Id=selected,Name=selected});
   var box=new ComboBox{ItemsSource=list,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=list.FindIndex(v=>v.Id==selected),Padding=new Thickness(7),MinHeight=32};safetyPitChoices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=Managed("Calculations.SafetyPit."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers[managed.Key]=()=>{if(managed.IsEdited(session.Project))return;var current=field.GetValue(owner);string id=current==null?null:Convert.ToString(current,CultureInfo.InvariantCulture);programmatic=true;box.SelectedIndex=list.FindIndex(v=>v.Id==id);programmatic=false;};}
   box.SelectionChanged+=(sender,args)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;string id=choice==null?null:choice.Id;field.SetValue(owner,field.FieldType==typeof(string)?(object)id:id==null?null:(object)Int32.Parse(id,CultureInfo.InvariantCulture));if(managed!=null)ManagedEdited(managed);session.Dirty=true;InvalidateSafetyPitDisplay();if(name.EndsWith("Count")||name.EndsWith("Allocation"))ShowPage("CALCULATIONS");RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);content.Children.Add(FormRow(label,panel));
  }
  void PitPercentage(string name,string label)
  {
   var field=typeof(SafetyPitInputs).GetField(name);var owner=session.Project.Calculations.SafetyPit;var value=(double?)field.GetValue(owner);string path="Calculations.SafetyPit."+name,draft;
   var box=new TextBox{Text=safetyPitDrafts.TryGetValue(path,out draft)?draft:value.HasValue?N(value):"",Width=150,HorizontalAlignment=HorizontalAlignment.Left};editors[path]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   box.TextChanged+=(sender,args)=>{safetyPitDrafts[path]=box.Text;safetyPitDraftErrors.Remove(path);double number;double? parsed=null;if(!String.IsNullOrWhiteSpace(box.Text)){if(Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&ChassisReactionSolver.Finite(number))parsed=number;else safetyPitDraftErrors[path]=label+": finite percentage required.";}field.SetValue(owner,parsed);session.Dirty=true;InvalidateSafetyPitDisplay();RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);content.Children.Add(FormRow(label,panel));
  }
  void RefreshSafetyPitMassSummary()
  {
   if(safetyPitMassSummary==null)return;var c=session.Project.Calculations;safetyPitMassSummary.Text="Q "+N(session.Project.Elevator.CapacityKg)+" kg · P "+N(c.CarMassKg)+" kg · counterweight "+N(c.CounterweightMassKg)+" kg";
   foreach(string side in new[]{"Car","Counterweight"}){double? mass=side=="Car"?c.CarSideAdditionalSuspendedMassKg:c.CounterweightSideAdditionalSuspendedMassKg;bool malformed=calculationDraftErrors.ContainsKey("Calculations."+side+"SideAdditionalSuspendedMassKg");if(safetyPitAllocationPanels.ContainsKey(side))safetyPitAllocationPanels[side].Visibility=mass>0||malformed?Visibility.Visible:Visibility.Collapsed;if(safetyPitAllocationSummaries.ContainsKey(side))safetyPitAllocationSummaries[side].Text=malformed?side+" additional mass: invalid number.":side+" additional mass: "+N(mass)+" kg";}
  }
  void RenderSafetyPitResults()
  {
   if(safetyPitResults==null)return;RefreshSafetyPitMassSummary();safetyPitResults.Children.Clear();var parent=content;content=safetyPitResults;
   try{
    var r=safetyPitResult;ApplySafetyPitDraftValidation(r);bool current=SafetyPitCalculationEngine.IsCurrent(r,session.Project);
    if(!current){foreach(var error in RelevantSafetyPitDraftErrors)Note(error.Value);return;}
    foreach(var load in new[]{r.P5,r.P6,r.P7}){
     if(load.PerComponentLoadN.HasValue){
      string text=load.Id+" · "+load.Application+"\n"+N(load.PerComponentLoadN)+" N per "+(load.Id=="P5"?"car guide rail":"buffer pit support")+" ("+N(load.PerComponentLoadN/1000)+" kN) · "+load.ComponentCount+" component(s) · "+N(load.GroupLoadN)+" N total group";
      content.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Foreground=ink,Margin=new Thickness(0,4,0,9)});
     }else {Note(load.Id+" · "+load.Application+" · "+StatusWord(load.Status)+" · load unavailable");foreach(var issue in load.Issues)Note(MessageText(issue.Message));}
    }
    CalculationGroup("Equations & event masses",()=>{foreach(var load in new[]{r.P5,r.P6,r.P7}){Note(load.Id+" · "+StatusWord(load.Status)+" · participating mass "+N(load.ParticipatingMassKg)+" kg");foreach(var detail in load.Details)Note(detail);foreach(var issue in load.Issues)Note(issue.Field+": "+MessageText(issue.Message));}Note("Rule version: "+r.RuleVersion);},false);
   }finally{content=parent;}
  }
 }
}
