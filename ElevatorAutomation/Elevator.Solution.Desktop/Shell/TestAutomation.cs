using System;
using System.Threading.Tasks;
using System.Windows;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Reporting.Sheet;
namespace Elevator.Solution.Desktop
{
 // In-process test access for the packaged UI test programs (InternalsVisibleTo in AssemblyInfo.cs). It gives the same
 // handles the tests read before the MainWindow split (state, controls, results and actions), by their former names, so
 // the UI suites keep their checks unchanged. Not used by the application.
 sealed class TestAutomation
 {
  readonly MainWindow w;
  public TestAutomation(MainWindow window){w=window;}
  public object Get(string name)
  {
   switch(name)
   {
    // project, services and results
    case "session":return w.Session;
    case "context":return w.Context;
    case "presets":return w.Presets.Library;
    case "design":return w.Live.Design;
    case "calculations":return w.Live.Chassis;
    case "calculationDraftErrors":return w.Live.ChassisDraftErrors;
    case "ropeTractionResult":return w.Live.RopeTraction;
    case "safetyPitResult":return w.Live.SafetyPit;
    case "carGuideForceResult":return w.Live.CarGuideForces;
    case "last":return w.Commands.Last;
    case "busy":return w.Busy;
    case "root":return w.Root;
    case "pages":return w.PageKeys;
    case "detailedStatus":return w.DetailedStatus;
    case "calculationSheetMessage":return w.Reports.CalculationSheetMessage;
    // the visible page
    case "content":return w.Form.Content;
    case "editors":return w.Form.Editors;
    case "presetRefreshers":return w.Form.PresetRefreshers;
    case "presetChoices":return w.Form.PresetChoices;
    case "floorGrid":return w.Form.FloorGrid;
    case "personsLabel":return w.Form.PersonsLabel;
    case "floorRows":return w.Floors.ViewModel.Rows;
    case "calculationChoices":return w.Calculations.Choices;
    case "calculationResults":return w.Calculations.Results;
    case "ropeTractionResults":return w.Calculations.RopeTraction.Results;
    case "safetyPitResults":return w.Calculations.SafetyPit.Results;
    case "safetyPitChoices":return w.Calculations.SafetyPit.Choices;
    case "carGuideForceResults":return w.Calculations.CarGuideForces.Results;
    // shell controls
    case "navigation":return w.Navigation;
    case "navigationPanel":return w.NavigationPanel;
    case "mainPanel":return w.MainPanel;
    case "pageScroll":return w.PageScroll;
    case "toolbar":return w.Toolbar;
    case "status":return w.StatusText;
    case "projectName":return w.ProjectName;
    case "dirtyLabel":return w.DirtyLabel;
    case "outputFolderButton":return w.OutputFolderButton;
   }
   throw new ArgumentException("Unknown test handle: "+name);
  }
  public void Set(string name,object value)
  {
   switch(name)
   {
    case "session":w.Session=(ProjectSession)value;return;
    case "safetyPitResult":w.Live.SafetyPit=(SafetyPitCalculationResult)value;return;
    case "busy":w.Busy=(bool)value;return;
    case "smoke":w.Smoke=(bool)value;return;
    case "root":w.Root=(string)value;return;
    case "outputFolder":w.Drawings.ViewModel.OutputFolder=(string)value;return;
    case "outputName":w.Drawings.ViewModel.OutputName=(string)value;return;
   }
   throw new ArgumentException("Unknown test handle: "+name);
  }
  public object Call(string name,params object[] arguments)
  {
   var a=arguments??new object[0];
   switch(name)
   {
    case "ShowPage":w.ShowPage((string)a[0]);return null;
    case "Commit":w.Commit();return null;
    case "Screenshot":w.Screenshot((string)a[0]);return null;
    case "SetBusy":w.SetBusy((bool)a[0]);return null;
    case "SetStatus":w.SetStatus((string)a[0]);return null;
    case "SetOutputLocation":w.SetOutputLocation((string)a[0]);return null;
    case "ResetCalculations":w.ResetCalculations();return null;
    case "NewProject":return w.Commands.NewProject();
    case "Calculate":w.Commands.Calculate();return null;
    case "Generate":return w.Commands.Generate();
    case "ApplyProjectPresets":w.PresetEditing.Apply(a.Length>0?(string)a[0]:null,a.Length>1?(bool)a[1]:true);return null;
    case "RunLiveCalculation":w.Updates.Run();return null;
    case "InvalidateRopeTractionDisplay":w.Updates.InvalidateRopeTraction();return null;
    case "RenderSafetyPitResults":w.Calculations.SafetyPit.Render();return null;
    case "BuildCalculationSheet":return w.Reports.BuildCalculationSheet();
    case "WriteCalculationSheet":return w.Reports.WriteCalculationSheet((string)a[0]);
    case "WriteDesignNotes":return w.Reports.WriteDesignNotes((string)a[0]);
    case "ConfirmCalculationSheet":return w.Reports.ConfirmCalculationSheet((CalculationSheet)a[0],(Func<Window,bool>)a[1]);
    case "OmissionDialog":return w.Reports.OmissionDialog((CalculationSheet)a[0]);
   }
   throw new ArgumentException("Unknown test action: "+name);
  }
 }
}
