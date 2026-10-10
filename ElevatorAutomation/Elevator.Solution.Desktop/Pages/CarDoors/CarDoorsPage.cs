using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Doors;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // CAR & DOORS: the door type, the clear opening and handing, the Advanced dimensions (every clearance, editable, Reset per field),
 // the landing shown with the car, and the calculated geometry. A door value the user edits is locked; the others follow.
 sealed class CarDoorsPage:PageView
 {
  readonly CarDoorsViewModel vm=new CarDoorsViewModel();
  readonly Dictionary<string,TextBox> boxes=new Dictionary<string,TextBox>();
  readonly Dictionary<string,Button> resets=new Dictionary<string,Button>();
  TextBlock inUse,fit,derived;ComboBox handing;bool programmatic;
  public CarDoorsPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   boxes.Clear();resets.Clear();inUse=null;fit=null;derived=null;handing=null;
   var project=Session.Project;string type=DoorFlow.TypeOf(project);
   Heading("Car door");
   var typePanel=new StackPanel{MaxWidth=300,HorizontalAlignment=HorizontalAlignment.Left};
   var typeBox=new ComboBox{ItemsSource=CarDoorsViewModel.Types(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=type,Padding=new Thickness(8)};
   AutomationProperties.SetName(typeBox,"Door type");typePanel.Children.Add(typeBox);Form.PresetChoices["Doors.Type"]=typeBox;
   typeBox.SelectionChanged+=(s,e)=>{var id=typeBox.SelectedValue as string;if(id==null||id==DoorFlow.TypeOf(Session.Project))return;DoorFlow.SetType(Session.Project,id);Changed();Shell.ShowPage(Key);};
   Content.Children.Add(Form.FormRow("Door type",typePanel));
   if(DoorTypes.IsAutomatic(type))BuildAutomatic(type);
   Heading("Car display landing");var select=new ComboBox{ItemsSource=CarDoorsViewModel.DisplayFloors(Session.Project),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=Session.Project.Car.DisplayFloorId,Padding=new Thickness(8),Margin=new Thickness(0,0,0,14)};
   select.SelectionChanged+=(s,e)=>{Session.Project.Car.DisplayFloorId=select.SelectedValue as string;Session.Dirty=true;Live.Design=null;};Content.Children.Add(select);
   Heading("Calculated geometry");derived=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,15),Foreground=Brushes.SlateGray};Content.Children.Add(derived);
   Refresh(null);
  }
  void BuildAutomatic(string type)
  {
   // Clear opening: the field is empty while the size is selected by the rules; typing locks it.
   var panel=new StackPanel{MaxWidth=300,HorizontalAlignment=HorizontalAlignment.Left};
   var open=Box(DoorSettings.OpeningKey,"Clear opening");panel.Children.Add(open);
   inUse=new TextBlock{Margin=new Thickness(1,5,0,0),FontSize=11,Foreground=Brushes.SlateGray};AutomationProperties.SetName(inUse,"Clear opening in use");panel.Children.Add(inUse);
   fit=new TextBlock{Margin=new Thickness(1,5,0,0),FontSize=12,Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};AutomationProperties.SetName(fit,"Door fit message");panel.Children.Add(fit);
   var row=Form.FormRow(DoorFlow.Label(DoorSettings.OpeningKey),panel);var reset=ResetButton(DoorSettings.OpeningKey,DoorFlow.Label(DoorSettings.OpeningKey));reset.HorizontalAlignment=HorizontalAlignment.Left;reset.Margin=new Thickness(0,6,0,0);panel.Children.Add(reset);Content.Children.Add(row);
   if(type==DoorTypes.Automatic2S)
   {
    var hp=new StackPanel{MaxWidth=300,HorizontalAlignment=HorizontalAlignment.Left};
    handing=new ComboBox{ItemsSource=CarDoorsViewModel.Handings(),DisplayMemberPath="Name",SelectedValuePath="Id",Padding=new Thickness(8)};AutomationProperties.SetName(handing,"Door handing");
    handing.SelectedValue=DoorFlow.IsLocked(Session.Project,DoorSettings.HandingKey)?DoorSettings.Read(Session.Project).Handing:DoorTypes.Auto;
    handing.SelectionChanged+=(s,e)=>{if(programmatic)return;var id=handing.SelectedValue as string;if(id==null)return;DoorFlow.SetHanding(Session.Project,id);Changed();Refresh(null);};
    Form.PresetChoices["Doors.Handing"]=handing;hp.Children.Add(handing);Content.Children.Add(Form.FormRow("Handing",hp));
   }
   // Advanced dimensions: every clearance, with Reset per field.
   var parent=Content;var group=new StackPanel{Margin=new Thickness(12,12,12,6)};Content=group;
   try{foreach(var key in CarDoorsViewModel.ClearanceKeys(type))AddClearance(type,key);}finally{Content=parent;}
   parent.Children.Add(new Expander{Header="Advanced dimensions",IsExpanded=false,Content=group,Background=Brushes.White,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)});
  }
  TextBox Box(string key,string label)
  {
   var box=new TextBox{MinWidth=130};AutomationProperties.SetName(box,label);boxes[key]=box;Form.Editors[key]=box;
   box.TextChanged+=(s,e)=>{if(programmatic)return;Typed(key,box.Text);};
   return box;
  }
  Button ResetButton(string key,string label)
  {
   var b=new Button{Content="Reset",Padding=new Thickness(10,4,10,4),Margin=new Thickness(8,0,0,0),Background=Brushes.White,Foreground=Ink,BorderBrush=new SolidColorBrush(Color.FromRgb(193,206,216)),Cursor=System.Windows.Input.Cursors.Hand};
   AutomationProperties.SetName(b,"Reset "+label);resets[key]=b;
   b.Click+=(s,e)=>Shell.Guard(()=>{DoorFlow.Reset(Session.Project,key);Changed();Refresh(null);});
   return b;
  }
  void AddClearance(string type,string key)
  {
   string label=CarDoorsViewModel.Label(type,key);var panel=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left};
   var box=Box(key,label);box.Width=130;panel.Children.Add(box);panel.Children.Add(ResetButton(key,label));
   Content.Children.Add(Form.FormRow(label,panel));
  }
  // A typed value locks the field; an empty or unreadable text resets (empty) or is ignored (unreadable) until it parses.
  void Typed(string key,string text)
  {
   if(String.IsNullOrWhiteSpace(text)){DoorFlow.SetNumber(Session.Project,key,null);Changed();Refresh(key);return;}
   double d;if(!Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out d)||Double.IsNaN(d)||Double.IsInfinity(d)||d<0)return;
   DoorFlow.SetNumber(Session.Project,key,d);Changed();Refresh(key);
  }
  // An edit makes the design, and the calculations that use the car depth, outdated; unlocked presets follow.
  void Changed()
  {
   Session.Dirty=true;Live.Design=null;
   Shell.Updates.InvalidateCarGuideForces();Shell.Updates.InvalidateCalculation();Shell.Updates.InvalidateRopeTraction();
   Shell.PresetEditing.Apply(null,false);Shell.RefreshProjectStatus();
  }
  // Shows the values in effect: unlocked fields follow, the typed one is left alone.
  void Refresh(string typing)
  {
   programmatic=true;
   try
   {
    var l=CarDoorsViewModel.Layout(Session,Live,Context);var project=Session.Project;
    foreach(var pair in boxes)
    {
     bool locked=DoorFlow.IsLocked(project,pair.Key);
     if(pair.Key!=typing){var eff=DoorFlow.Effective(l,pair.Key);string text=pair.Key==DoorSettings.OpeningKey&&!locked?"":CarDoorsViewModel.Format(eff);if(pair.Value.Text!=text)pair.Value.Text=text;}
     Button reset;if(resets.TryGetValue(pair.Key,out reset))reset.IsEnabled=locked;
    }
    if(handing!=null)handing.SelectedValue=DoorFlow.IsLocked(project,DoorSettings.HandingKey)?DoorSettings.Read(project).Handing:DoorTypes.Auto;
    if(inUse!=null)inUse.Text=l==null?"":"In use: "+CarDoorsViewModel.Format(l.OpeningMm)+" mm";
    if(fit!=null){var msg=DoorFlow.FitMessage(l);fit.Text=msg??"";fit.Visibility=msg==null?Visibility.Collapsed:Visibility.Visible;}
    if(derived!=null)derived.Text=DerivedGeometry.Text(Session,Live,Context)??"";
   }
   finally{programmatic=false;}
  }
 }
}
