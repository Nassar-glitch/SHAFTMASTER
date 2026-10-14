using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Presets;
using Elevator.Core.Steel;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // SHAFT: shaft dimensions, the shaft construction (concrete or steel structure) and, for steel, the profiles, plates and beam levels.
 sealed class ShaftPage:PageView
 {
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  readonly ShaftViewModel vm=new ShaftViewModel();
  public ShaftPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Form.FillBlanksButton(Key);Heading("Shaft dimensions");Form.Fields(ShaftViewModel.Fields);
   Heading("Calculated geometry");var derived=DerivedGeometry.Text(Session,Live,Context);if(derived!=null)Note(derived);
   Form.PresetRefreshers.Clear();Form.PresetChoices.Clear();
   ConstructionEditors();
  }
  PresetContext Ctx(){return Presets.Context(Session.Project);}
  bool IsSteel(){return Presets.Library!=null&&SteelFields.IsSteel(Session.Project,Presets.Library);}
  // Something the drawings and the sheet use changed: the next result is built again.
  void Changed()
  {
   Session.Dirty=true;Shell.Live.Design=null;Shell.Updates.InvalidateRopeTraction();Shell.RefreshProjectStatus();
  }
  ContextMenu ResetMenu(PresetField field,Action after)
  {
   var menu=new ContextMenu();var item=new MenuItem{Header="Reset to preset"};
   item.Click+=(s,e)=>Shell.Guard(()=>{field.Reset(Session.Project);after();Changed();});
   menu.Items.Add(item);return menu;
  }
  // A list of the steel fields: shows the effective value, locks when another entry is chosen, Reset to preset.
  void Choice(PresetField field,IList<KeyValuePair<string,string>> items,Action<string> chosen=null)
  {
   var combo=new ComboBox{ItemsSource=items.Select(i=>new Choice{Id=i.Key,Name=i.Value}).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",MinWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,5,8,5)};
   System.Windows.Automation.AutomationProperties.SetName(combo,field.Label);Form.PresetChoices[field.Key]=combo;
   bool programmatic=false;
   Func<string> value=()=>{var v=field.Effective(Ctx());return v==null?null:Convert.ToString(v,Inv);};
   Action show=()=>{programmatic=true;combo.SelectedValue=value();programmatic=false;};
   combo.SelectionChanged+=(s,e)=>{
    if(programmatic)return;var choice=combo.SelectedItem as Choice;if(choice==null)return;
    field.Edit(Session.Project,field.Key==SteelFields.BoltCount.Key?(object)Double.Parse(choice.Id,Inv):choice.Id);Changed();
    if(chosen!=null)chosen(choice.Id);
   };
   combo.ContextMenu=ResetMenu(field,()=>{show();if(chosen!=null)chosen(value());});
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(combo);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  // A number of the steel fields.
  void Number(PresetField field)
  {
   var box=new TextBox{MinWidth=130};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);Form.Editors[field.Key]=box;
   var hint=new TextBlock{FontSize=11,Foreground=Brushes.DarkRed,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};
   var panel=new StackPanel{MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(box);panel.Children.Add(hint);
   bool programmatic=false;string error=null;
   Action show=()=>{programmatic=true;var v=field.Effective(Ctx());box.Text=v==null?"":Convert.ToDouble(v,Inv).ToString("0.###",Inv);programmatic=false;error=null;hint.Visibility=Visibility.Collapsed;};
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;double v;error=null;
    if(String.IsNullOrWhiteSpace(box.Text))field.Reset(Session.Project);
    else if(!Double.TryParse(box.Text,NumberStyles.Float,Inv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))error="Enter a number.";
    else if(v<=0)error="Enter a number above zero.";
    else field.Edit(Session.Project,v);
    hint.Text=error??"";hint.Visibility=error==null?Visibility.Collapsed:Visibility.Visible;Changed();
   };
   Form.Committers.Add(()=>{if(error!=null)throw new ArgumentException(field.Label+": "+error);});
   box.ContextMenu=ResetMenu(field,show);
   Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  void Text(PresetField field)
  {
   var box=new TextBox{MinWidth=200};System.Windows.Automation.AutomationProperties.SetName(box,field.Label);Form.Editors[field.Key]=box;
   bool programmatic=false;
   Action show=()=>{programmatic=true;box.Text=(field.Effective(Ctx()) as string)??"";programmatic=false;};
   box.TextChanged+=(s,e)=>{if(programmatic)return;if(String.IsNullOrWhiteSpace(box.Text))field.Reset(Session.Project);else field.Edit(Session.Project,box.Text.Trim());Changed();};
   box.ContextMenu=ResetMenu(field,show);Form.PresetRefreshers.Add(()=>{if(!field.IsEdited(Session.Project))show();});
   show();var panel=new StackPanel();panel.Children.Add(box);Content.Children.Add(Form.FormRow(field.Label,panel));
  }
  void ConstructionEditors()
  {
   if(Presets.Library==null)return;
   Heading("Shaft construction");
   var steel=Presets.Library.SteelShaftOrDefault();
   Choice(SteelFields.Construction,new[]{new KeyValuePair<string,string>(SteelShaftPreset.Concrete,"Concrete"),new KeyValuePair<string,string>(SteelShaftPreset.SteelStructure,"Steel structure")},id=>Shell.ShowPage("SHAFT"));
   if(!IsSteel())return;
   Choice(SteelFields.ColumnProfile,steel.Channels.Select(c=>new KeyValuePair<string,string>(c.Name,c.Name)).ToList());
   Choice(SteelFields.BeamProfile,steel.Channels.Select(c=>new KeyValuePair<string,string>(c.Name,c.Name)).ToList());
   Choice(SteelFields.BracingProfile,steel.Angles.Select(a=>new KeyValuePair<string,string>(a.Name,a.Name)).ToList());
   Number(SteelFields.PlateWidth);Number(SteelFields.PlateDepth);Number(SteelFields.PlateThickness);
   Choice(SteelFields.BoltCount,new[]{"4","6","8"}.Select(n=>new KeyValuePair<string,string>(n,n)).ToList());
   Number(SteelFields.BoltDiameter);
   Form.CalculationGroup("Advanced",()=>{
    Number(SteelFields.GussetLength);Number(SteelFields.GussetWidth);Number(SteelFields.GussetThickness);
    Text(SteelFields.SteelGrade);
    Number(SteelFields.BaseMargin);Number(SteelFields.BaseThickness);Number(SteelFields.BarDiameter);Number(SteelFields.BarCountAlongX);Number(SteelFields.BarCountAlongY);
    Number(SteelFields.SlabEdgeDepth);Number(SteelFields.SlabEdgeWidth);Number(SteelFields.HousingHeight);Number(SteelFields.HousingOverhang);
    BeamLevels();
   },false);
  }
  static string FaceTitle(string face)
  {
   return face=="Front"?"Beam levels, entrance face":face=="Back"?"Beam levels, back face":face=="Left"?"Beam levels, left face":"Beam levels, right face";
  }
  // The beam levels of each face, read from the rule (the car rail bracket spacing and the floors) and editable: a typed level locks.
  void BeamLevels()
  {
   var steel=SteelStructure.Build(Ctx());
   if(!steel.Valid)return;
   foreach(string face in SteelFields.Faces)
   {
    string name=face;
    var group=new StackPanel{Margin=new Thickness(0,4,0,10)};
    group.Children.Add(new TextBlock{Text=FaceTitle(face),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,6,0,6)});
    foreach(var level in steel.Levels[face])
    {
     var l=level;
     var box=new TextBox{MinWidth=130,Text=(l.ElevationMm-0).ToString("0.###",Inv),IsReadOnly=l.Kind==SteelLevelKind.Floor};
     System.Windows.Automation.AutomationProperties.SetName(box,FaceTitle(face)+" "+l.Key);
     Form.Editors["Steel.Level."+face+"."+l.Key]=box;
     var panel=new StackPanel{MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(box);
     string label=l.Tag+(l.FloorName!=null?" "+l.FloorName:"")+" (mm)";
     if(l.Edited)label+=", locked";
     bool programmatic=false;
     if(l.Kind!=SteelLevelKind.Floor)
     {
      box.TextChanged+=(s,e)=>{
       if(programmatic)return;double v;
       if(Double.TryParse(box.Text,NumberStyles.Float,Inv,out v)&&!Double.IsNaN(v)&&!Double.IsInfinity(v)){SteelFields.EditLevel(Session.Project,name,l.Key,v);Changed();}
      };
      var menu=new ContextMenu();
      var reset=new MenuItem{Header="Reset to the rule"};
      reset.Click+=(s,e)=>Shell.Guard(()=>{SteelFields.ResetLevel(Session.Project,name,l.Key);Changed();Shell.ShowPage("SHAFT");});
      var remove=new MenuItem{Header="Remove this beam"};
      remove.Click+=(s,e)=>Shell.Guard(()=>{SteelFields.RemoveLevel(Session.Project,name,l.Key);Changed();Shell.ShowPage("SHAFT");});
      menu.Items.Add(reset);menu.Items.Add(remove);box.ContextMenu=menu;
     }
     group.Children.Add(Form.FormRow(label,panel));
    }
    var resetAll=Form.Button("Reset the beam levels of this face",()=>{SteelFields.ResetLevels(Session.Project,name);Changed();Shell.ShowPage("SHAFT");});
    System.Windows.Automation.AutomationProperties.SetName(resetAll,"Reset beam levels "+face);
    resetAll.HorizontalAlignment=HorizontalAlignment.Left;
    group.Children.Add(resetAll);
    Content.Children.Add(group);
   }
  }
 }
}
