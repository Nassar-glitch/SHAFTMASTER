using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  Dictionary<string,string> RelevantDraftErrors
  {
   get{
    if(CalculationModes.Resolve(session.Project.Calculations)!="Basic")return calculationDraftErrors;
    var c=session.Project.Calculations;var fields=new[]{"CarMassKg","CounterweightMassKg","MachineMassKg","CarSideAdditionalSuspendedMassKg","CounterweightSideAdditionalSuspendedMassKg"}.Select(f=>"Calculations."+f).ToList();
    if(c.IncludeChassisSelfWeight==true)fields.Add("Calculations.ChassisMassKg");if(c.IncludeHangerAssemblyWeight==true)fields.Add("Calculations.HangerAssemblyMassKg");
    return calculationDraftErrors.Where(pair=>fields.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value);
   }
  }
  void CalculationsPage()
  {
   if(session.Project.Calculations==null)session.Project.Calculations=new ChassisCalculationInputs();
   var c=session.Project.Calculations;if(c.Mode==null){c.Mode=CalculationModes.Resolve(c);session.Dirty=true;}
   RunLiveCalculation();
   calculationChoices.Clear();calculationResults=null;safetyPitResults=null;carGuideForceResults=null;carGuideForceDimensions=null;
   var choices=new List<Choice>{new Choice{Id="Basic",Name="Simplified load distribution"},new Choice{Id="Advanced",Name="Advanced · detailed rope / member actions"}};
   if(!choices.Any(v=>v.Id==c.Mode))choices.Add(new Choice{Id=c.Mode,Name="Unsupported saved mode: "+c.Mode});
   var mode=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.Mode,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)};calculationChoices["Mode"]=mode;System.Windows.Automation.AutomationProperties.SetName(mode,"Calculation mode");
   mode.SelectionChanged+=(s,e)=>{if(mode.SelectedValue==null)return;c.Mode=(string)mode.SelectedValue;session.Dirty=true;design=null;calculations.Invalidate();ShowPage("CALCULATIONS");};content.Children.Add(mode);
   FillBlanksButton("CALCULATIONS");
   if(c.Mode=="Basic")BasicCalculationsPage();else if(c.Mode=="Advanced")AdvancedCalculationsPage();else Note("Unsupported mode. Select a supported workflow explicitly.");
  }
  void BasicCalculationsPage()
  {
   calculations.EnsureGeometry(session.Project,context);
   Note("Q "+N(session.Project.Elevator.CapacityKg)+" kg · 1:1");
   var parent=content;var masses=new StackPanel{Orientation=Orientation.Horizontal};content=masses;
   CalcField("Empty car mass P (kg)","CarMassKg",null,true);CalcField("Counterweight mass (kg)","CounterweightMassKg",null,true);CalcField("Machine mass (kg)","MachineMassKg",null,true);
   content=parent;parent.Children.Add(masses);masses.SizeChanged+=(s,e)=>masses.Orientation=masses.ActualWidth<600?Orientation.Vertical:Orientation.Horizontal;
   var c=session.Project.Calculations;
   CalculationGroup("Optional additional effective masses",()=>{
    CalcField("Effective car-side additions (kg)","CarSideAdditionalSuspendedMassKg");CalcField("Effective CW-side additions (kg)","CounterweightSideAdditionalSuspendedMassKg");
    BasicWeight("Include chassis assembly mass","IncludeChassisSelfWeight");if(c.IncludeChassisSelfWeight==true)CalcField("Chassis assembly mass (kg)","ChassisMassKg");
    BasicWeight("Include hanger assembly mass","IncludeHangerAssemblyWeight");if(c.IncludeHangerAssemblyWeight==true)CalcField("Hanger assembly mass (kg)","HangerAssemblyMassKg");
   },c.CarSideAdditionalSuspendedMassKg.HasValue||c.CounterweightSideAdditionalSuspendedMassKg.HasValue||c.IncludeChassisSelfWeight==true||c.IncludeHangerAssemblyWeight==true);
   calculationResults=new StackPanel{Margin=new Thickness(0,14,0,10)};content.Children.Add(calculationResults);RenderBasicResults();
   RopeTractionSection();
   SafetyPitSection();
   CarGuideForceSection();
   var g=calculations.Geometry;
   if(g!=null){Heading("Drawing geometry");Note("Machine load reference: traction sheave centre "+PointText(g.TractionCentre)+" mm");content.Children.Add(CalculationPreview(g));}
   else {Note("Geometry unavailable: enter SW, SD and capacity.");if(calculations.SourceDesign!=null)foreach(var issue in calculations.SourceDesign.Validation.Where(v=>v.BlocksGeneration))Note(issue.Field+": "+MessageText(issue.Message));}
  }
  void BasicWeight(string label,string fieldName)
  {
   var field=typeof(ChassisCalculationInputs).GetField(fieldName);var c=session.Project.Calculations;
   var box=new CheckBox{Content=label,IsChecked=(bool?)field.GetValue(c)==true,Margin=new Thickness(0,5,0,12)};System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=Managed("Calculations."+fieldName);if(managed!=null)box.ContextMenu=ManagedResetMenu(managed);
   box.Click+=(s,e)=>{field.SetValue(c,(bool?)(box.IsChecked==true));if(managed!=null&&ProjectPresets.Follows(session.Project))ProjectPresets.Lock(session.Project,managed.Key);session.Dirty=true;calculations.Invalidate();ShowPage("CALCULATIONS");};content.Children.Add(box);
  }
  // Basic P1–P4 lever arms as a small table (mm, 0–1 decimals).
  FrameworkElement DistanceTable(SimplifiedDistances d)
  {
   var grid=new Grid{Margin=new Thickness(0,0,0,12),HorizontalAlignment=HorizontalAlignment.Left};
   foreach(var w in new[]{230.0,150,150,110})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(w)});
   Func<double,string> mm=v=>v.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
   var rows=new[]{new[]{"Distances (mm)","Start → load","Load → end","Span"},
    new[]{"Front row → rear row",mm(d.LoadFromFrontMm),mm(d.LoadToRearMm),mm(d.LongitudinalSpanMm)},
    new[]{"Front row P4 → P2",mm(d.FrontLoadFromLeftMm),mm(d.FrontLoadToRightMm),mm(d.FrontTransverseSpanMm)},
    new[]{"Rear row P3 → P1",mm(d.RearLoadFromLeftMm),mm(d.RearLoadToRightMm),mm(d.RearTransverseSpanMm)}};
   for(int i=0;i<rows.Length;i++)
   {
    grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    for(int j=0;j<4;j++)
    {
     var cell=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(214,223,232)),BorderThickness=new Thickness(0,0,0,1),Background=i==0?new SolidColorBrush(Color.FromRgb(232,238,245)):Brushes.White,Padding=new Thickness(8,4,8,4),
      Child=new TextBlock{Text=rows[i][j],FontWeight=i==0?FontWeights.SemiBold:FontWeights.Normal,TextAlignment=j==0?TextAlignment.Left:TextAlignment.Right,Foreground=ink}};
     Grid.SetRow(cell,i);Grid.SetColumn(cell,j);grid.Children.Add(cell);
    }
   }
   return grid;
  }
  static string BasicFieldLabel(string field)
  {
   switch(field){
    case "Calculations.CarMassKg":return "Empty car mass";
    case "Calculations.CounterweightMassKg":return "Counterweight mass";
    case "Calculations.MachineMassKg":return "Machine mass";
    case "Calculations.CarSideAdditionalSuspendedMassKg":return "Effective car-side additions";
    case "Calculations.CounterweightSideAdditionalSuspendedMassKg":return "Effective CW-side additions";
    case "Calculations.ChassisMassKg":return "Chassis assembly mass";
    case "Calculations.HangerAssemblyMassKg":return "Hanger assembly mass";
    case "Elevator.CapacityKg":return "Rated capacity";
    case "Suspension.RopingRatio":return "Suspension ratio";
    case "Machine.TractionSheavePitchDiameterMm":return "Traction-sheave pitch diameter";
    case "Basic.Geometry":case "Geometry":return "Drawing geometry";
    case "Bearings":return "Bearing reactions";
    default:return "Project input";
   }
  }
  void RenderBasicResults()
  {
   if(calculationResults==null)return;calculationResults.Children.Clear();var parent=content;content=calculationResults;
   try{
    var r=calculations.Result;bool current=RelevantDraftErrors.Count==0&&calculations.IsCurrent(session.Project,context);
    // Live results: an invalid entry shows only its message; an outdated result is never shown (it is recalculated at once).
    if(RelevantDraftErrors.Count>0){content.Children.Add(new TextBlock{Text="Basic · Invalid mass input",Foreground=Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});foreach(var issue in RelevantDraftErrors)Note(BasicFieldLabel(issue.Key)+": "+issue.Value);return;}
    if(r==null||(r.Reactions!=null&&!current))return;
    content.Children.Add(new TextBlock{Text="Basic · "+StatusWord(r.Status),Foreground=current&&r.Status!="UPLIFT_REVIEW_REQUIRED"?accent:Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
    if(current){
     Note("Total mass: "+N(r.TotalMassKg)+" kg · Vertical load: "+N(r.TotalVerticalLoadN/1000)+" kN (g = 9.81 m/s²)");
     foreach(var b in r.Reactions.OrderBy(v=>v.Id))Note(b.Id+" · "+BearingLabel(b.Id)+"   "+N(b.UpwardN/1000)+" kN   ["+PointText(b.PositionMm)+" mm]");
     var d=r.SimplifiedGeometry;content.Children.Add(new TextBlock{Text="Derived distances",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,4,0,6)});content.Children.Add(DistanceTable(d));
     CalculationGroup("Load composition & rope tensions",()=>{
      foreach(var mass in r.MassCompositionKg)Note(mass.Key+": "+N(mass.Value)+" kg");
      Note("Rope tensions\nCar bundle: "+N(r.CarBundleTensionN)+" N · CW bundle: "+N(r.CounterweightBundleTensionN)+" N\nPer rope: car "+N(r.CarPerRopeTensionN)+" N · CW "+N(r.CounterweightPerRopeTensionN)+" N");
      Note("Model: "+r.ModelVersion+" · Front row Y "+N(d.FrontRowY)+", rear row Y "+N(d.RearRowY)+" mm\nFront load "+N(d.FrontLoadN)+" N · Rear load "+N(d.RearLoadN)+" N\nEquilibrium residuals: "+N(r.VerticalResidualN)+" N, "+N(r.MomentXResidualNmm)+" / "+N(r.MomentYResidualNmm)+" N.mm");
     },false);
    }
    foreach(var issue in r.Issues)Note(BasicFieldLabel(issue.Field)+": "+MessageText(issue.Message));
   }finally{content=parent;}
  }
 }
}
