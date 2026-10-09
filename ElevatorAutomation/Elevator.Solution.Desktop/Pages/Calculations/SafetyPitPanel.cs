using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Safety gear & pit loads P5–P7: safety gear, guides, buffers, event-mass allocation and the live result.
 sealed class SafetyPitPanel:ViewPart
 {
  public SafetyPitPanel(MainWindow shell):base(shell){}
  public StackPanel Results;
  TextBlock massSummary;
  readonly Dictionary<string,StackPanel> allocationPanels=new Dictionary<string,StackPanel>();
  readonly Dictionary<string,TextBlock> allocationSummaries=new Dictionary<string,TextBlock>();
  // The combo boxes of the section (by SafetyPitInputs field name).
  public readonly Dictionary<string,ComboBox> Choices=new Dictionary<string,ComboBox>();
  public void ResetView(){Results=null;massSummary=null;allocationPanels.Clear();allocationSummaries.Clear();Choices.Clear();}
  public void Section()
  {
   var c=Session.Project.Calculations;if(c.SafetyPit==null)c.SafetyPit=new SafetyPitInputs();var s=c.SafetyPit;Choices.Clear();allocationPanels.Clear();allocationSummaries.Clear();
   bool expanded=s.SafetyGearType!=null||s.CarGuideCount.HasValue||s.CarBufferCount.HasValue||s.CounterweightBufferCount.HasValue;
   Form.CalculationGroup("Safety gear & pit loads",()=>{
    massSummary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,12)};Content.Children.Add(massSummary);RefreshMassSummary();
    PitChoice("Car safety gear","SafetyGearType",new[]{new Choice{Id=ProjectPresets.Progressive,Name="Progressive"},new Choice{Id=ProjectPresets.Instantaneous,Name="Instantaneous"},new Choice{Id="Other",Name="Other"}});
    PitChoice("Car guide rails","CarGuideCount",new[]{new Choice{Id="2",Name="2"},new Choice{Id="3",Name="3"},new Choice{Id="4",Name="4"}});
    PitChoice("Buffer type","BufferType",new[]{new Choice{Id=ProjectPresets.EnergyAccumulation,Name="Energy accumulation"},new Choice{Id=ProjectPresets.EnergyDissipation,Name="Energy dissipation"}});
    var quantities=Enumerable.Range(1,4).Select(n=>new Choice{Id=n.ToString(CultureInfo.InvariantCulture),Name=n.ToString(CultureInfo.InvariantCulture)}).ToArray();
    PitChoice("Car buffers","CarBufferCount",quantities);PitChoice("Counterweight buffers","CounterweightBufferCount",quantities);
    if(s.CarBufferCount>1)PitSharing("CarBufferSharing","Car buffer sharing");if(s.CounterweightBufferCount>1)PitSharing("CounterweightBufferSharing","CW buffer sharing");
    foreach(string side in new[]{"Car","Counterweight"}){var parent=Content;var panel=new StackPanel();Content=panel;PitAllocation(side);Content=parent;parent.Children.Add(panel);allocationPanels[side]=panel;}
    Results=new StackPanel{Margin=new Thickness(0,12,0,0)};Content.Children.Add(Results);Render();
   },expanded);
  }
  void PitSharing(string field,string label)
  {PitChoice(label,field,new[]{new Choice{Id="EqualSharing",Name="Equal sharing"},new Choice{Id="Unresolved",Name="Unresolved"}});}
  void PitAllocation(string side)
  {
   var s=Session.Project.Calculations.SafetyPit;string field=side+"AdditionalMassAllocation";
   var summary=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,12)};Content.Children.Add(summary);allocationSummaries[side]=summary;
   PitChoice(side+" event mass allocation",field,new[]{new Choice{Id="AllParticipates",Name="All participates"},new Choice{Id="NoneParticipates",Name="None participates"},new Choice{Id="FractionParticipates",Name="Percentage participates"}});
   if((string)typeof(SafetyPitInputs).GetField(field).GetValue(s)=="FractionParticipates")PitPercentage(side+"EventAdditionalMassPercent",side+" participating mass (%)");
  }
  void PitChoice(string label,string name,IEnumerable<Choice> choices)
  {
   var field=typeof(SafetyPitInputs).GetField(name);var owner=Session.Project.Calculations.SafetyPit;var value=field.GetValue(owner);string selected=value==null?null:Convert.ToString(value,CultureInfo.InvariantCulture);
   var list=new List<Choice>{new Choice{Id=null,Name="—"}};list.AddRange(choices);if(selected!=null&&!list.Any(c=>c.Id==selected))list.Add(new Choice{Id=selected,Name=selected});
   var box=new ComboBox{ItemsSource=list,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=list.FindIndex(v=>v.Id==selected),Padding=new Thickness(7),MinHeight=32};Choices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=PresetFlow.Managed("Calculations.SafetyPit."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);Form.ManagedRefreshers[managed.Key]=()=>{if(managed.IsEdited(Session.Project))return;var current=field.GetValue(owner);string id=current==null?null:Convert.ToString(current,CultureInfo.InvariantCulture);programmatic=true;box.SelectedIndex=list.FindIndex(v=>v.Id==id);programmatic=false;};}
   box.SelectionChanged+=(sender,args)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;string id=choice==null?null:choice.Id;field.SetValue(owner,field.FieldType==typeof(string)?(object)id:id==null?null:(object)Int32.Parse(id,CultureInfo.InvariantCulture));if(managed!=null)Shell.PresetEditing.Edited(managed);Session.Dirty=true;Shell.Updates.InvalidateSafetyPit();if(name.EndsWith("Count")||name.EndsWith("Allocation"))Shell.ShowPage("CALCULATIONS");Shell.RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(label,panel));
  }
  void PitPercentage(string name,string label)
  {
   var field=typeof(SafetyPitInputs).GetField(name);var owner=Session.Project.Calculations.SafetyPit;var value=(double?)field.GetValue(owner);string path="Calculations.SafetyPit."+name,draft;
   var box=new TextBox{Text=Live.SafetyPitDrafts.TryGetValue(path,out draft)?draft:value.HasValue?DisplayText.N(value):"",Width=150,HorizontalAlignment=HorizontalAlignment.Left};Form.Editors[path]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   box.TextChanged+=(sender,args)=>{var parsed=Live.EnterSafetyPitPercentage(path,label,box.Text);field.SetValue(owner,parsed);Session.Dirty=true;Shell.Updates.InvalidateSafetyPit();Shell.RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(label,panel));
  }
  void RefreshMassSummary()
  {
   if(massSummary==null)return;var c=Session.Project.Calculations;massSummary.Text="Q "+DisplayText.N(Session.Project.Elevator.CapacityKg)+" kg · P "+DisplayText.N(c.CarMassKg)+" kg · counterweight "+DisplayText.N(c.CounterweightMassKg)+" kg";
   foreach(string side in new[]{"Car","Counterweight"}){double? mass=side=="Car"?c.CarSideAdditionalSuspendedMassKg:c.CounterweightSideAdditionalSuspendedMassKg;bool malformed=Live.ChassisDraftErrors.ContainsKey("Calculations."+side+"SideAdditionalSuspendedMassKg");if(allocationPanels.ContainsKey(side))allocationPanels[side].Visibility=mass>0||malformed?Visibility.Visible:Visibility.Collapsed;if(allocationSummaries.ContainsKey(side))allocationSummaries[side].Text=malformed?side+" additional mass: invalid number.":side+" additional mass: "+DisplayText.N(mass)+" kg";}
  }
  public void Render()
  {
   if(Results==null)return;RefreshMassSummary();Results.Children.Clear();var parent=Content;Content=Results;
   try{
    var r=Live.SafetyPit;Live.ApplySafetyPitDraftValidation(Session.Project,r);bool current=SafetyPitCalculationEngine.IsCurrent(r,Session.Project);
    if(!current){foreach(var error in Live.RelevantSafetyPitDraftErrors(Session.Project))Note(error.Value);return;}
    foreach(var load in new[]{r.P5,r.P6,r.P7}){
     if(load.PerComponentLoadN.HasValue){
      string text=load.Id+" · "+load.Application+"\n"+DisplayText.N(load.PerComponentLoadN)+" N per "+(load.Id=="P5"?"car guide rail":"buffer pit support")+" ("+DisplayText.N(load.PerComponentLoadN/1000)+" kN) · "+load.ComponentCount+" component(s) · "+DisplayText.N(load.GroupLoadN)+" N total group";
      Content.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Ink,Margin=new Thickness(0,4,0,9)});
     }else {Note(load.Id+" · "+load.Application+" · "+DisplayText.StatusWord(load.Status)+" · load unavailable");foreach(var issue in load.Issues)Note(DisplayText.MessageText(issue.Message));}
    }
    Form.CalculationGroup("Equations & event masses",()=>{foreach(var load in new[]{r.P5,r.P6,r.P7}){Note(load.Id+" · "+DisplayText.StatusWord(load.Status)+" · participating mass "+DisplayText.N(load.ParticipatingMassKg)+" kg");foreach(var detail in load.Details)Note(detail);foreach(var issue in load.Issues)Note(issue.Field+": "+DisplayText.MessageText(issue.Message));}Note("Rule version: "+r.RuleVersion);},false);
   }finally{Content=parent;}
  }
 }
}
