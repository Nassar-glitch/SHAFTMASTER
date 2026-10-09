using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop.Common
{
 // The form of the visible page: the panel rows go into, the editors and their commit / refresh actions, and the
 // shared form builders (moved from MainWindow). ShowPage clears it before a page is built.
 sealed class PageForm
 {
  readonly MainWindow shell;
  public PageForm(MainWindow shell){this.shell=shell;}
  // The panel new rows go into (a group swaps it while it builds).
  public StackPanel Content;
  public readonly Dictionary<string,TextBox> Editors=new Dictionary<string,TextBox>();
  public readonly List<Action> Committers=new List<Action>();
  public readonly List<Grid> FormRows=new List<Grid>();
  // Preset-managed editors: refresh while unlocked (DESIGN-FLOW stage 2).
  public readonly Dictionary<string,Action> ManagedRefreshers=new Dictionary<string,Action>();
  // Rope / traction preset editors of the Suspension page.
  public readonly List<Action> PresetRefreshers=new List<Action>();
  public readonly Dictionary<string,ComboBox> PresetChoices=new Dictionary<string,ComboBox>();
  // The floor grid of the Floors page (committed and checked by Commit).
  public DataGrid FloorGrid;
  // Persons next to Q on the General page.
  public TextBlock PersonsLabel;
  public void Clear(){Content.Children.Clear();Editors.Clear();Committers.Clear();FormRows.Clear();FloorGrid=null;PresetRefreshers.Clear();PresetChoices.Clear();ManagedRefreshers.Clear();PersonsLabel=null;}
  ProjectSession Session{get{return shell.Session;}}

  public Button Button(string text,Action action,bool primary=false)
  {
   var b=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,8,0),Background=primary?shell.Accent:Brushes.White,Foreground=primary?Brushes.White:shell.Ink,BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromRgb(193,206,216)),Cursor=System.Windows.Input.Cursors.Hand};
   b.Click+=(s,e)=>shell.Guard(action);return b;
  }
  public void Note(string text){Content.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,15),Foreground=Brushes.SlateGray});}
  public void Heading(string text){Content.Children.Add(new TextBlock{Text=text,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,15,0,12)});}
  public Grid FormRow(string label,StackPanel panel)
  {
   var row=new Grid{Margin=new Thickness(0,0,0,15)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(215)});row.ColumnDefinitions.Add(new ColumnDefinition());row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   row.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,7,18,0),FontSize=13});row.Children.Add(panel);Grid.SetColumn(panel,1);FormRows.Add(row);row.Loaded+=(s,e)=>shell.UpdateResponsiveLayout();return row;
  }
  // A collapsible group; its rows go into the group while build runs.
  public void CalculationGroup(string name,Action build,bool expanded)
  {
   var parent=Content;var group=new StackPanel{Margin=new Thickness(12,12,12,6)};Content=group;try{build();}finally{Content=parent;}
   parent.Children.Add(new Expander{Header=name,IsExpanded=expanded,Content=group,Background=Brushes.White,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)});
  }
  // One action per page, shown while the page has blank preset fields.
  public void FillBlanksButton(string page)
  {
   if(!shell.Presets.CanFillBlanks(Session.Project,page))return;
   var b=Button("Fill blanks from presets",()=>shell.PresetEditing.FillBlanks(page));b.HorizontalAlignment=HorizontalAlignment.Left;b.Margin=new Thickness(0,0,0,14);
   System.Windows.Automation.AutomationProperties.SetName(b,"Fill blanks from presets ("+page+")");Content.Children.Add(b);
  }
  object Value(string path,out object owner,out FieldInfo field)
  {
   var parts=path.Split('.');owner=Session.Project;for(int i=0;i<parts.Length-1;i++)owner=owner.GetType().GetField(parts[i]).GetValue(owner);field=owner.GetType().GetField(parts.Last());return field.GetValue(owner);
  }
  // A project field editor (path into ElevatorProject). Preset-managed fields lock when typed in and follow while unlocked.
  public void Field(string label,string path,string hint=null,bool readOnly=false)
  {
   object owner;FieldInfo field;object value=Value(path,out owner,out field);
   var panel=new StackPanel();var row=FormRow(label,panel);bool numeric=(Nullable.GetUnderlyingType(field.FieldType)??field.FieldType)!=typeof(string);if(numeric){panel.MaxWidth=250;panel.HorizontalAlignment=HorizontalAlignment.Left;}
   if(readOnly){panel.Children.Add(new TextBlock{Text=DisplayText.DisplayValue(path,value),Padding=new Thickness(10,7,10,7),Background=new SolidColorBrush(Color.FromRgb(228,235,241))});}
   else
   {
    Func<object,string> text=v=>{string t=v==null?"":Convert.ToString(v,CultureInfo.InvariantCulture);return (path=="Shaft.WidthMm"||path=="Shaft.DepthMm")&&t=="0"?"":t;};
    var box=new TextBox{Text=text(value),MinWidth=numeric?130:0};System.Windows.Automation.AutomationProperties.SetName(box,label);Editors[path]=box;panel.Children.Add(box);
    var managed=PresetFlow.Managed(path);bool programmatic=false;var effect=LiveCalculation.EffectOf(path);
    box.TextChanged+=(s,e)=>{if(programmatic)return;Session.Dirty=true;if(effect==InputEffect.None){shell.RefreshProjectStatus();return;} /* sheet metadata: no calculation or drawing input */
     if((effect&InputEffect.Design)!=0)shell.Live.Design=null;if((effect&InputEffect.CarGuideForces)!=0)shell.Updates.InvalidateCarGuideForces();if((effect&InputEffect.Chassis)!=0)shell.Updates.InvalidateCalculation();if((effect&InputEffect.RopeTraction)!=0)shell.Updates.InvalidateRopeTraction();shell.RefreshProjectStatus();
     if(managed==null)return;
     // A managed field takes a parsable value at once so that the fields following it update while unlocked.
     Type t=Nullable.GetUnderlyingType(field.FieldType)??field.FieldType;double d;int n;
     if(String.IsNullOrWhiteSpace(box.Text)){if(Nullable.GetUnderlyingType(field.FieldType)==null)return;field.SetValue(owner,null);}
     else if(t==typeof(double)&&Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&!Double.IsNaN(d)&&!Double.IsInfinity(d))field.SetValue(owner,d);
     else if(t==typeof(int)&&Int32.TryParse(box.Text,out n))field.SetValue(owner,n);
     else return;
     shell.PresetEditing.Edited(managed);};
    if(managed!=null){box.ContextMenu=shell.PresetEditing.ResetMenu(managed);ManagedRefreshers[path]=()=>{if(managed.IsEdited(Session.Project))return;programmatic=true;box.Text=text(field.GetValue(owner));programmatic=false;};}
    Committers.Add(()=>{object parsed;Type type=Nullable.GetUnderlyingType(field.FieldType)??field.FieldType;
     if(String.IsNullOrWhiteSpace(box.Text)){if(type==typeof(string)||Nullable.GetUnderlyingType(field.FieldType)!=null)parsed=null;else throw new ArgumentException(label+" is required.");}
     else if(type==typeof(string))parsed=box.Text;
     else if(type==typeof(double)){double v;if(!Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||Double.IsNaN(v)||Double.IsInfinity(v))throw new ArgumentException(label+" must be a finite number.");parsed=v;}
     else if(type==typeof(int)){int v;if(!Int32.TryParse(box.Text,out v))throw new ArgumentException(label+" must be an integer.");parsed=v;}
     else throw new ArgumentException("Unsupported editable field "+path);
     field.SetValue(owner,parsed);
    });
   }
   if(hint!=null)panel.Children.Add(new TextBlock{Text=hint,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap});Content.Children.Add(row);
  }
  // A labelled list of project fields (FieldSpec), in order.
  public void Fields(IEnumerable<FieldSpec> fields){foreach(var f in fields)Field(f.Label,f.Path,f.Hint,f.ReadOnly);}
 }
 // A project field shown on a page: label, path into ElevatorProject, optional hint, read-only display.
 sealed class FieldSpec
 {
  public FieldSpec(string label,string path,string hint=null,bool readOnly=false){Label=label;Path=path;Hint=hint;ReadOnly=readOnly;}
  public string Label {get;private set;}
  public string Path {get;private set;}
  public string Hint {get;private set;}
  public bool ReadOnly {get;private set;}
 }
}
