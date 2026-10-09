using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // CALCULATIONS: the mode (Basic / Advanced) and its panels: P1–P4 inputs and results, ropes and traction, safety gear
 // and pit loads, car guide forces FF1 / FF2. Results are live (LiveUpdates).
 sealed class CalculationsPage:PageView
 {
  readonly CalculationsViewModel vm=new CalculationsViewModel();
  public CalculationsPage(MainWindow shell):base(shell)
  {
   Inputs=new ChassisInputs(shell,this);Basic=new BasicPanel(shell,this);Advanced=new AdvancedPanel(shell,this);
   RopeTraction=new RopeTractionPanel(shell);SafetyPit=new SafetyPitPanel(shell);CarGuideForces=new CarGuideForcePanel(shell);
  }
  public CalculationsViewModel ViewModel{get{return vm;}}
  public override string Key{get{return vm.Key;}}
  // The mode and chassis-input combo boxes (by field name).
  public readonly Dictionary<string,ComboBox> Choices=new Dictionary<string,ComboBox>();
  // The P1–P4 result panel of the visible mode.
  public StackPanel Results;
  public readonly ChassisInputs Inputs;
  public readonly BasicPanel Basic;
  public readonly AdvancedPanel Advanced;
  public readonly RopeTractionPanel RopeTraction;
  public readonly SafetyPitPanel SafetyPit;
  public readonly CarGuideForcePanel CarGuideForces;
  public override void Build()
  {
   if(Session.Project.Calculations==null)Session.Project.Calculations=new ChassisCalculationInputs();
   var c=Session.Project.Calculations;if(c.Mode==null){c.Mode=CalculationModes.Resolve(c);Session.Dirty=true;}
   Shell.Updates.Run();
   Choices.Clear();Results=null;SafetyPit.Results=null;CarGuideForces.Results=null;CarGuideForces.Dimensions=null;
   var choices=CalculationsViewModel.ModeChoices(c.Mode);
   var mode=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=c.Mode,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)};Choices["Mode"]=mode;System.Windows.Automation.AutomationProperties.SetName(mode,"Calculation mode");
   mode.SelectionChanged+=(s,e)=>{if(mode.SelectedValue==null)return;c.Mode=(string)mode.SelectedValue;Session.Dirty=true;Live.Design=null;Live.Chassis.Invalidate();Shell.ShowPage(Key);};Content.Children.Add(mode);
   Form.FillBlanksButton(Key);
   if(c.Mode=="Basic")Basic.Build();else if(c.Mode=="Advanced")Advanced.Build();else Note("Unsupported mode. Select a supported workflow explicitly.");
  }
  bool IsBasic{get{return CalculationModes.Resolve(Session.Project.Calculations)=="Basic";}}
  public void RenderResults(){if(Results==null)return;if(IsBasic){Basic.Render();return;}Advanced.Render();}
  public void RenderAll(){RenderResults();RopeTraction.Render();SafetyPit.Render();CarGuideForces.Render();}
  // The result panels of the rope / traction, safety / pit and FF sections (after Fill blanks).
  public void ResetPanels(){SafetyPit.ResetView();RopeTraction.ResetView();CarGuideForces.ResetView();}
  // Every result panel (new or opened project).
  public void ResetViews(){Results=null;ResetPanels();}
  // The Basic / Advanced panels: the P1–P4 results of the mode, then ropes, safety / pit and FF.
  public void AddResultsAndSections(Thickness? resultsMargin)
  {
   Results=resultsMargin.HasValue?new StackPanel{Margin=resultsMargin.Value}:new StackPanel();Content.Children.Add(Results);RenderResults();
   RopeTraction.Section();
   SafetyPit.Section();
   CarGuideForces.Section();
  }
 }
}
