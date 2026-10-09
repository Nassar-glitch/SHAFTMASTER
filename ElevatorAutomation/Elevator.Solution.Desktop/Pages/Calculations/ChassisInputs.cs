using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Editors of the chassis calculation inputs (ChassisCalculationInputs), shared by the Basic and Advanced panels. Invalid
 // text keeps its message and clears the value (LiveCalculation drafts).
 sealed class ChassisInputs:ViewPart
 {
  readonly CalculationsPage page;
  public ChassisInputs(MainWindow shell,CalculationsPage page):base(shell){this.page=page;}
  public void CalcField(string label,string name,string hint=null,bool compact=false)
  {
   string path="Calculations."+name;var field=typeof(ChassisCalculationInputs).GetField(name);var owner=Session.Project.Calculations;var v=field.GetValue(owner);
   var panel=new StackPanel();bool numeric=field.FieldType!=typeof(string);if(numeric){panel.MaxWidth=250;panel.HorizontalAlignment=HorizontalAlignment.Left;}if(compact){panel.Width=185;panel.Margin=new Thickness(0,0,12,10);panel.Children.Add(new TextBlock{Text=label,FontSize=13,Margin=new Thickness(0,0,0,5)});}
   string draft;var box=new TextBox{Text=Live.ChassisDrafts.TryGetValue(path,out draft)?draft:(v==null?"":Convert.ToString(v,CultureInfo.InvariantCulture)),MinWidth=numeric?130:0};Form.Editors[path]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);panel.Children.Add(box);
   var message=new TextBlock{Text=hint??"",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(0,4,0,0)};message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;panel.Children.Add(message);
   var managed=PresetFlow.Managed(path);bool programmatic=false;
   if(managed!=null){box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);Form.ManagedRefreshers[path]=()=>{if(managed.IsEdited(Session.Project))return;var current=field.GetValue(owner);programmatic=true;Live.ClearDraft(path);box.Text=current==null?"":Convert.ToString(current,CultureInfo.InvariantCulture);message.Text=hint??"";message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;programmatic=false;};}
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;
    // Invalid text clears the canonical value instead of leaving an old hidden number active.
    object parsed=Live.EnterChassisText(path,label,box.Text,numeric);
    var errors=Live.ChassisDraftErrors;
    field.SetValue(owner,parsed);Session.Dirty=true;Live.Design=null;message.Text=errors.ContainsKey(path)?errors[path]:hint??"";message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;message.Foreground=errors.ContainsKey(path)?Brushes.DarkRed:Brushes.SlateGray;
    if(managed!=null&&!errors.ContainsKey(path))Shell.PresetEditing.Edited(managed);
    Shell.Updates.InvalidateCalculation();Shell.RefreshProjectStatus();
   };Content.Children.Add(compact?(FrameworkElement)panel:Form.FormRow(label,panel));
  }
  public void SelectCalculation(string label,string name,IEnumerable<Choice> choices)
  {
   var field=typeof(ChassisCalculationInputs).GetField(name);var owner=Session.Project.Calculations;string selected=field.GetValue(owner) as string;
   var list=new List<Choice>{new Choice{Id=null,Name="—"}};list.AddRange(choices);
   if(selected!=null&&!list.Any(c=>c.Id==selected))list.Add(new Choice{Id=selected,Name=selected});
   var box=new ComboBox{ItemsSource=list,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=list.FindIndex(c=>c.Id==selected),Padding=new Thickness(8),MinHeight=34};page.Choices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=PresetFlow.Managed("Calculations."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);Form.ManagedRefreshers["Calculations."+name]=()=>{if(managed.IsEdited(Session.Project))return;programmatic=true;box.SelectedIndex=list.FindIndex(c=>c.Id==(field.GetValue(owner) as string));programmatic=false;};}
   box.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;field.SetValue(owner,choice==null?null:choice.Id);if(managed!=null)Shell.PresetEditing.Edited(managed);Session.Dirty=true;Live.Design=null;Shell.Updates.InvalidateCalculation();Shell.RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(label,panel));
  }
  public void WeightChoice(string label,string name)
  {
   var field=typeof(ChassisCalculationInputs).GetField(name);var owner=Session.Project.Calculations;var v=(bool?)field.GetValue(owner);
   var choices=new[]{new Choice{Id="missing",Name="—"},new Choice{Id="include",Name="Include"},new Choice{Id="exclude",Name="Exclude"}};
   var box=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=v.HasValue?(v.Value?"include":"exclude"):"missing",Padding=new Thickness(8)};page.Choices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=PresetFlow.Managed("Calculations."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);Form.ManagedRefreshers["Calculations."+name]=()=>{if(managed.IsEdited(Session.Project))return;var current=(bool?)field.GetValue(owner);programmatic=true;box.SelectedValue=current.HasValue?(current.Value?"include":"exclude"):"missing";programmatic=false;};}
   box.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;string value=choice==null?null:choice.Id;field.SetValue(owner,value=="include"?(bool?)true:value=="exclude"?(bool?)false:null);if(managed!=null)Shell.PresetEditing.Edited(managed);Session.Dirty=true;Live.Design=null;Shell.Updates.InvalidateCalculation();Shell.RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(label,panel));
  }
 }
}
