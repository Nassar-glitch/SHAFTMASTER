using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Advanced mode: detailed rope / member actions P1–P4 (geometry and reference points, masses, CG, carrying members,
 // optional self-weights, results).
 sealed class AdvancedPanel:ViewPart
 {
  readonly CalculationsPage page;
  public AdvancedPanel(MainWindow shell,CalculationsPage page):base(shell){this.page=page;}
  public void Build()
  {
   var inputs=page.Inputs;
   if(Session.Project.Calculations==null)Session.Project.Calculations=new ChassisCalculationInputs();
   Live.Chassis.EnsureGeometry(Session.Project,Context);
   page.AddResultsAndSections(null);
   Form.CalculationGroup("Geometry & reference points",()=>{
    var g=Live.Chassis.Geometry;
    if(g==null){Note("Geometry unavailable: enter SW, SD, capacity and floor intervals.");if(Live.Chassis.SourceDesign!=null)foreach(var issue in Live.Chassis.SourceDesign.Validation.Where(v=>v.BlocksGeneration))Note(issue.Field+": "+DisplayText.MessageText(issue.Message));return;}
    Note(String.Format(CultureInfo.InvariantCulture,"SW {0:0.###} mm · SD {1:0.###} mm · Capacity {2} kg · {3}:1 · Ropes {4}\nX right, +Y entrance → rear, +Z above machine-room slab",g.ShaftWidthMm,g.ShaftDepthMm,Session.Project.Elevator.CapacityKg,Session.Project.Suspension.RopingRatio,Session.Project.Suspension.RopeCount.HasValue?Session.Project.Suspension.RopeCount.Value.ToString():"—"));
    Content.Children.Add(CalculationPreview.Create(g,false,Ink,Accent));
    Note("Traction pitch centre: "+DisplayText.PointText(g.TractionCentre)+" mm\nDeflector pitch centre: "+DisplayText.PointText(g.DeflectorCentre)+" mm\nHanger attachment Z: "+DisplayText.N(g.HangerAttachmentZMm)+" mm · Bearing plane Z: "+DisplayText.N(g.BearingPlaneZMm)+" mm");
    foreach(var b in g.Bearings.OrderBy(b=>b.Id))Note(b.Id+" · "+DisplayText.BearingLabel(b.Id)+" · contact-area centre "+DisplayText.PointText(b.Centre)+" mm");
   },true);
   Form.CalculationGroup("Component & effective additional suspended masses",()=>{
    inputs.SelectCalculation("Load case","LoadCase",new[]{new Choice{Id="StaticRatedLoad1To1",Name="Static rated load · ideal 1:1"}});
    inputs.CalcField("Car mass (kg)","CarMassKg");inputs.CalcField("Counterweight mass (kg)","CounterweightMassKg");inputs.CalcField("Machine mass (kg)","MachineMassKg");
    inputs.CalcField("Car-side additional mass (kg)","CarSideAdditionalSuspendedMassKg");inputs.CalcField("CW-side additional mass (kg)","CounterweightSideAdditionalSuspendedMassKg");
   },true);
   Form.CalculationGroup("Machine centre of gravity",()=>{
    foreach(var axis in new[]{"X","Y","Z"})inputs.CalcField("Machine CG Δ"+axis+" (mm)","MachineCgOffset"+axis+"Mm");
   },false);
   Form.CalculationGroup("Carrying members & hanger attachment",()=>{
    var g=Live.Chassis.Geometry;var members=g==null?new Choice[0]:g.LongitudinalMembers.Select(m=>new Choice{Id=m.Id,Name=m.Id.Replace("Longitudinal.","")+" · X front "+DisplayText.N(m.Front.X)+", rear "+DisplayText.N(m.Rear.X)+" mm ["+m.Id+"]"}).ToArray();
    inputs.SelectCalculation("Left carrying member","LeftCarryingMemberId",members);inputs.SelectCalculation("Right carrying member","RightCarryingMemberId",members);
    inputs.SelectCalculation("Transfer model","TransferModel",new[]{new Choice{Id=ChassisReactionSolver.Model,Name="Released two-stage beams + ideal rigid spreader"}});
    inputs.CalcField("Hanger attachment ΔX (mm)","HangerAttachmentOffsetXMm");inputs.CalcField("Hanger attachment ΔY (mm)","HangerAttachmentOffsetYMm");
   },false);
   Form.CalculationGroup("Optional self-weights",()=>{
    inputs.WeightChoice("Chassis self-weight","IncludeChassisSelfWeight");
    inputs.CalcField("Chassis mass (kg)","ChassisMassKg");foreach(var a in new[]{"X","Y","Z"})inputs.CalcField("Chassis CG Δ"+a+" (mm)","ChassisCgOffset"+a+"Mm");
    inputs.WeightChoice("Hanger assembly weight","IncludeHangerAssemblyWeight");
    inputs.CalcField("Hanger assembly mass (kg)","HangerAssemblyMassKg");foreach(var a in new[]{"X","Y","Z"})inputs.CalcField("Hanger CG Δ"+a+" (mm)","HangerCgOffset"+a+"Mm");
   },false);
  }
  public void Render()
  {
   var results=page.Results;
   if(results==null)return;results.Children.Clear();var parent=Content;Content=results;
   try{
    var errors=Live.ChassisDraftErrors;
    var r=Live.Chassis.Result;bool current=errors.Count==0&&Live.Chassis.IsCurrent(Session.Project,Context);
    // Live results: an invalid entry shows only its message; an outdated result is never shown (it is recalculated at once).
    if(errors.Count>0){Content.Children.Add(new TextBlock{Text="Advanced · Invalid input",Foreground=Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});foreach(var issue in errors)Note(issue.Key+": "+issue.Value);return;}
    if(r==null||(r.Reactions!=null&&!current))return;
    Content.Children.Add(new TextBlock{Text="Advanced · "+DisplayText.StatusWord(r.Status),Foreground=current&&r.Status!="UPLIFT_REVIEW_REQUIRED"?Accent:Brushes.DarkRed,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
    if(current){
     Note("Load case: "+Session.Project.Calculations.LoadCase+" · Model: "+r.ModelVersion+"\nPositive = upward bearing reaction");
     foreach(var b in r.Reactions.OrderBy(b=>b.Id))Note(b.Id+" · "+DisplayText.BearingLabel(b.Id)+" · "+DisplayText.PointText(b.PositionMm)+" mm   →   "+DisplayText.N(b.UpwardN)+" N ("+DisplayText.N(b.UpwardN/1000)+" kN; "+DisplayText.Kg(b.UpwardN)+" kg)");
     Note("Rope tensions · bundle / per rope (N)\nCar: "+DisplayText.N(r.CarBundleTensionN)+" / "+DisplayText.N(r.CarPerRopeTensionN)+"\nCounterweight: "+DisplayText.N(r.CounterweightBundleTensionN)+" / "+DisplayText.N(r.CounterweightPerRopeTensionN));
     Note("Equilibrium residuals: Fz "+DisplayText.N(r.VerticalResidualN)+" N · Mx "+DisplayText.N(r.MomentXResidualNmm)+" N.mm · My "+DisplayText.N(r.MomentYResidualNmm)+" N.mm");
     var detail=new StackPanel();
     foreach(var load in r.Loads)detail.Children.Add(new TextBlock{Text=load.Id+" · "+DisplayText.PointText(load.PositionMm)+" mm",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,5)});
     Content.Children.Add(new Expander{Header="Load nodes",Content=detail,Margin=new Thickness(0,0,0,10)});
    }
    foreach(var issue in r.Issues)Note(issue.Field+": "+DisplayText.MessageText(issue.Message));
   }finally{Content=parent;}
  }
 }
}
