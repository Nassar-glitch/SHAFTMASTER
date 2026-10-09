using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.Suspension;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // SUSPENSION: ropes and roping, and the rope / traction data (generic preset editors: lock on edit, follow while
 // unlocked, Reset to preset).
 sealed class SuspensionPage:PageView
 {
  static readonly CultureInfo PresetInv=CultureInfo.InvariantCulture;
  readonly SuspensionViewModel vm=new SuspensionViewModel();
  public SuspensionPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);Form.Fields(SuspensionViewModel.Fields);
   RopeTractionInputs();
  }
  ContextMenu ResetMenu(PresetField field,Action after)
  {
   var menu=new ContextMenu();var item=new MenuItem{Header="Reset to preset"};
   item.Click+=(s,e)=>Shell.Guard(()=>{Presets.Reset(Session,field);after();Shell.PresetEditing.RefreshPresetEditors();Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();});
   menu.Items.Add(item);return menu;
  }
  void PresetNumber(PresetField field,Func<double,string> validate,Func<bool> visible=null)
  {
   var box=new TextBox{MinWidth=130};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);Form.Editors[field.Key]=box;
   var hint=new TextBlock{FontSize=11,Foreground=Brushes.DarkRed,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};
   var panel=new StackPanel{MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(box);panel.Children.Add(hint);var row=Form.FormRow(field.Label,panel);
   bool programmatic=false;string error=null;
   Action show=()=>{programmatic=true;var v=field.Effective(Presets.Context(Session.Project));box.Text=v==null?"":Convert.ToDouble(v,PresetInv).ToString("0.###",PresetInv);programmatic=false;error=null;hint.Visibility=Visibility.Collapsed;};
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;double v;error=null;
    if(String.IsNullOrWhiteSpace(box.Text)){field.Edit(Session.Project,null);}
    else if(!Double.TryParse(box.Text,NumberStyles.Float,PresetInv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))error="Enter a number.";
    else{error=validate(v);if(error==null)field.Edit(Session.Project,v);}
    hint.Text=error??"";hint.Visibility=error==null?Visibility.Collapsed:Visibility.Visible;
    Session.Dirty=true;Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();
   };
   Form.Committers.Add(()=>{if(error!=null)throw new ArgumentException(field.Label+": "+error);});
   box.ContextMenu=ResetMenu(field,show);
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();if(visible!=null)row.Visibility=visible()?Visibility.Visible:Visibility.Collapsed;});
   show();if(visible!=null)row.Visibility=visible()?Visibility.Visible:Visibility.Collapsed;Content.Children.Add(row);
  }
  void PresetText(PresetField field)
  {
   var box=new TextBox{MinWidth=260};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);Form.Editors[field.Key]=box;
   bool programmatic=false;
   Action show=()=>{programmatic=true;box.Text=(field.Effective(Presets.Context(Session.Project)) as string)??"";programmatic=false;};
   box.TextChanged+=(s,e)=>{if(programmatic)return;field.Edit(Session.Project,String.IsNullOrWhiteSpace(box.Text)?null:box.Text.Trim());Session.Dirty=true;Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();};
   box.ContextMenu=ResetMenu(field,show);Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  void PresetChoice(PresetField field,string[] values,Func<string,string> label)
  {
   var combo=new ComboBox{ItemsSource=values.Select(v=>new Choice{Id=v,Name=label(v)}).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,5,8,5)};
   System.Windows.Automation.AutomationProperties.SetName(combo,field.Label);Form.PresetChoices[field.Key]=combo;
   bool programmatic=false;
   Action show=()=>{programmatic=true;combo.SelectedValue=field.Effective(Presets.Context(Session.Project)) as string;programmatic=false;};
   combo.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=combo.SelectedItem as Choice;field.Edit(Session.Project,choice==null?null:choice.Id);Session.Dirty=true;Shell.PresetEditing.RefreshPresetEditors();Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();};
   combo.ContextMenu=ResetMenu(field,show);Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(combo);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  string EffectiveGroove(){return SuspensionViewModel.EffectiveGroove(Presets,Session.Project);}
  // Rope and traction data.
  void RopeTractionInputs()
  {
   Form.PresetRefreshers.Clear();Form.PresetChoices.Clear();
   Heading("Rope and traction data");
   if(Presets.Library==null){Note("The preset library could not be read: "+Presets.Error);}
   Func<double,string> positive=SuspensionViewModel.Positive;
   PresetText(PresetFields.RopeDesignation);
   PresetNumber(PresetFields.MinimumBreakingLoad,positive);PresetNumber(PresetFields.RopeMass,positive);
   PresetChoice(PresetFields.GrooveType,GrooveTypes.All,GrooveTypes.Label);
   PresetNumber(PresetFields.UndercutAngle,v=>SuspensionViewModel.UndercutAngle(v,EffectiveGroove()),()=>GrooveTypes.NeedsUndercut(EffectiveGroove()));
   PresetNumber(PresetFields.GrooveAngle,v=>SuspensionViewModel.GrooveAngle(v,EffectiveGroove()),()=>GrooveTypes.NeedsGrooveAngle(EffectiveGroove()));
   PresetNumber(PresetFields.TravellingCable,positive);
   PresetNumber(PresetFields.BrakingRetardation,SuspensionViewModel.BrakingRetardation);
   PresetNumber(PresetFields.DeflectorMass,positive,()=>Presets.Context(Session.Project).DeflectorPitchMm.HasValue);
   PresetChoice(PresetFields.TractionLimitingMethod,TractionLimitingMethods.All,TractionLimitingMethods.Label);
   // Unlocked rope data follow the rope diameter as it is typed.
   TextBox diameter;
   if(Form.Editors.TryGetValue("Suspension.RopeDiameterMm",out diameter))diameter.TextChanged+=(s,e)=>{double v;
    if(String.IsNullOrWhiteSpace(diameter.Text))Session.Project.Suspension.RopeDiameterMm=null;
    else if(Double.TryParse(diameter.Text,NumberStyles.Float,PresetInv,out v)&&!Double.IsNaN(v)&&!Double.IsInfinity(v))Session.Project.Suspension.RopeDiameterMm=v;
    else return;
    Shell.PresetEditing.RefreshPresetEditors();};
  }
 }
}
