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
  TextBlock inUse,fit,derived,offsetRange,offsetMessage;ComboBox handing,offsetSide;TextBox offsetBox;bool programmatic,sideSet;
  public CarDoorsPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   boxes.Clear();resets.Clear();inUse=null;fit=null;derived=null;handing=null;offsetBox=null;offsetSide=null;offsetRange=null;offsetMessage=null;
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
    handing=new ComboBox{ItemsSource=CarDoorsViewModel.Handings(),DisplayMemberPath="Name",SelectedValuePath="Id",Padding=new Thickness(8)};AutomationProperties.SetName(handing,DoorFlow.Label(DoorSettings.HandingKey));
    handing.SelectedValue=DoorFlow.IsLocked(Session.Project,DoorSettings.HandingKey)?DoorSettings.Read(Session.Project).Handing:DoorTypes.Auto;
    handing.SelectionChanged+=(s,e)=>{if(programmatic)return;var id=handing.SelectedValue as string;if(id==null)return;DoorFlow.SetHanding(Session.Project,id);Changed();Shell.ShowPage(Key);};
    Form.PresetChoices["Doors.Handing"]=handing;hp.Children.Add(handing);Content.Children.Add(Form.FormRow(DoorFlow.Label(DoorSettings.HandingKey),hp));
   }
   BuildOffset();
   // Advanced dimensions: every clearance, with Reset per field.
   var parent=Content;var group=new StackPanel{Margin=new Thickness(12,12,12,6)};Content=group;
   try{foreach(var key in CarDoorsViewModel.ClearanceKeys(type))AddClearance(type,key);}finally{Content=parent;}
   parent.Children.Add(new Expander{Header="Advanced dimensions",IsExpanded=false,Content=group,Background=Brushes.White,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)});
  }
  // Doors Offset: the distance from the shaft centre line to the opening centre line and Left / Right as seen in the plan drawing.
  // The allowed range is shown under the field; a value outside it is kept and flagged.
  void BuildOffset()
  {
   var panel=new StackPanel{MaxWidth=300,HorizontalAlignment=HorizontalAlignment.Left};
   var line=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left};
   offsetBox=new TextBox{MinWidth=100,Width=100};AutomationProperties.SetName(offsetBox,"Doors Offset");Form.Editors[DoorSettings.OffsetKey]=offsetBox;
   offsetBox.TextChanged+=(s,e)=>{if(programmatic)return;TypedOffset(offsetBox.Text,null);};
   offsetSide=new ComboBox{ItemsSource=CarDoorsViewModel.Sides(),DisplayMemberPath="Name",SelectedValuePath="Id",Padding=new Thickness(8,4,8,4),Margin=new Thickness(8,0,0,0),Width=90};
   AutomationProperties.SetName(offsetSide,"Doors Offset side");Form.PresetChoices[DoorSettings.OffsetSideKey]=offsetSide;
   offsetSide.SelectionChanged+=(s,e)=>{if(sideSet||e.AddedItems.Count==0)return;var c=e.AddedItems[0] as Choice;var id=c==null?null:c.Id;if(id==null||id==ShownSide())return;TypedOffset(offsetBox.Text,id);};
   line.Children.Add(offsetBox);line.Children.Add(offsetSide);
   var reset=ResetButton(DoorSettings.OffsetKey,"Doors Offset");line.Children.Add(reset);
   panel.Children.Add(line);
   offsetRange=new TextBlock{Margin=new Thickness(1,5,0,0),FontSize=11,Foreground=Brushes.SlateGray};AutomationProperties.SetName(offsetRange,"Doors Offset range");panel.Children.Add(offsetRange);
   offsetMessage=new TextBlock{Margin=new Thickness(1,5,0,0),FontSize=12,Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};AutomationProperties.SetName(offsetMessage,"Doors Offset message");panel.Children.Add(offsetMessage);
   Content.Children.Add(Form.FormRow(DoorFlow.Label(DoorSettings.OffsetKey),panel));
  }
  // A typed distance or a chosen side locks the offset (distance and side together); empty text resets it.
  void TypedOffset(string text,string side)
  {
   if(String.IsNullOrWhiteSpace(text)){DoorFlow.Reset(Session.Project,DoorSettings.OffsetKey);Changed();Refresh(DoorSettings.OffsetKey);return;}
   double d;if(!Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out d)||Double.IsNaN(d)||Double.IsInfinity(d)||d<0)return;
   string chosen=side??ShownSide()??DoorTypes.Left;
   DoorFlow.SetOffset(Session.Project,d,chosen);Changed();Refresh(side==null?DoorSettings.OffsetKey:null);
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
   var stackLayout=CarDoorsViewModel.Layout(Session,Live,Context);string label=CarDoorsViewModel.Label(type,key,stackLayout==null||stackLayout.StackingSide!=DoorTypes.Left);var panel=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left};
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
  // The Doors Offset in effect: the typed value (kept even when out of range) or the position in use; its side, range and message.
  // The side shown for the Doors Offset in effect (the typed value's side, or the side of the position in use); null for Manual.
  string ShownSide()
  {
   DoorLayout layout;var off=CarDoorsViewModel.Offset(Session,Live,Context,out layout);if(off==null)return null;
   var project=Session.Project;bool locked=DoorFlow.IsLocked(project,DoorSettings.OffsetKey)||DoorFlow.IsLocked(project,DoorSettings.LegacyOffsetKey);
   string preset=layout.Type==DoorTypes.Automatic2S&&layout.StackingSide!=DoorTypes.Right?DoorTypes.Right:DoorTypes.Left;
   string lockedSide=DoorSettings.Read(project).OffsetSide;
   return DoorOffset.SideOf(locked?off.RequestedShiftMm:off.PlacedShiftMm,locked&&lockedSide!=null?lockedSide:preset);
  }
  void RefreshOffset(string typing)
  {
   DoorLayout layout;var off=CarDoorsViewModel.Offset(Session,Live,Context,out layout);var project=Session.Project;
   bool locked=DoorFlow.IsLocked(project,DoorSettings.OffsetKey)||DoorFlow.IsLocked(project,DoorSettings.LegacyOffsetKey);
   if(off!=null)
   {
    bool stackRight=layout.StackingSide==DoorTypes.Right;
    string preset=layout.Type==DoorTypes.Automatic2S&&!stackRight?DoorTypes.Right:DoorTypes.Left;
    double shift=locked?off.RequestedShiftMm:off.PlacedShiftMm;
    string lockedSide=DoorSettings.Read(project).OffsetSide;
    string side=DoorOffset.SideOf(shift,locked&&lockedSide!=null?lockedSide:preset);
    if(typing!=DoorSettings.OffsetKey){string text=CarDoorsViewModel.Format(Math.Abs(shift));if(offsetBox.Text!=text)offsetBox.Text=text;}
    sideSet=true;try{offsetSide.SelectedValue=side;}finally{sideSet=false;}
    offsetRange.Text=off.AllowedText??"";var msg=off.Message;
    offsetMessage.Text=msg??"";offsetMessage.Visibility=msg==null?Visibility.Collapsed:Visibility.Visible;
   }
   else{offsetRange.Text="";offsetMessage.Text="";offsetMessage.Visibility=Visibility.Collapsed;}
   Button reset;if(resets.TryGetValue(DoorSettings.OffsetKey,out reset))reset.IsEnabled=locked;
  }
  // Shows the values in effect: unlocked fields follow, the typed one is left alone.
  void Refresh(string typing)
  {
   bool was=programmatic;programmatic=true;
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
    if(offsetBox!=null)RefreshOffset(typing);
    if(inUse!=null)inUse.Text=l==null?"":"In use: "+CarDoorsViewModel.Format(l.OpeningMm)+" mm";
    if(fit!=null){var msg=DoorFlow.FitMessage(l);fit.Text=msg??"";fit.Visibility=msg==null?Visibility.Collapsed:Visibility.Visible;}
    if(derived!=null)derived.Text=DerivedGeometry.Text(Session,Live,Context)??"";
   }
   finally{programmatic=was;}
  }
 }
}
