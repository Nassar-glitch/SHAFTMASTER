using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Elevator.Core;
using Elevator.Core.Calculations;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  CalculationWorkspace calculations=new CalculationWorkspace();
  readonly Dictionary<string,string> calculationDrafts=new Dictionary<string,string>();
  readonly Dictionary<string,string> calculationDraftErrors=new Dictionary<string,string>();
  StackPanel calculationResults;DateTime lastCurrencyCheck=DateTime.MinValue;
  readonly Dictionary<string,ComboBox> calculationChoices=new Dictionary<string,ComboBox>();
  void ResetCalculations(){calculations=new CalculationWorkspace();calculationDrafts.Clear();calculationDraftErrors.Clear();calculationResults=null;ResetSafetyPit();ResetRopeTraction();ResetCarGuideForces();}
  void InvalidateCalculationDisplay(){calculations.Invalidate();InvalidateSafetyPitDisplay();CheckCarGuideForceCurrency();if(activePage=="CALCULATIONS")RenderCalculationResults();ScheduleLiveCalculation();}
  void CheckCalculationCurrency()
  {
   if(DateTime.UtcNow-lastCurrencyCheck<TimeSpan.FromSeconds(2))return;lastCurrencyCheck=DateTime.UtcNow;
   if(safetyPitResult!=null&&!SafetyPitCalculationEngine.IsCurrent(safetyPitResult,session.Project))InvalidateSafetyPitDisplay();
   CheckCarGuideForceCurrency();
   var r=calculations.Result;
   if(r!=null&&r.Reactions!=null&&!calculations.IsCurrent(session.Project,context))InvalidateCalculationDisplay();
  }
  void AdvancedCalculationsPage()
  {
   if(session.Project.Calculations==null)session.Project.Calculations=new ChassisCalculationInputs();
   calculations.EnsureGeometry(session.Project,context);
   calculationResults=new StackPanel();content.Children.Add(calculationResults);RenderCalculationResults();
   RopeTractionSection();
   SafetyPitSection();
   CarGuideForceSection();
   CalculationGroup("Geometry & reference points",()=>{
    var g=calculations.Geometry;
    if(g==null){Note("Geometry unavailable: enter SW, SD, capacity and floor intervals.");if(calculations.SourceDesign!=null)foreach(var issue in calculations.SourceDesign.Validation.Where(v=>v.BlocksGeneration))Note(issue.Field+": "+MessageText(issue.Message));return;}
    Note(String.Format(CultureInfo.InvariantCulture,"SW {0:0.###} mm · SD {1:0.###} mm · Capacity {2} kg · {3}:1 · Ropes {4}\nX right, +Y entrance → rear, +Z above machine-room slab",g.ShaftWidthMm,g.ShaftDepthMm,session.Project.Elevator.CapacityKg,session.Project.Suspension.RopingRatio,session.Project.Suspension.RopeCount.HasValue?session.Project.Suspension.RopeCount.Value.ToString():"—"));
    content.Children.Add(CalculationPreview(g));
    Note("Traction pitch centre: "+PointText(g.TractionCentre)+" mm\nDeflector pitch centre: "+PointText(g.DeflectorCentre)+" mm\nHanger attachment Z: "+N(g.HangerAttachmentZMm)+" mm · Bearing plane Z: "+N(g.BearingPlaneZMm)+" mm");
    foreach(var b in g.Bearings.OrderBy(b=>b.Id))Note(b.Id+" · "+BearingLabel(b.Id)+" · contact-area centre "+PointText(b.Centre)+" mm");
   },true);
   CalculationGroup("Component & effective additional suspended masses",()=>{
    SelectCalculation("Load case","LoadCase",new[]{new Choice{Id="StaticRatedLoad1To1",Name="Static rated load · ideal 1:1"}});
    CalcField("Car mass (kg)","CarMassKg");CalcField("Counterweight mass (kg)","CounterweightMassKg");CalcField("Machine mass (kg)","MachineMassKg");
    CalcField("Car-side additional mass (kg)","CarSideAdditionalSuspendedMassKg");CalcField("CW-side additional mass (kg)","CounterweightSideAdditionalSuspendedMassKg");
   },true);
   CalculationGroup("Machine centre of gravity",()=>{
    foreach(var axis in new[]{"X","Y","Z"})CalcField("Machine CG Δ"+axis+" (mm)","MachineCgOffset"+axis+"Mm");
   },false);
   CalculationGroup("Carrying members & hanger attachment",()=>{
    var g=calculations.Geometry;var members=g==null?new Choice[0]:g.LongitudinalMembers.Select(m=>new Choice{Id=m.Id,Name=m.Id.Replace("Longitudinal.","")+" · X front "+N(m.Front.X)+", rear "+N(m.Rear.X)+" mm ["+m.Id+"]"}).ToArray();
    SelectCalculation("Left carrying member","LeftCarryingMemberId",members);SelectCalculation("Right carrying member","RightCarryingMemberId",members);
    SelectCalculation("Transfer model","TransferModel",new[]{new Choice{Id=ChassisReactionSolver.Model,Name="Released two-stage beams + ideal rigid spreader"}});
    CalcField("Hanger attachment ΔX (mm)","HangerAttachmentOffsetXMm");CalcField("Hanger attachment ΔY (mm)","HangerAttachmentOffsetYMm");
   },false);
   CalculationGroup("Optional self-weights",()=>{
    WeightChoice("Chassis self-weight","IncludeChassisSelfWeight");
    CalcField("Chassis mass (kg)","ChassisMassKg");foreach(var a in new[]{"X","Y","Z"})CalcField("Chassis CG Δ"+a+" (mm)","ChassisCgOffset"+a+"Mm");
    WeightChoice("Hanger assembly weight","IncludeHangerAssemblyWeight");
    CalcField("Hanger assembly mass (kg)","HangerAssemblyMassKg");foreach(var a in new[]{"X","Y","Z"})CalcField("Hanger CG Δ"+a+" (mm)","HangerCgOffset"+a+"Mm");
   },false);
  }
  void CalculationGroup(string name,Action build,bool expanded)
  {
   var parent=content;var group=new StackPanel{Margin=new Thickness(12,12,12,6)};content=group;try{build();}finally{content=parent;}
   parent.Children.Add(new Expander{Header=name,IsExpanded=expanded,Content=group,Background=Brushes.White,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)});
  }
  void CalcField(string label,string name,string hint=null,bool compact=false)
  {
   string path="Calculations."+name;var field=typeof(ChassisCalculationInputs).GetField(name);var owner=session.Project.Calculations;var v=field.GetValue(owner);
   var panel=new StackPanel();bool numeric=field.FieldType!=typeof(string);if(numeric){panel.MaxWidth=250;panel.HorizontalAlignment=HorizontalAlignment.Left;}if(compact){panel.Width=185;panel.Margin=new Thickness(0,0,12,10);panel.Children.Add(new TextBlock{Text=label,FontSize=13,Margin=new Thickness(0,0,0,5)});}
   string draft;var box=new TextBox{Text=calculationDrafts.TryGetValue(path,out draft)?draft:(v==null?"":Convert.ToString(v,CultureInfo.InvariantCulture)),MinWidth=numeric?130:0};editors[path]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);panel.Children.Add(box);
   var message=new TextBlock{Text=hint??"",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(0,4,0,0)};message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;panel.Children.Add(message);
   var managed=Managed(path);bool programmatic=false;
   if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers[path]=()=>{if(managed.IsEdited(session.Project))return;var current=field.GetValue(owner);programmatic=true;calculationDrafts.Remove(path);calculationDraftErrors.Remove(path);box.Text=current==null?"":Convert.ToString(current,CultureInfo.InvariantCulture);message.Text=hint??"";message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;programmatic=false;};}
   box.TextChanged+=(s,e)=>{
    if(programmatic)return;
    calculationDrafts[path]=box.Text;calculationDraftErrors.Remove(path);object parsed=null;
    if(!String.IsNullOrWhiteSpace(box.Text)){if(!numeric)parsed=box.Text;else{double d;if(Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&!Double.IsNaN(d)&&!Double.IsInfinity(d))parsed=d;else calculationDraftErrors[path]=label+": finite number required (mm or kg as labelled).";}}
    // Invalid text clears the canonical value instead of leaving an old hidden number active.
    field.SetValue(owner,parsed);session.Dirty=true;design=null;message.Text=calculationDraftErrors.ContainsKey(path)?calculationDraftErrors[path]:hint??"";message.Visibility=String.IsNullOrEmpty(message.Text)?Visibility.Collapsed:Visibility.Visible;message.Foreground=calculationDraftErrors.ContainsKey(path)?Brushes.DarkRed:Brushes.SlateGray;
    if(managed!=null&&!calculationDraftErrors.ContainsKey(path))ManagedEdited(managed);
    InvalidateCalculationDisplay();RefreshProjectStatus();
   };content.Children.Add(compact?(FrameworkElement)panel:FormRow(label,panel));
  }
  void SelectCalculation(string label,string name,IEnumerable<Choice> choices)
  {
   var field=typeof(ChassisCalculationInputs).GetField(name);var owner=session.Project.Calculations;string selected=field.GetValue(owner) as string;
   var list=new List<Choice>{new Choice{Id=null,Name="—"}};list.AddRange(choices);
   if(selected!=null&&!list.Any(c=>c.Id==selected))list.Add(new Choice{Id=selected,Name=selected});
   var box=new ComboBox{ItemsSource=list,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=list.FindIndex(c=>c.Id==selected),Padding=new Thickness(8),MinHeight=34};calculationChoices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=Managed("Calculations."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers["Calculations."+name]=()=>{if(managed.IsEdited(session.Project))return;programmatic=true;box.SelectedIndex=list.FindIndex(c=>c.Id==(field.GetValue(owner) as string));programmatic=false;};}
   box.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;field.SetValue(owner,choice==null?null:choice.Id);if(managed!=null)ManagedEdited(managed);session.Dirty=true;design=null;InvalidateCalculationDisplay();RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);content.Children.Add(FormRow(label,panel));
  }
  void WeightChoice(string label,string name)
  {
   var field=typeof(ChassisCalculationInputs).GetField(name);var owner=session.Project.Calculations;var v=(bool?)field.GetValue(owner);
   var choices=new[]{new Choice{Id="missing",Name="—"},new Choice{Id="include",Name="Include"},new Choice{Id="exclude",Name="Exclude"}};
   var box=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=v.HasValue?(v.Value?"include":"exclude"):"missing",Padding=new Thickness(8)};calculationChoices[name]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=Managed("Calculations."+name);bool programmatic=false;
   if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers["Calculations."+name]=()=>{if(managed.IsEdited(session.Project))return;var current=(bool?)field.GetValue(owner);programmatic=true;box.SelectedValue=current.HasValue?(current.Value?"include":"exclude"):"missing";programmatic=false;};}
   box.SelectionChanged+=(s,e)=>{if(programmatic)return;var choice=box.SelectedItem as Choice;string value=choice==null?null:choice.Id;field.SetValue(owner,value=="include"?(bool?)true:value=="exclude"?(bool?)false:null);if(managed!=null)ManagedEdited(managed);session.Dirty=true;design=null;InvalidateCalculationDisplay();RefreshProjectStatus();};var panel=new StackPanel();panel.Children.Add(box);content.Children.Add(FormRow(label,panel));
  }
  void RenderCalculationResults()
  {
   if(calculationResults==null)return;if(CalculationModes.Resolve(session.Project.Calculations)=="Basic"){RenderBasicResults();return;}calculationResults.Children.Clear();var parent=content;content=calculationResults;
   try{
    var r=calculations.Result;bool current=calculationDraftErrors.Count==0&&calculations.IsCurrent(session.Project,context);
    // Live results: an invalid entry shows only its message; an outdated result is never shown (it is recalculated at once).
    if(calculationDraftErrors.Count>0){content.Children.Add(new TextBlock{Text="Advanced · Invalid input",Foreground=Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});foreach(var issue in calculationDraftErrors)Note(issue.Key+": "+issue.Value);return;}
    if(r==null||(r.Reactions!=null&&!current))return;
    content.Children.Add(new TextBlock{Text="Advanced · "+StatusWord(r.Status),Foreground=current&&r.Status!="UPLIFT_REVIEW_REQUIRED"?accent:Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
    if(current){
     Note("Load case: "+session.Project.Calculations.LoadCase+" · Model: "+r.ModelVersion+"\nPositive = upward bearing reaction");
     foreach(var b in r.Reactions.OrderBy(b=>b.Id))Note(b.Id+" · "+BearingLabel(b.Id)+" · "+PointText(b.PositionMm)+" mm   →   "+N(b.UpwardN)+" N ("+N(b.UpwardN/1000)+" kN)");
     Note("Rope tensions · bundle / per rope (N)\nCar: "+N(r.CarBundleTensionN)+" / "+N(r.CarPerRopeTensionN)+"\nCounterweight: "+N(r.CounterweightBundleTensionN)+" / "+N(r.CounterweightPerRopeTensionN));
     Note("Equilibrium residuals: Fz "+N(r.VerticalResidualN)+" N · Mx "+N(r.MomentXResidualNmm)+" N.mm · My "+N(r.MomentYResidualNmm)+" N.mm");
     var detail=new StackPanel();
     foreach(var load in r.Loads)detail.Children.Add(new TextBlock{Text=load.Id+" · "+PointText(load.PositionMm)+" mm",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,5)});
     content.Children.Add(new Expander{Header="Load nodes",Content=detail,Margin=new Thickness(0,0,0,10)});
    }
    foreach(var issue in r.Issues)Note(issue.Field+": "+MessageText(issue.Message));
   }finally{content=parent;}
  }
  static string BearingLabel(string id){return id=="P1"?"right / rear":id=="P2"?"right / front":id=="P3"?"left / rear":"left / front";}
  static string N(double v){return v.ToString("0.###",CultureInfo.InvariantCulture);}static string N(double? v){return v.HasValue?N(v.Value):"UNAVAILABLE";}
  static string PointText(LocalPoint p){return "("+N(p.X)+", "+N(p.Y)+", "+N(p.Z)+")";}
  FrameworkElement CalculationPreview(ChassisCalculationGeometry g)
  {
   var canvas=new Canvas{Width=760,Height=345,Background=new SolidColorBrush(Color.FromRgb(246,249,252))};
   double minX=Math.Min(0,g.Bearings.Min(b=>b.Centre.X)),maxX=Math.Max(g.ShaftWidthMm,g.Bearings.Max(b=>b.Centre.X));double minY=Math.Min(0,g.Bearings.Min(b=>b.Centre.Y)),maxY=Math.Max(g.ShaftDepthMm,g.Bearings.Max(b=>b.Centre.Y));
   double scale=Math.Min(500/(maxX-minX+150),255/(maxY-minY+150));Func<double,double> x=v=>110+(v-minX)*scale;Func<double,double> y=v=>295-(v-minY)*scale;
   Action<double,double,double,double,Brush,double> line=(a,b,c,d,brush,width)=>canvas.Children.Add(new Line{X1=x(a),Y1=y(b),X2=x(c),Y2=y(d),Stroke=brush,StrokeThickness=width});
   Action<double,double,string,Brush> label=(a,b,t,brush)=>{var text=new TextBlock{Text=t,Foreground=brush,FontSize=12};Canvas.SetLeft(text,a);Canvas.SetTop(text,b);canvas.Children.Add(text);};
   line(0,0,g.ShaftWidthMm,0,Brushes.SlateGray,1);line(0,0,0,g.ShaftDepthMm,Brushes.SlateGray,1);line(g.ShaftWidthMm,0,g.ShaftWidthMm,g.ShaftDepthMm,Brushes.SlateGray,1);line(0,g.ShaftDepthMm,g.ShaftWidthMm,g.ShaftDepthMm,Brushes.SlateGray,1);
   var p4=g.Bearings.Single(b=>b.Id=="P4").Centre;var p2=g.Bearings.Single(b=>b.Id=="P2").Centre;var p3=g.Bearings.Single(b=>b.Id=="P3").Centre;var p1=g.Bearings.Single(b=>b.Id=="P1").Centre;
   line(p4.X,p4.Y,p2.X,p2.Y,ink,6);line(p3.X,p3.Y,p1.X,p1.Y,ink,6);
   foreach(var m in g.LongitudinalMembers){line(m.Front.X,m.Front.Y,m.Rear.X,m.Rear.Y,accent,3);label(x(m.Rear.X)-4,y(m.Rear.Y)-18,m.Id.Replace("Longitudinal.","").Substring(0,1),ink);}
   foreach(var b in g.Bearings){var polygon=new Polygon{Stroke=Brushes.DarkOrange,StrokeThickness=2,Fill=Brushes.White,Points=new PointCollection(b.ContactBoundary.Select(v=>new Point(x(v.X),y(v.Y))))};canvas.Children.Add(polygon);label(x(b.Centre.X)+(b.Id=="P1"||b.Id=="P2"?14:-75),y(b.Centre.Y)-7,b.Id+" "+BearingLabel(b.Id).Replace(" / ","/"),ink);}
   Action<LocalPoint,string,Brush> node=(p,text,color)=>{var dot=new Ellipse{Width=9,Height=9,Fill=color};Canvas.SetLeft(dot,x(p.X)-4);Canvas.SetTop(dot,y(p.Y)-4);canvas.Children.Add(dot);label(x(p.X)+8,y(p.Y)-7,text,color);};
   node(g.TractionCentre,CalculationModes.Resolve(session.Project.Calculations)=="Basic"?"Machine load point":"Traction centre",ink);if(CalculationModes.Resolve(session.Project.Calculations)!="Basic")node(g.DeflectorCentre,"Deflector centre",ink);
   if(g.NativeMachineMountingContours!=null&&g.NativeMachineMountingContours.Length>0){var points=g.NativeMachineMountingContours;double a=points.Min(p=>p.X),b=points.Max(p=>p.X),lo=points.Min(p=>p.Y),hi=points.Max(p=>p.Y);var region=new Rectangle{Width=(b-a)*scale,Height=(hi-lo)*scale,Stroke=Brushes.SlateGray,StrokeThickness=1,StrokeDashArray=new DoubleCollection{4,3}};Canvas.SetLeft(region,x(a));Canvas.SetTop(region,y(hi));canvas.Children.Add(region);}
   label(505,30,"PLAN · local mm\n+X → right\n+Y ↑ rear\n+Z above slab\n\nBlue: upper longitudinal\nL = Left · M = Middle · R = Right\nNavy: lower transverse\nOrange: contact perimeters\nDashed: machine contour envelope\n(not confirmed feet / CG)",ink);
   label(110,318,"ENTRANCE · Y = 0    |    SW "+N(g.ShaftWidthMm)+"    SD "+N(g.ShaftDepthMm),ink);
   return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=345,HorizontalAlignment=HorizontalAlignment.Left};
  }
 }
}
