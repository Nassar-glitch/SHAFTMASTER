using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  CarGuideForceGeometry carGuideForceGeometry;
  CarGuideForceResult carGuideForceResult;
  StackPanel carGuideForceResults;
  TextBlock carGuideForceDimensions;
  string carGuideForceDraft,carGuideForceDraftError;
  void ResetCarGuideForces(){carGuideForceGeometry=null;carGuideForceResult=null;carGuideForceResults=null;carGuideForceDimensions=null;carGuideForceDraft=null;carGuideForceDraftError=null;}
  void InvalidateCarGuideForceDisplay(){carGuideForceResult=null;if(activePage=="CALCULATIONS")RenderCarGuideForceResults();ScheduleLiveCalculation();}
  void CheckCarGuideForceCurrency()
  {if(carGuideForceResult!=null&&!CarGuideForceCalculationEngine.IsCurrent(carGuideForceResult,session.Project,context))InvalidateCarGuideForceDisplay();}
  void EnsureCarGuideForceGeometry()
  {if(!CarGuideForceCalculationEngine.IsGeometryCurrent(carGuideForceGeometry,session.Project,context))carGuideForceGeometry=new ElevatorDesignEngine().CalculateCarGuideForceGeometry(session.Project,context);}
  void CarGuideForceSection()
  {
   var c=session.Project.Calculations;if(c.CarGuideForces==null)c.CarGuideForces=new CarGuideForceInputs();
   EnsureCarGuideForceGeometry();
   CalculationGroup("Car guide forces — FF1 / FF2",()=>{
    carGuideForceDimensions=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=ink,Margin=new Thickness(0,0,0,12)};content.Children.Add(carGuideForceDimensions);RefreshCarGuideForceDimensions();
    var owner=c.CarGuideForces;
    var box=new TextBox{Text=carGuideForceDraft??(owner.GuideShoeSpacingMm.HasValue?N(owner.GuideShoeSpacingMm):""),Width=150,HorizontalAlignment=HorizontalAlignment.Left};
    const string label="Guide-shoe spacing h (mm)";const string key="Calculations.CarGuideForces.GuideShoeSpacingMm";
    editors[key]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
    var hint=new TextBlock{Text=carGuideForceDraftError??"",FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkRed,Margin=new Thickness(0,4,0,8),Visibility=carGuideForceDraftError==null?Visibility.Collapsed:Visibility.Visible};
    var managed=Managed(key);bool programmatic=false;
    if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers[key]=()=>{if(managed.IsEdited(session.Project))return;programmatic=true;carGuideForceDraft=null;carGuideForceDraftError=null;box.Text=owner.GuideShoeSpacingMm.HasValue?N(owner.GuideShoeSpacingMm):"";hint.Visibility=Visibility.Collapsed;programmatic=false;};}
    box.TextChanged+=(sender,args)=>{
     if(programmatic)return;
     carGuideForceDraft=box.Text;carGuideForceDraftError=null;double value;double? parsed=null;
     if(!String.IsNullOrWhiteSpace(box.Text)){
      if(Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&ChassisReactionSolver.Finite(value))parsed=value;
      else carGuideForceDraftError=label+": invalid text; enter a finite positive number. Malformed text is not a missing value.";
     }
     owner.GuideShoeSpacingMm=parsed;session.Dirty=true;hint.Text=carGuideForceDraftError??"";hint.Visibility=carGuideForceDraftError==null?Visibility.Collapsed:Visibility.Visible;
     if(managed!=null&&carGuideForceDraftError==null)ManagedEdited(managed);
     InvalidateCarGuideForceDisplay();RefreshProjectStatus();
    };
    var panel=new StackPanel();panel.Children.Add(box);panel.Children.Add(hint);content.Children.Add(FormRow(label,panel));
    carGuideForceResults=new StackPanel{Margin=new Thickness(0,12,0,0)};content.Children.Add(carGuideForceResults);RenderCarGuideForceResults();
   },true);
  }
  void ApplyCarGuideForceDraftValidation()
  {
   if(carGuideForceResult==null||carGuideForceDraftError==null)return;
   var r=carGuideForceResult;r.Status="INVALID_INPUT";r.FF1BaseN=null;r.FF1DesignN=null;r.FF2BaseN=null;r.FF2DesignN=null;r.Details.Clear();
   const string field="Calculations.CarGuideForces.GuideShoeSpacingMm";r.Issues.RemoveAll(issue=>issue.Field==field);r.Issues.Add(new CalculationIssue(field,carGuideForceDraftError));
  }
  void RefreshCarGuideForceDimensions()
  {
   if(carGuideForceDimensions==null)return;
   bool current=CarGuideForceCalculationEngine.IsGeometryCurrent(carGuideForceGeometry,session.Project,context);
   carGuideForceDimensions.Text="Q "+N(session.Project.Elevator.CapacityKg)+" kg · Internal cabin width "+N(current?carGuideForceGeometry.InternalWidthMm:null)+" mm · Internal cabin depth "+N(current?carGuideForceGeometry.InternalDepthMm:null)+" mm";
  }
  void RenderCarGuideForceResults()
  {
   if(carGuideForceResults==null)return;RefreshCarGuideForceDimensions();carGuideForceResults.Children.Clear();ApplyCarGuideForceDraftValidation();var parent=content;content=carGuideForceResults;
   try{
    var r=carGuideForceResult;bool current=CarGuideForceCalculationEngine.IsCurrent(r,session.Project,context);
    if(r==null||!current){
     if(carGuideForceDraftError!=null)Note(carGuideForceDraftError);
     if(r!=null)foreach(var issue in r.Issues)Note(issue.Field+": "+MessageText(issue.Message));
     else if(carGuideForceGeometry!=null&&!CarGuideForceCalculationEngine.IsGeometryCurrent(carGuideForceGeometry,session.Project,context))foreach(var issue in carGuideForceGeometry.Issues)Note(issue.Field+": "+MessageText(issue.Message));
     return;
    }
    if(r.FF1BaseN.HasValue&&r.FF2BaseN.HasValue){
     content.Children.Add(new TextBlock{Text="Calculated",Foreground=accent,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
     Note("FF1 · Base: "+N(r.FF1BaseN)+" N ("+N(r.FF1BaseN/1000)+" kN) · Design: "+N(r.FF1DesignN)+" N ("+N(r.FF1DesignN/1000)+" kN)\nFF2 · Base: "+N(r.FF2BaseN)+" N ("+N(r.FF2BaseN/1000)+" kN) · Design: "+N(r.FF2DesignN)+" N ("+N(r.FF2DesignN/1000)+" kN)");
    }else {Note("FF1 / FF2 · "+StatusWord(r.Status)+" · forces unavailable");foreach(var issue in r.Issues)Note(MessageText(issue.Message));}
    CalculationGroup("Equations & definitions",()=>{
     Note("FF1 base = Q × 10 N/kg × internal width / (4h)\nFF2 base = Q × 10 N/kg × internal depth / (8h)\nEach design force = its base × 2, once. Length units cancel when consistent. The coefficient 10 N/kg remains separate from g = 9.81 m/s².");
     Note(CarGuideForceCalculationEngine.Definitions);foreach(var detail in r.Details.Where(d=>d.StartsWith("FF1 base")||d.StartsWith("FF2 base")))Note(detail);
     Note("Rule version: "+r.RuleVersion);
    },false);
   }finally{content=parent;}
  }
 }
}
