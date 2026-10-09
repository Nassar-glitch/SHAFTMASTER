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
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// Real rendered WPF controls and their normal event handlers. This harness does
// not simulate the engine's UI validation or claim interactive mouse coverage.
public static class SafetyPitDraftUiTests
{
    static MainWindow window;
    static string output;
    static int checks;
    static readonly List<string> names = new List<string>();

    // The window state through its internal test-automation facade (TestAutomation, ARCHITECTURE-PLAN P4).
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> PitChoices() { return (Dictionary<string, ComboBox>)Get("safetyPitChoices"); }
    static Dictionary<string, ComboBox> CalculationChoices() { return (Dictionary<string, ComboBox>)Get("calculationChoices"); }
    static Dictionary<string, string> DraftErrors() { return (Dictionary<string, string>)Get("calculationDraftErrors"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static SafetyPitCalculationResult Result() { return (SafetyPitCalculationResult)Get("safetyPitResult"); }

    static IEnumerable<DependencyObject> Tree(DependencyObject node)
    {
        yield return node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i)
            foreach (var child in Tree(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    static string Text()
    {
        var results = Get("safetyPitResults") as DependencyObject;
        return results == null ? "" : String.Join("\n", Tree(results).OfType<TextBlock>().Select(t => t.Text));
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        ++checks;
        names.Add(name);
    }
    static void Edit(string field, string text) { Editors()["Calculations." + field].Text = text; }
    static void ClickCalculate()
    {
        window.UpdateLayout();
        /* Calculate buttons were replaced by live calculation: run the debounced calculation now. */
        Call("RunLiveCalculation");
        Check(Result() != null, "Calculate returns independent assembly results, including malformed drafts");
    }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Pit()
    {
        await Ready();
        var section = Tree(window).OfType<Expander>().First(x => Convert.ToString(x.Header) == "Safety gear & pit loads");
        section.IsExpanded = true;
        section.BringIntoView();
        await Ready();
    }
    static async Task Select(Dictionary<string, ComboBox> choices, string field, string id)
    {
        await Ready();
        var box = choices[field];
        int index = 0;
        foreach (var item in box.Items)
        {
            if (Convert.ToString(item.GetType().GetProperty("Id").GetValue(item, null)) == id) break;
            ++index;
        }
        if (index >= box.Items.Count) throw new Exception("Choice missing: " + field + " = " + id);
        box.SelectedIndex = index;
        await Ready();
    }
    static async Task Choose(string field, string id) { await Pit(); await Select(PitChoices(), field, id); await Pit(); }
    static async Task Mode(string id) { await Select(CalculationChoices(), "Mode", id); await Pit(); }
    static async Task Navigate()
    {
        Call("ShowPage", "GENERAL");
        await Ready();
        Call("ShowPage", "CALCULATIONS");
        await Pit();
    }
    static async Task Shot(string name)
    {
        await Pit();
        ((FrameworkElement)Get("safetyPitResults")).BringIntoView();
        await Ready();
        Call("Screenshot", Path.Combine(output, name + ".png"));
    }
    static void Invalid(SafetyPitLoad load, string field, string name)
    {
        Check(load.Status == "INVALID_INPUT" && !load.GroupLoadN.HasValue &&
              !load.PerComponentLoadN.HasValue && !load.ParticipatingMassKg.HasValue,
              name + ": no group, component or participating-mass values retained");
        Check(load.Issues.Any(issue => issue.Field == "Calculations." + field &&
              !String.IsNullOrWhiteSpace(issue.Message)), name + ": field-specific draft error");
    }
    static void CarInvalid(string field, string name)
    {
        Invalid(Result().P5, field, name + " P5");
        Invalid(Result().P6, field, name + " P6");
    }
    static void Available(SafetyPitLoad load, double force, string name)
    {
        Check(load.Status == "CALCULATED_UNDER_NOTE_ASSUMPTIONS" &&
              load.PerComponentLoadN == force, name);
    }
    static void CarAvailable(double p5, double p6, string name)
    {
        Available(Result().P5, p5, name + " P5");
        Available(Result().P6, p6, name + " P6");
    }
    static void CanonicalRender()
    {
        // This deliberately injects the exact previously unsafe result: canonical
        // optional null calculated as omission while the textbox remains invalid.
        Set("safetyPitResult", new SafetyPitCalculationEngine().Calculate(Session().Project));
        Call("RenderSafetyPitResults");
    }

    [STAThread]
    public static int Main(string[] arguments)
    {
        output = arguments[0];
        Directory.CreateDirectory(output);
        var application = new Application();
        window = new MainWindow(new string[0]);
        Set("smoke", true);
        int exit = 1;
        window.Loaded += async (sender, eventArguments) =>
        {
            try
            {
                var project = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "reviewed-example.elevator.json")));
                project.Info.Name = "SYNTHETIC SHARED MASS DRAFT UI REGRESSION — NOT PROJECT DEFAULTS";
                project.Elevator.CapacityKg = 480;
                project.Calculations.Mode = "Basic";
                project.Calculations.CarMassKg = 500;
                project.Calculations.CounterweightMassKg = 740;
                project.Calculations.MachineMassKg = 300;
                project.Calculations.CarSideAdditionalSuspendedMassKg = 600;
                project.Calculations.CounterweightSideAdditionalSuspendedMassKg = null;
                project.Calculations.SafetyPit = new SafetyPitInputs
                {
                    SafetyGearType = "Progressive", CarGuideCount = 2,
                    CarBufferCount = 1, CounterweightBufferCount = 1,
                    CarAdditionalMassAllocation = "AllParticipates",
                    CounterweightAdditionalMassAllocation = "AllParticipates"
                };
                Set("session", new ProjectSession(project));
                Call("ResetCalculations");
                Call("ShowPage", "CALCULATIONS");
                await Pit();
                ClickCalculate();
                CarAvailable(15800, 63200, "Initial note fixture");
                Available(Result().P7, 29600, "Initial CW fixture");

                Edit("CarSideAdditionalSuspendedMassKg", "invalid");
                Check(project.Calculations.CarSideAdditionalSuspendedMassKg == null &&
                    DraftErrors().ContainsKey("Calculations.CarSideAdditionalSuspendedMassKg"),
                    "Malformed car addition is canonical null with a distinct retained UI draft error");
                Check(!Text().Contains("15800") && !Text().Contains("63200"), "Car edit immediately removes prior event values");
                ClickCalculate();
                CarInvalid("CarSideAdditionalSuspendedMassKg", "Malformed car addition");
                Available(Result().P7, 29600, "Malformed car addition preserves independent CW buffer");
                Check(Text().Contains("Invalid input") && Text().Contains("car-side") &&
                      !Text().Contains("9800 N") && !Text().Contains("39200 N"),
                      "Rendered car error identifies field and does not present omission-based reduced loads");
                await Shot("01-malformed-car-addition-independent-cw");

                CanonicalRender();
                CarInvalid("CarSideAdditionalSuspendedMassKg", "Rendering-only safeguard for canonical car omission result");
                Available(Result().P7, 29600, "Rendering-only safeguard preserves independently valid CW result");
                Check(!Text().Contains("9800 N") && !Text().Contains("39200 N"),
                    "Rendering canonical cached result cannot bypass malformed car draft validation");
                await Navigate();
                Check(Editors()["Calculations.CarSideAdditionalSuspendedMassKg"].Text == "invalid",
                    "Car malformed draft survives navigation");
                CarInvalid("CarSideAdditionalSuspendedMassKg", "Navigation render retains car draft error");
                Available(Result().P7, 29600, "Navigation render preserves independent CW result");
                await Mode("Advanced");
                Check(Editors()["Calculations.CarSideAdditionalSuspendedMassKg"].Text == "invalid",
                    "Car malformed draft survives Basic to Advanced");
                CarInvalid("CarSideAdditionalSuspendedMassKg", "Advanced render retains shared car draft error");
                ClickCalculate();
                Available(Result().P7, 29600, "Advanced event calculation ignores incomplete Advanced chassis inputs");
                await Mode("Basic");
                CarInvalid("CarSideAdditionalSuspendedMassKg", "Basic return retains shared car draft error");
                Edit("CarSideAdditionalSuspendedMassKg", "600");
                ClickCalculate();
                CarAvailable(15800, 63200, "Corrected car addition");
                Check(!DraftErrors().ContainsKey("Calculations.CarSideAdditionalSuspendedMassKg"),
                    "Correcting car addition removes its draft error");

                Edit("CounterweightSideAdditionalSuspendedMassKg", "200");
                ClickCalculate();
                Available(Result().P7, 37600, "Explicit CW addition included once");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "invalid");
                ClickCalculate();
                Invalid(Result().P7, "CounterweightSideAdditionalSuspendedMassKg", "Malformed CW addition");
                CarAvailable(15800, 63200, "Malformed CW addition preserves independent car loads");
                Check(Text().Contains("Invalid input") && Text().Contains("CW-side") && !Text().Contains("29600 N"),
                    "Rendered CW error identifies field without false reduced P7");
                await Shot("02-malformed-cw-addition-independent-car");
                CanonicalRender();
                Invalid(Result().P7, "CounterweightSideAdditionalSuspendedMassKg", "Rendering-only safeguard for canonical CW omission result");
                CarAvailable(15800, 63200, "Rendering-only CW safeguard preserves car results");
                await Navigate();
                Check(Editors()["Calculations.CounterweightSideAdditionalSuspendedMassKg"].Text == "invalid",
                    "CW malformed draft survives navigation");
                Invalid(Result().P7, "CounterweightSideAdditionalSuspendedMassKg", "Navigation render retains CW draft error");
                await Mode("Advanced");
                ClickCalculate();
                Invalid(Result().P7, "CounterweightSideAdditionalSuspendedMassKg", "Advanced event calculation retains CW draft error");
                CarAvailable(15800, 63200, "Advanced mode preserves valid car events");
                await Mode("Basic");
                Invalid(Result().P7, "CounterweightSideAdditionalSuspendedMassKg", "Basic return retains CW draft error");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "200");
                ClickCalculate();
                Available(Result().P7, 37600, "Corrected CW addition");

                // Required component mass errors have the same assembly-local validation.
                Edit("CarMassKg", "invalid");
                ClickCalculate();
                CarInvalid("CarMassKg", "Malformed required car mass");
                Available(Result().P7, 37600, "Malformed required car mass preserves CW");
                Edit("CarMassKg", "500");
                Edit("CounterweightMassKg", "invalid");
                ClickCalculate();
                Invalid(Result().P7, "CounterweightMassKg", "Malformed required CW mass");
                CarAvailable(15800, 63200, "Malformed required CW mass preserves car");
                Edit("CounterweightMassKg", "740");

                // Blank is deliberate omission; zero is an explicit valid mass.
                Edit("CarSideAdditionalSuspendedMassKg", "");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "");
                ClickCalculate();
                CarAvailable(9800, 39200, "Intentional blank additions");
                Available(Result().P7, 29600, "Intentional blank CW addition");
                Check(!DraftErrors().ContainsKey("Calculations.CarSideAdditionalSuspendedMassKg") &&
                      !DraftErrors().ContainsKey("Calculations.CounterweightSideAdditionalSuspendedMassKg"),
                      "Blank optional inputs are distinct from malformed drafts");
                Edit("CarSideAdditionalSuspendedMassKg", "0");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "0");
                ClickCalculate();
                CarAvailable(9800, 39200, "Explicit zero additions");
                Available(Result().P7, 29600, "Explicit zero CW addition");
                Check(project.Calculations.CarSideAdditionalSuspendedMassKg == 0 &&
                    project.Calculations.CounterweightSideAdditionalSuspendedMassKg == 0,
                    "Explicit zero optional masses remain canonical zeros");

                // Irrelevant malformed fields must never become hidden event requirements.
                Edit("CarSideAdditionalSuspendedMassKg", "600");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "200");
                await Mode("Advanced");
                foreach (string field in new[] { "MachineMassKg", "MachineCgOffsetXMm", "ChassisMassKg", "HangerAssemblyMassKg" })
                    Edit(field, "invalid");
                ClickCalculate();
                CarAvailable(15800, 63200, "Irrelevant machine/CG/chassis/hanger malformed inputs");
                Available(Result().P7, 37600, "Irrelevant malformed chassis inputs preserve CW event");
                Check(new[] { "MachineMassKg", "MachineCgOffsetXMm", "ChassisMassKg", "HangerAssemblyMassKg" }
                    .All(f => DraftErrors().ContainsKey("Calculations." + f)),
                    "Irrelevant malformed inputs are real textbox draft errors, not omitted fixture fields");
                await Mode("Basic");
                ClickCalculate();
                CarAvailable(15800, 63200, "Retained irrelevant Advanced draft errors ignored in Basic events");
                Available(Result().P7, 37600, "Retained irrelevant Advanced errors do not block Basic CW event");

                // Percentage failures remain assembly-local, and inactive drafts stay ignored.
                await Choose("CarAdditionalMassAllocation", "FractionParticipates");
                Edit("SafetyPit.CarEventAdditionalMassPercent", "50");
                ClickCalculate();
                CarAvailable(12800, 51200, "Car percentage allocates shared mass once");
                Edit("SafetyPit.CarEventAdditionalMassPercent", "invalid");
                ClickCalculate();
                CarInvalid("SafetyPit.CarEventAdditionalMassPercent", "Malformed active car percentage");
                Available(Result().P7, 37600, "Malformed active car percentage preserves CW event");
                await Choose("CarAdditionalMassAllocation", "AllParticipates");
                ClickCalculate();
                CarAvailable(15800, 63200, "Inactive invalid car percentage ignored");
                await Choose("CounterweightAdditionalMassAllocation", "FractionParticipates");
                Edit("SafetyPit.CounterweightEventAdditionalMassPercent", "invalid");
                ClickCalculate();
                Invalid(Result().P7, "SafetyPit.CounterweightEventAdditionalMassPercent", "Malformed active CW percentage");
                CarAvailable(15800, 63200, "Malformed active CW percentage preserves car events");
                await Choose("CounterweightAdditionalMassAllocation", "AllParticipates");
                ClickCalculate();
                Available(Result().P7, 37600, "Inactive invalid CW percentage ignored");
                await Choose("CarAdditionalMassAllocation", "FractionParticipates");
                Edit("CarSideAdditionalSuspendedMassKg", "");
                await Choose("CounterweightAdditionalMassAllocation", "FractionParticipates");
                Edit("CounterweightSideAdditionalSuspendedMassKg", "0");
                ClickCalculate();
                CarAvailable(9800, 39200, "No car addition makes malformed retained percentage inactive");
                Available(Result().P7, 29600, "Zero CW addition makes malformed retained percentage inactive");
                await Shot("03-blank-zero-additions-ignored-inactive-percentages");

                exit = 0;
            }
            catch (Exception error)
            {
                Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = error.ToString() });
            }
            finally
            {
                Wire.Write(Path.Combine(output, "safety-pit-draft-ui-tests.json"), new
                {
                    Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names,
                    Method = "Real rendered WPF controls driven through normal textbox, combo and button event handlers. Canonical-result injection separately checks rendering validation. Not interactive mouse/keyboard testing."
                });
                window.Close();
            }
        };
        application.Run(window);
        return exit;
    }
}
