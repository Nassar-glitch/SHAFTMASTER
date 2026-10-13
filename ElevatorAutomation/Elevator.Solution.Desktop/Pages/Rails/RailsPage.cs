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
    Form.Field(Label("Rails.ClipPushThroughN"),"Rails.ClipPushThroughN");
    Form.Field(Label("Rails.CwShoeSpacingMm"),"Rails.CwShoeSpacingMm");
    Form.Field(Label("Rails.StructureDeflectionMm"),"Rails.StructureDeflectionMm");
    Choice("Rails.CarShoeType",new[]{RailProfile.Sliding,RailProfile.Roller},v=>v as string,id=>id);
    Form.Field(Label("Rails.CarShoeLiningMm"),"Rails.CarShoeLiningMm");
    Choice("Rails.CwShoeType",new[]{RailProfile.Sliding,RailProfile.Roller},v=>v as string,id=>id);
    Form.Field(Label("Rails.CwShoeLiningMm"),"Rails.CwShoeLiningMm");
    Choice("Rails.A5Band",new[]{RailProfile.Over12,RailProfile.Between8And12},v=>v as string,id=>id);
   },false);
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
    field.Store(Session.Project,fromId(choice.Id));Shell.PresetEditing.Edited(field);
    Shell.Live.Design=null;Shell.Updates.InvalidateCalculation();Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();
   };
   combo.ContextMenu=Shell.PresetEditing.ResetMenu(field);
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(combo);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
 }
}
