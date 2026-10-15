using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // RAILS (DESIGN-FLOW "Guide rails"): in order machine supported on the rails, car profile, car bracket spacing, counterweight
 // profile, counterweight bracket spacing. Each value shows its recommendation as the preset, locks when edited and has Reset to
 // preset. A choice that does not pass is kept; its result is in the calculation sheet and the Design Notes, not on this page.
 sealed class RailsPage:PageView
 {
  readonly RailsViewModel vm=new RailsViewModel();
  public RailsPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);
   Form.PresetRefreshers.Clear();Form.PresetChoices.Clear();
   Choice(ProjectPresets.MachineOnRailsKey,new[]{"Yes","No"},v=>v==null?null:(Convert.ToBoolean(v)?"Yes":"No"),id=>id=="Yes");
   Choice(ProjectPresets.CarProfileKey,Profiles(),v=>v as string,id=>id);
   Form.Field(Label(ProjectPresets.CarSpacingKey),ProjectPresets.CarSpacingKey);
   Choice(ProjectPresets.CwProfileKey,Profiles(),v=>v as string,id=>id);
   Form.Field(Label(ProjectPresets.CwSpacingKey),ProjectPresets.CwSpacingKey);
   Form.CalculationGroup("Advanced",()=>{
    Form.Field(Label("Rails.MachineRailCount"),"Rails.MachineRailCount");
    ShownWhen(()=>GuideRailFieldRules.MachineValuesShown(Presets.Context(Session.Project)));
    Form.Field(Label("Rails.ClipPushThroughN"),"Rails.ClipPushThroughN");
    ShownWhen(()=>GuideRailFieldRules.PushThroughShown(Presets.Context(Session.Project)));
    Form.Field(Label("Rails.CwShoeSpacingMm"),"Rails.CwShoeSpacingMm");
    Form.Field(Label("Rails.StructureDeflectionMm"),"Rails.StructureDeflectionMm");
    Choice("Rails.CarShoeType",new[]{RailProfile.Sliding,RailProfile.Roller},v=>v as string,id=>id);
    Form.Field(Label("Rails.CarShoeLiningMm"),"Rails.CarShoeLiningMm");
    ShownWhen(()=>GuideRailFieldRules.LiningShown(Presets.Context(Session.Project),true));
    Choice("Rails.CwShoeType",new[]{RailProfile.Sliding,RailProfile.Roller},v=>v as string,id=>id);
    Form.Field(Label("Rails.CwShoeLiningMm"),"Rails.CwShoeLiningMm");
    ShownWhen(()=>GuideRailFieldRules.LiningShown(Presets.Context(Session.Project),false));
    Choice("Rails.A5Band",new[]{RailProfile.Over12,RailProfile.Between8And12},v=>v as string,id=>id);
    foreach(var f in RailBracketFields.Of(RailBracketFields.Car))Number(f);
    foreach(var f in RailBracketFields.Of(RailBracketFields.Cw))Number(f);
   },false);
  }
  // The row just added is shown only while it affects the result; it follows the other inputs as the editors refresh.
  void ShownWhen(Func<bool> visible)
  {
   var row=Form.FormRows[Form.FormRows.Count-1];
   Action update=()=>row.Visibility=visible()?Visibility.Visible:Visibility.Collapsed;
   Form.PresetRefreshers.Add(update);update();
  }
  static string Label(string key){var f=ProjectPresets.Find(key);return f==null?key:f.Label;}
  string[] Profiles()
  {
   var lib=Presets.Library;
   return lib==null?new string[0]:lib.GuideRailsOrDefault().Profiles.Select(p=>p.Name).ToArray();
  }
  static string ShownName(string id)
  {
   if(id==RailProfile.Over12)return "Over 12 %";
   if(id==RailProfile.Between8And12)return "8 % to 12 %";
   return id;
  }
  // A number editor of a bracket value: shows the effective value (the preset of the profile until typed), locks when edited, Reset to preset.
  void Number(PresetField field)
  {
   var inv=System.Globalization.CultureInfo.InvariantCulture;
   var box=new TextBox{MinWidth=130};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);Form.Editors[field.Key]=box;
   var hint=new TextBlock{FontSize=11,Foreground=System.Windows.Media.Brushes.DarkRed,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};
   var panel=new StackPanel{MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(box);panel.Children.Add(hint);
   bool programmatic=false;string error=null;
   Action show=()=>{programmatic=true;var v=field.Effective(Presets.Context(Session.Project));box.Text=v==null?"":Convert.ToDouble(v,inv).ToString("0.###",inv);programmatic=false;error=null;hint.Visibility=Visibility.Collapsed;};
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;double v;error=null;
    if(String.IsNullOrWhiteSpace(box.Text))field.Reset(Session.Project);
    else if(!Double.TryParse(box.Text,System.Globalization.NumberStyles.Float,inv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))error="Enter a number.";
    else if(v<=0)error="Enter a number above zero.";
    else field.Edit(Session.Project,v);
    hint.Text=error??"";hint.Visibility=error==null?Visibility.Collapsed:Visibility.Visible;
    Session.Dirty=true;Shell.PresetEditing.RefreshPresetEditors();Shell.Live.Design=null;Shell.RefreshProjectStatus();
   };
   Form.Committers.Add(()=>{if(error!=null)throw new ArgumentException(field.Label+": "+error);});
   box.ContextMenu=Shell.PresetEditing.ResetMenu(field);
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  // A list editor of a preset-managed rail value: shows the effective value, locks when another entry is chosen, Reset to preset.
  void Choice(string key,string[] ids,Func<object,string> toId,Func<string,object> fromId)
  {
   var field=ProjectPresets.Find(key);
   var combo=new ComboBox{ItemsSource=ids.Select(v=>new Common.Choice{Id=v,Name=ShownName(v)}).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,5,8,5)};
   System.Windows.Automation.AutomationProperties.SetName(combo,field.Label);Form.PresetChoices[key]=combo;
   bool programmatic=false;
   Action show=()=>{programmatic=true;combo.SelectedValue=toId(field.Effective(Presets.Context(Session.Project)));programmatic=false;};
   combo.SelectionChanged+=(s,e)=>{
    if(programmatic)return;var choice=combo.SelectedItem as Common.Choice;if(choice==null)return;
    field.Store(Session.Project,fromId(choice.Id));Shell.PresetEditing.Edited(field);Shell.PresetEditing.RefreshPresetEditors();
    Shell.Live.Design=null;Shell.Updates.InvalidateCalculation();Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();
   };
   combo.ContextMenu=Shell.PresetEditing.ResetMenu(field);
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(combo);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
 }
}
