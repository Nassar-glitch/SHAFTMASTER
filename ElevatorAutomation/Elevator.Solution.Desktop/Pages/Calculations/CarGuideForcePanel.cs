using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.GuideRails;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Car guide forces FF1 / FF2: guide-shoe spacing h and the live result.
 sealed class CarGuideForcePanel:ViewPart
 {
  public CarGuideForcePanel(MainWindow shell):base(shell){}
  public StackPanel Results;
  public TextBlock Dimensions;
  public void ResetView(){Results=null;Dimensions=null;}
  public void Section()
  {
   var c=Session.Project.Calculations;if(c.CarGuideForces==null)c.CarGuideForces=new CarGuideForceInputs();
   Live.EnsureCarGuideForceGeometry(Session.Project);
   Form.CalculationGroup("Car guide forces — FF1 / FF2",()=>{
    Dimensions=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Ink,Margin=new Thickness(0,0,0,12)};Content.Children.Add(Dimensions);RefreshDimensions();
    var owner=c.CarGuideForces;
    var box=new TextBox{Text=Live.CarGuideForceDraft??(owner.GuideShoeSpacingMm.HasValue?DisplayText.N(owner.GuideShoeSpacingMm):""),Width=150,HorizontalAlignment=HorizontalAlignment.Left};
    const string label="Guide-shoe spacing h (mm)";const string key=LiveCalculation.GuideShoeSpacingKey;
    Form.Editors[key]=box;System.Windows.Automation.AutomationProperties.SetName(box,label);
    var hint=new TextBlock{Text=Live.CarGuideForceDraftError??"",FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkRed,Margin=new Thickness(0,4,0,8),Visibility=Live.CarGuideForceDraftError==null?Visibility.Collapsed:Visibility.Visible};
    var managed=PresetFlow.Managed(key);bool programmatic=false;
    if(managed!=null){box.ContextMenu=Shell.PresetEditing.ResetMenu(managed);Form.ManagedRefreshers[key]=()=>{if(managed.IsEdited(Session.Project))return;programmatic=true;Live.CarGuideForceDraft=null;Live.CarGuideForceDraftError=null;box.Text=owner.GuideShoeSpacingMm.HasValue?DisplayText.N(owner.GuideShoeSpacingMm):"";hint.Visibility=Visibility.Collapsed;programmatic=false;};}
    box.TextChanged+=(sender,args)=>{
     if(programmatic)return;
     var parsed=Live.EnterGuideShoeSpacing(label,box.Text);
     owner.GuideShoeSpacingMm=parsed;Session.Dirty=true;hint.Text=Live.CarGuideForceDraftError??"";hint.Visibility=Live.CarGuideForceDraftError==null?Visibility.Collapsed:Visibility.Visible;
     if(managed!=null&&Live.CarGuideForceDraftError==null)Shell.PresetEditing.Edited(managed);
     Shell.Updates.InvalidateCarGuideForces();Shell.RefreshProjectStatus();
    };
    var panel=new StackPanel();panel.Children.Add(box);panel.Children.Add(hint);Content.Children.Add(Form.FormRow(label,panel));
    Results=new StackPanel{Margin=new Thickness(0,12,0,0)};Content.Children.Add(Results);Render();
   },true);
  }
  void RefreshDimensions()
  {
   if(Dimensions==null)return;
   bool current=Live.CarGuideForceGeometryCurrent(Session.Project);var geometry=Live.CarGuideForceGeometry;
   Dimensions.Text="Q "+DisplayText.N(Session.Project.Elevator.CapacityKg)+" kg · Internal cabin width "+DisplayText.N(current?geometry.InternalWidthMm:null)+" mm · Internal cabin depth "+DisplayText.N(current?geometry.InternalDepthMm:null)+" mm";
  }
  public void Render()
  {
   if(Results==null)return;RefreshDimensions();Results.Children.Clear();Live.ApplyCarGuideForceDraftValidation();var parent=Content;Content=Results;
   try{
    var r=Live.CarGuideForces;bool current=CarGuideForceCalculationEngine.IsCurrent(r,Session.Project,Context);
    if(r==null||!current){
     if(Live.CarGuideForceDraftError!=null)Note(Live.CarGuideForceDraftError);
     if(r!=null)foreach(var issue in r.Issues)Note(issue.Field+": "+DisplayText.MessageText(issue.Message));
     else if(Live.CarGuideForceGeometry!=null&&!Live.CarGuideForceGeometryCurrent(Session.Project))foreach(var issue in Live.CarGuideForceGeometry.Issues)Note(issue.Field+": "+DisplayText.MessageText(issue.Message));
     return;
    }
    if(r.FF1BaseN.HasValue&&r.FF2BaseN.HasValue){
     Content.Children.Add(new TextBlock{Text="Calculated",Foreground=Accent,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
     Note("FF1 · Base: "+DisplayText.N(r.FF1BaseN)+" N ("+DisplayText.N(r.FF1BaseN/1000)+" kN) · Design: "+DisplayText.N(r.FF1DesignN)+" N ("+DisplayText.N(r.FF1DesignN/1000)+" kN)\nFF2 · Base: "+DisplayText.N(r.FF2BaseN)+" N ("+DisplayText.N(r.FF2BaseN/1000)+" kN) · Design: "+DisplayText.N(r.FF2DesignN)+" N ("+DisplayText.N(r.FF2DesignN/1000)+" kN)");
    }else {Note("FF1 / FF2 · "+DisplayText.StatusWord(r.Status)+" · forces unavailable");foreach(var issue in r.Issues)Note(DisplayText.MessageText(issue.Message));}
    Form.CalculationGroup("Equations & definitions",()=>{
     Note("FF1 base = Q × 10 N/kg × internal width / (4h)\nFF2 base = Q × 10 N/kg × internal depth / (8h)\nEach design force = its base × 2, once. Length units cancel when consistent. The coefficient 10 N/kg remains separate from g = 9.81 m/s².");
     Note(CarGuideForceCalculationEngine.Definitions);foreach(var detail in r.Details.Where(d=>d.StartsWith("FF1 base")||d.StartsWith("FF2 base")))Note(detail);
     Note("Rule version: "+r.RuleVersion);
    },false);
   }finally{Content=parent;}
  }
 }
}
