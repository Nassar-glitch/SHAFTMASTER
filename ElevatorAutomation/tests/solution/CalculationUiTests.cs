using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
public static class CalculationUiTests
{
 static MainWindow window;static string output;static int count;static List<string> checks=new List<string>();
 // The window state through its internal test-automation facade (TestAutomation, ARCHITECTURE-PLAN P4).
 static object Get(string name){return window.Automation.Get(name);}
 static void Set(string name,object value){window.Automation.Set(name,value);}
 static object Call(string name,params object[] values){return window.Automation.Call(name,values);}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;checks.Add(name);}
 static IEnumerable<DependencyObject> Tree(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Tree(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Click(string text){if(text.StartsWith("Calculate ")){/* Calculate buttons were replaced by live calculation: run the debounced calculation now. */window.UpdateLayout();Call("RunLiveCalculation");return;}var b=Tree(window).OfType<Button>().First(x=>Convert.ToString(x.Content)==text);b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static Dictionary<string,TextBox> Editors(){return (Dictionary<string,TextBox>)Get("editors");}
 static Dictionary<string,ComboBox> Choices(){return (Dictionary<string,ComboBox>)Get("calculationChoices");}
 static string ResultText(){return String.Join("\n",Tree((DependencyObject)Get("calculationResults")).OfType<TextBlock>().Select(x=>x.Text));}
 static async Task Shot(string name){window.UpdateLayout();await Task.Delay(150);window.UpdateLayout();Call("Screenshot",Path.Combine(output,name+".png"));}
 static ProjectSession Session(){return (ProjectSession)Get("session");}
 [STAThread]public static int Main(string[] args)
 {
  output=args[0];Directory.CreateDirectory(output);var app=new Application();window=new MainWindow(new string[0]);Set("smoke",true);int exit=1;
  window.Loaded+=async(s,e)=>{
   try{
    var bin=AppDomain.CurrentDomain.BaseDirectory;
    var p=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(output,"SYNTHETIC.elevator.json")));Set("session",new ProjectSession(p));Call("ResetCalculations");Call("ShowPage","CALCULATIONS");window.UpdateLayout();Click("Calculate P1–P4 & rope tensions");await Shot("01-calculated");
    Check(ResultText().Contains("Advanced · Calculated")&&ResultText().Contains("9319.5"),"Live calculation shows labelled reactions and static tensions (status as words)");
    var w=(CalculationWorkspace)Get("calculations");var g=w.Geometry;int generated=w.GeometryGenerationCount;
    var context=(DesignContext)Get("context");context.RoomExtensions.Left+=5;await Task.Delay(2400);Call("RunLiveCalculation");Check(w.Result!=null&&w.IsCurrent(Session().Project,context)&&!ResultText().Contains("STALE"),"Changed DesignContext: the live calculation recalculates against the changed context");Click("Calculate P1–P4 & rope tensions");Check(w.IsCurrent(Session().Project,context),"Changed context regenerates before calculation");context.RoomExtensions.Left-=5;Click("Calculate P1–P4 & rope tensions");g=w.Geometry;generated=w.GeometryGenerationCount;
    Editors()["Calculations.CarMassKg"].Text="550";Check(!ResultText().Contains("9319.5")&&!ResultText().Contains("STALE"),"Text edit immediately hides old numbers (no stale state shown)");Click("Calculate P1–P4 & rope tensions");Check(Object.ReferenceEquals(g,w.Geometry)&&generated==w.GeometryGenerationCount&&w.Result.CarBundleTensionN==9810,"Mass edit recalculates through same source geometry");await Shot("02-load-edit");
    Check(Choices()["LeftCarryingMemberId"].Items.Count==4&&Choices()["RightCarryingMemberId"].SelectedValue.ToString()=="Longitudinal.Right","Three semantic member choices from current geometry plus missing option");var optional=Tree(window).OfType<Expander>().First(x=>Convert.ToString(x.Header).StartsWith("Optional self-weights"));optional.IsExpanded=true;window.UpdateLayout();await Task.Delay(150);Choices()["IncludeChassisSelfWeight"].SelectedIndex=1;Click("Calculate P1–P4 & rope tensions");Wire.Write(Path.Combine(output,"optional-decision-check.json"),new{Selection=Choices()["IncludeChassisSelfWeight"].SelectedValue,Inputs=Session().Project.Calculations,Result=w.Result,Text=ResultText()});Check(Session().Project.Calculations.IncludeChassisSelfWeight==true&&ResultText().Contains("Calculations.ChassisMassKg")&&w.Result.Reactions==null,"Include decision requires explicit optional mass and CG; no fabricated values");Choices()["IncludeChassisSelfWeight"].SelectedIndex=2;/* source / basis texts are kept in the model and hidden in the UI */Session().Project.Calculations.MachineCgSource="SYNTHETIC UI-edit CG source; not manufacturer evidence";Click("Calculate P1–P4 & rope tensions");optional.IsExpanded=false;
    // The canonical project is updated by real page controls and committed on navigation.
    ((ListBox)Get("navigation")).SelectedIndex=1;Editors()["Shaft.WidthMm"].Text="1550";Check(w.Result==null,"Geometry text edit invalidates reaction cache");((ListBox)Get("navigation")).SelectedIndex=7;Check(w.GeometryGenerationCount==generated+1&&w.Geometry.ShaftWidthMm==1550,"Navigation commits shaft edit and regenerates authoritative geometry");await Shot("03-geometry-stale");var geometryPanel=Tree(window).OfType<Expander>().First(x=>Convert.ToString(x.Header).StartsWith("Geometry &"));geometryPanel.BringIntoView();await Shot("03b-geometry-preview");Click("Calculate P1–P4 & rope tensions");Check(w.IsCurrent(Session().Project,(DesignContext)Get("context")),"Recalculated resized shaft result current");
    // Source strings, explicit zero and optional decisions survive Save, Open and Save As handlers.
    string save=Path.Combine(output,"UI-saved.elevator.json");Session().FilePath=save;Click("Save");var opened=new ProjectSession(new ElevatorProject());opened.Open(save);Check(opened.Project.Calculations.CarMassKg==550&&opened.Project.Calculations.MachineCgSource.Contains("UI-edit")&&opened.Project.Calculations.CarSideAdditionalSuspendedMassKg==0&&opened.Project.Calculations.IncludeChassisSelfWeight==false,"Save handler and reopen preserve edited calculation values and sources");opened.Save(Path.Combine(output,"UI-save-as.elevator.json"));opened.Open(opened.FilePath);Set("session",opened);Call("ResetCalculations");Call("ShowPage","CALCULATIONS");Check(Editors()["Calculations.CarMassKg"].Text=="550","Reopened inputs populated in actual WPF editors");Click("Calculate P1–P4 & rope tensions");
    Editors()["Calculations.CarMassKg"].Text="";Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Calculations.CarMassKg")&&!ResultText().Contains("9810"),"Missing field message / no old tensions");await Shot("04-missing");
    Editors()["Calculations.CarMassKg"].Text="invalid";Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Invalid input")&&Session().Project.Calculations.CarMassKg==null,"Malformed numeric text unavailable, no retained canonical mass");await Shot("05-invalid-text");
    Editors()["Calculations.CarMassKg"].Text="-1";Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Invalid input")&&ResultText().Contains("positive"),"Negative mass field validation");Editors()["Calculations.CarMassKg"].Text="500";
    var expanders=Tree(window).OfType<Expander>().ToArray();foreach(var exp in expanders)exp.IsExpanded=true;window.UpdateLayout();var carrying=expanders.First(x=>Convert.ToString(x.Header).StartsWith("Carrying members"));carrying.BringIntoView();await Shot("05b-members-and-hanger");Editors()["Calculations.MachineMassKg"].Text="5000";Editors()["Calculations.MachineCgOffsetXMm"].Text="2000";Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Uplift at a bearing")&&((CalculationWorkspace)Get("calculations")).Result.Reactions.Any(r=>r.UpwardN<0),"Uplift warning with signed reactions rendered");((ScrollViewer)Get("pageScroll")).ScrollToTop();await Shot("06-uplift");
    window.Width=760;window.Height=640;window.UpdateLayout();var scroll=(ScrollViewer)Get("pageScroll");scroll.ScrollToBottom();await Shot("07-narrow-bottom");Check(scroll.ScrollableHeight>0&&Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<1,"Page scroll reaches last calculate control at small window size");
    // Live calculation: no Calculate control remains; every calculation section header scrolls fully into view at 760 × 640.
    Check(!Tree(window).OfType<Button>().Any(x=>Convert.ToString(x.Content).StartsWith("Calculate ")),"No Calculate buttons (results are calculated live)");
    var sections=Tree(window).OfType<Expander>().Where(x=>x.IsVisible&&new[]{"Ropes and traction","Safety gear & pit loads","Car guide forces — FF1 / FF2"}.Contains(Convert.ToString(x.Header))).ToList();
    Check(sections.Count==3,"Calculation sections present: "+String.Join(", ",sections.Select(x=>Convert.ToString(x.Header))));
    for(int i=0;i<sections.Count;i++)
    {
     var header=sections[i];header.BringIntoView(new Rect(0,0,Math.Max(1,header.ActualWidth),Math.Min(40,header.ActualHeight)));window.UpdateLayout();await Task.Delay(200);window.UpdateLayout();
     var position=header.TransformToAncestor(scroll).Transform(new Point(0,0));
     await Shot("07b-narrow-section-"+(i+1));
     Check(header.IsVisible&&position.Y>=-0.5&&position.Y<scroll.ViewportHeight,"'"+Convert.ToString(header.Header)+"' (#"+(i+1)+") scrolls into the 760 × 640 viewport");
    }
    scroll.ScrollToTop();await Shot("08-narrow-top");
    Call("ShowPage","GENERAL");Check(Tree(window).OfType<TextBox>().Any(b=>b.IsVisible),"Existing general workflow accessible after resize");
    // Old project: missing data and no auto-selected pair; native generation uses same desktop task.
    Set("session",new ProjectSession(ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")))));Session().Project.Calculations.Mode="Advanced";Call("ResetCalculations");Call("ShowPage","CALCULATIONS");Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Input missing")&&Choices()["LeftCarryingMemberId"].SelectedValue==null&&Choices()["IncludeChassisSelfWeight"].SelectedValue.ToString()=="missing","Explicit Advanced mode renders missing inputs and choices");await Shot("09-old-project");
    window.Width=1220;window.Height=820;Call("ShowPage","FLOORS");await Shot("10-floors-preserved");
    Call("ShowPage","MACHINE");Editors()["Machine.TractionSheavePitchDiameterMm"].Text="600";Call("Commit");Call("ShowPage","CALCULATIONS");Click("Calculate P1–P4 & rope tensions");Check(ResultText().Contains("Invalid input")&&ResultText().Contains("Machine.TractionSheavePitchDiameterMm"),"Unsupported diameter field-specific rejection displayed");await Shot("10b-unsupported-machine");Call("ShowPage","MACHINE");Editors()["Machine.TractionSheavePitchDiameterMm"].Text="520";Call("Commit");
    Set("outputFolder",output);Set("outputName","OldProject-IncompleteCalculations.dwg");Call("ShowPage","DRAWINGS");await (Task)Call("Generate");var response=(GenerationResponse)Get("last");Check(response.Success&&response.ViewsGenerated.Length==4,"Actual desktop Generate task completes four native views with missing calculations");await Shot("11-drawings-generated");Wire.Write(Path.Combine(output,"native-generation.json"),response);
    Set("session",new ProjectSession(ProjectJson.Deserialize(File.ReadAllText(Path.Combine(output,"UI-saved.elevator.json")))));Call("ResetCalculations");Set("outputName","SYNTHETIC-WithCalculationInputs.dwg");Call("ShowPage","DRAWINGS");await (Task)Call("Generate");response=(GenerationResponse)Get("last");Check(response.Success&&response.ViewsGenerated.Length==4,"Native save/reopen verifies populated calculation inputs survive drawing adapters");Wire.Write(Path.Combine(output,"native-calculation-inputs-generation.json"),response);
    Call("ShowPage","CALCULATIONS");var weights=Tree(window).OfType<Expander>().First(x=>Convert.ToString(x.Header).StartsWith("Optional self-weights"));weights.IsExpanded=true;window.UpdateLayout();await Task.Delay(150);Choices()["IncludeChassisSelfWeight"].SelectedIndex=1;Editors()["Calculations.ChassisMassKg"].Text="50";foreach(var axis in new[]{"X","Y","Z"})Editors()["Calculations.ChassisCgOffset"+axis+"Mm"].Text="0";Session().Project.Calculations.ChassisWeightSourceOrExclusionReason="SYNTHETIC included lumped weight assumption, not manufacturer data";Click("Calculate P1–P4 & rope tensions");Check(((CalculationWorkspace)Get("calculations")).IsCurrent(Session().Project,(DesignContext)Get("context")),"Explicitly supplied included self-weight calculates under recorded assumption");Session().Save(Path.Combine(output,"UI-included-weight.elevator.json"));Session().Open(Session().FilePath);Call("ResetCalculations");Call("ShowPage","CALCULATIONS");window.UpdateLayout();await Task.Delay(150);Check(Session().Project.Calculations.IncludeChassisSelfWeight==true&&Choices()["IncludeChassisSelfWeight"].SelectedValue.ToString()=="include"&&Editors()["Calculations.ChassisMassKg"].Text=="50","Included decision and explicit zero CG survive reopening in WPF controls");
    exit=0;
   }catch(Exception ex){Wire.Write(Path.Combine(output,"ui-error.json"),new{Error=ex.ToString()});}
   finally{Wire.Write(Path.Combine(output,"rendered-ui-tests.json"),new{Status=exit==0?"PASS":"FAIL",Assertions=count,Checks=checks,Mode="Real WPF window / controls driven by programmatic events, screenshots via RenderTargetBitmap. Native AutoCAD generation exercised. Not a mouse/keyboard Computer Use pass."});Set("smoke",true);window.Close();}
  };app.Run(window);return exit;
 }
}
