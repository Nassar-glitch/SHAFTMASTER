using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Basic mode: simplified load distribution P1–P4 (masses, optional additions, results, drawing geometry).
 sealed class BasicPanel:ViewPart
 {
  readonly CalculationsPage page;
  public BasicPanel(MainWindow shell,CalculationsPage page):base(shell){this.page=page;}
  public void Build()
  {
   var inputs=page.Inputs;
   Live.Chassis.EnsureGeometry(Session.Project,Context);
   Note("Q "+DisplayText.N(Session.Project.Elevator.CapacityKg)+" kg · 1:1");
   var parent=Content;var masses=new StackPanel{Orientation=Orientation.Horizontal};Content=masses;
   inputs.CalcField("Empty car mass P (kg)","CarMassKg",null,true);inputs.CalcField("Counterweight mass (kg)","CounterweightMassKg",null,true);inputs.CalcField("Machine mass (kg)","MachineMassKg",null,true);
   Content=parent;parent.Children.Add(masses);masses.SizeChanged+=(s,e)=>masses.Orientation=masses.ActualWidth<600?Orientation.Vertical:Orientation.Horizontal;
   var c=Session.Project.Calculations;
   Form.CalculationGroup("Optional additional effective masses",()=>{
    inputs.CalcField("Effective car-side additions (kg)","CarSideAdditionalSuspendedMassKg");inputs.CalcField("Effective CW-side additions (kg)","CounterweightSideAdditionalSuspendedMassKg");
    Weight("Include chassis assembly mass","IncludeChassisSelfWeight");if(c.IncludeChassisSelfWeight==true)inputs.CalcField("Chassis assembly mass (kg)","ChassisMassKg");
    Weight("Include hanger assembly mass","IncludeHangerAssemblyWeight");if(c.IncludeHangerAssemblyWeight==true)inputs.CalcField("Hanger assembly mass (kg)","HangerAssemblyMassKg");
   },c.CarSideAdditionalSuspendedMassKg.HasValue||c.CounterweightSideAdditionalSuspendedMassKg.HasValue||c.IncludeChassisSelfWeight==true||c.IncludeHangerAssemblyWeight==true);
   page.AddResultsAndSections(new Thickness(0,14,0,10));
   var g=Live.Chassis.Geometry;
   if(g!=null){Heading("Drawing geometry");Note("Machine load reference: traction sheave centre "+DisplayText.PointText(g.TractionCentre)+" mm");Content.Children.Add(CalculationPreview.Create(g,true,Ink,Accent));}
   else {Note("Geometry unavailable: enter SW, SD and capacity.");if(Live.Chassis.SourceDesign!=null)foreach(var issue in Live.Chassis.SourceDesign.Validation.Where(v=>v.BlocksGeneration))Note(issue.Field+": "+DisplayText.MessageText(issue.Message));}
  }
  void Weight(string label,string fieldName)
  {
   var field=typeof(ChassisCalculationInputs).GetField(fieldName);var c=Session.Project.Calculations;
   var box=new CheckBox{Content=label,IsChecked=(bool?)field.GetValue(c)==true,Margin=new Thickness(0,5,0,12)};System.Windows.Automation.AutomationProperties.SetName(box,label);
   var managed=PresetFlow.Managed("Calculations."+fieldName);if(managed!=null)box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);
   box.Click+=(s,e)=>{field.SetValue(c,(bool?)(box.IsChecked==true));if(managed!=null)Presets.Edited(Session,managed);Session.Dirty=true;Live.Chassis.Invalidate();Shell.ShowPage(page.Key);};Content.Children.Add(box);
  }
  // Basic P1–P4 lever arms as a small table (mm, 0–1 decimals).
  FrameworkElement DistanceTable(SimplifiedDistances d)
  {
   var grid=new Grid{Margin=new Thickness(0,0,0,12),HorizontalAlignment=HorizontalAlignment.Left};
   foreach(var w in new[]{230.0,150,150,110})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(w)});
   var rows=CalculationsViewModel.DistanceRows(d);
   for(int i=0;i<rows.Length;i++)
   {
    grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    for(int j=0;j<4;j++)
    {
     var cell=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(214,223,232)),BorderThickness=new Thickness(0,0,0,1),Background=i==0?new SolidColorBrush(Color.FromRgb(232,238,245)):Brushes.White,Padding=new Thickness(8,4,8,4),
      Child=new TextBlock{Text=rows[i][j],FontWeight=i==0?FontWeights.SemiBold:FontWeights.Normal,TextAlignment=j==0?TextAlignment.Left:TextAlignment.Right,Foreground=Ink}};
     Grid.SetRow(cell,i);Grid.SetColumn(cell,j);grid.Children.Add(cell);
    }
   }
   return grid;
  }
  public void Render()
  {
   var results=page.Results;
   if(results==null)return;results.Children.Clear();var parent=Content;Content=results;
   try{
    var draftErrors=Live.RelevantDraftErrors(Session.Project);
    var r=Live.Chassis.Result;bool current=draftErrors.Count==0&&Live.Chassis.IsCurrent(Session.Project,Context);
    // Live results: an invalid entry shows only its message; an outdated result is never shown (it is recalculated at once).
    if(draftErrors.Count>0){Content.Children.Add(new TextBlock{Text="Basic · Invalid mass input",Foreground=Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});foreach(var issue in draftErrors)Note(CalculationsViewModel.BasicFieldLabel(issue.Key)+": "+issue.Value);return;}
    if(r==null||(r.Reactions!=null&&!current))return;
    Content.Children.Add(new TextBlock{Text="Basic · "+DisplayText.StatusWord(r.Status),Foreground=current&&r.Status!="UPLIFT_REVIEW_REQUIRED"?Accent:Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
    if(current){
     Note("Total mass: "+DisplayText.N(r.TotalMassKg)+" kg · Vertical load: "+DisplayText.N(r.TotalVerticalLoadN/1000)+" kN ("+DisplayText.Kg(r.TotalVerticalLoadN)+" kg) (g = 9.81 m/s²)");
     foreach(var b in r.Reactions.OrderBy(v=>v.Id))Note(b.Id+" · "+DisplayText.BearingLabel(b.Id)+"   "+DisplayText.N(b.UpwardN/1000)+" kN ("+DisplayText.Kg(b.UpwardN)+" kg)   ["+DisplayText.PointText(b.PositionMm)+" mm]");
     var d=r.SimplifiedGeometry;Content.Children.Add(new TextBlock{Text="Derived distances",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,4,0,6)});Content.Children.Add(DistanceTable(d));
     Form.CalculationGroup("Load composition & rope tensions",()=>{
      foreach(var mass in r.MassCompositionKg)Note(mass.Key+": "+DisplayText.N(mass.Value)+" kg");
      Note("Rope tensions\nCar bundle: "+DisplayText.N(r.CarBundleTensionN)+" N · CW bundle: "+DisplayText.N(r.CounterweightBundleTensionN)+" N\nPer rope: car "+DisplayText.N(r.CarPerRopeTensionN)+" N · CW "+DisplayText.N(r.CounterweightPerRopeTensionN)+" N");
      Note("Model: "+r.ModelVersion+" · Front row Y "+DisplayText.N(d.FrontRowY)+", rear row Y "+DisplayText.N(d.RearRowY)+" mm\nFront load "+DisplayText.N(d.FrontLoadN)+" N · Rear load "+DisplayText.N(d.RearLoadN)+" N\nEquilibrium residuals: "+DisplayText.N(r.VerticalResidualN)+" N, "+DisplayText.N(r.MomentXResidualNmm)+" / "+DisplayText.N(r.MomentYResidualNmm)+" N.mm");
     },false);
    }
    foreach(var issue in r.Issues)Note(CalculationsViewModel.BasicFieldLabel(issue.Field)+": "+DisplayText.MessageText(issue.Message));
   }finally{Content=parent;}
  }
 }
}
