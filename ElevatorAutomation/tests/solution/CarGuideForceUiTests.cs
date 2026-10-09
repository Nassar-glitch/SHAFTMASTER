using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

// Real rendered WPF controls driven through their normal TextChanged,
// SelectionChanged and Click handlers. This is not mouse/keyboard testing.
public static class CarGuideForceUiTests
{
    const string SpacingPath = "Calculations.CarGuideForces.GuideShoeSpacingMm";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static MainWindow window;
    static string output;
    static int checks;
    static readonly List<string> names = new List<string>();

    static object Get(string name) { return typeof(MainWindow).GetField(name, PrivateInstance).GetValue(window); }
    static void Set(string name, object value) { typeof(MainWindow).GetField(name, PrivateInstance).SetValue(window, value); }
    static object Call(string name, params object[] arguments) { return typeof(MainWindow).GetMethod(name, PrivateInstance).Invoke(window, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> Choices() { return (Dictionary<string, ComboBox>)Get("calculationChoices"); }
    static Dictionary<string, ComboBox> PitChoices() { return (Dictionary<string, ComboBox>)Get("safetyPitChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static object Result() { return Get("carGuideForceResult"); }
    static object Member(object item, string name)
    {
        if (item == null) return null;
        var field = item.GetType().GetField(name);
        if (field != null) return field.GetValue(item);
        var property = item.GetType().GetProperty(name);
        if (property != null) return property.GetValue(item, null);
        throw new Exception("Expected member missing: " + item.GetType().FullName + "." + name);
    }
    static double? Force(string field)
    {
        var value = Member(Result(), field);
        return value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
    static IEnumerable<DependencyObject> Tree(DependencyObject node)
    {
        yield return node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i)
            foreach (var child in Tree(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    static string Text()
    {
        var panel = Get("carGuideForceResults") as DependencyObject;
        return panel == null ? "" : String.Join("\n", Tree(panel).OfType<TextBlock>().Select(t => t.Text));
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        ++checks;
        names.Add(name);
    }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Section()
    {
        await Ready();
        var section = Tree(window).OfType<Expander>().First(x => Convert.ToString(x.Header) == "Car guide forces — FF1 / FF2");
        section.IsExpanded = true;
        section.BringIntoView();
        await Ready();
    }
    static void Click(string content)
    {
        window.UpdateLayout();
        if (content.StartsWith("Calculate ")) { /* Calculate buttons were replaced by live calculation: run the debounced calculation now. */ Call("RunLiveCalculation"); return; }
        Tree(window).OfType<Button>().First(b => Convert.ToString(b.Content) == content)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    static void Calculate()
    {
        Click("Calculate FF1 / FF2");
        Check(Result() != null, "FF Calculate returns an explicit available/unavailable result");
    }
    static void SetSpacing(string text) { Editors()[SpacingPath].Text = text; }
    static async Task Navigate(string page)
    {
        string[] pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
        ((ListBox)Get("navigation")).SelectedIndex = Array.IndexOf(pages, page);
        await Ready();
        if (page == "CALCULATIONS") await Section();
    }
    static async Task Mode(string id)
    {
        var box = Choices()["Mode"];
        int index = 0;
        foreach (var item in box.Items)
        {
            if (Convert.ToString(item.GetType().GetProperty("Id").GetValue(item, null)) == id) break;
            ++index;
        }
        Check(index < box.Items.Count, "Mode is available: " + id);
        box.SelectedIndex = index;
        await Section();
    }
    static async Task PitChoose(string field, string id)
    {
        var section = Tree(window).OfType<Expander>().First(x => Convert.ToString(x.Header) == "Safety gear & pit loads");
        section.IsExpanded = true;
        await Ready();
        var box = PitChoices()[field];
        int index = 0;
        foreach (var item in box.Items)
        {
            if (Convert.ToString(item.GetType().GetProperty("Id").GetValue(item, null)) == id) break;
            ++index;
        }
        Check(index < box.Items.Count, "Pit choice is available: " + field + " = " + id);
        box.SelectedIndex = index;
        await Ready();
    }
    static void Available(double width, double depth, double capacity, double h, string name)
    {
        double one = capacity * 10 * width / (4 * h);
        double two = capacity * 10 * depth / (8 * h);
        Check(Force("FF1BaseN").HasValue && Math.Abs(Force("FF1BaseN").Value - one) < 1e-8 &&
              Force("FF1DesignN").HasValue && Math.Abs(Force("FF1DesignN").Value - 2 * one) < 1e-8,
              name + ": FF1 base/design once, adopted 10 N/kg");
        Check(Force("FF2BaseN").HasValue && Math.Abs(Force("FF2BaseN").Value - two) < 1e-8 &&
              Force("FF2DesignN").HasValue && Math.Abs(Force("FF2DesignN").Value - 2 * two) < 1e-8,
              name + ": FF2 base/design once, internal depth");
                Check(Convert.ToString(Member(Result(), "Status")).Contains("CALCULATED"), name + ": adopted-rule calculated status");
        Check(Convert.ToDouble(Member(Result(), "InternalWidthMm")) == width &&
              Convert.ToDouble(Member(Result(), "InternalDepthMm")) == depth &&
              Convert.ToDouble(Member(Result(), "CapacityKg")) == capacity &&
              Convert.ToDouble(Member(Result(), "GuideShoeSpacingMm")) == h,
              name + ": result records current reused dimensions, capacity and spacing");
    }
    static void Unavailable(string field, string name)
    {
        Check(!Force("FF1BaseN").HasValue && !Force("FF1DesignN").HasValue &&
              !Force("FF2BaseN").HasValue && !Force("FF2DesignN").HasValue,
              name + ": all base/design forces unavailable, not zero");
        var issues = ((IEnumerable)Member(Result(), "Issues")).Cast<object>();
        Check(issues.Any(i => Convert.ToString(Member(i, "Field")) == field &&
              !String.IsNullOrWhiteSpace(Convert.ToString(Member(i, "Message")))), name + ": field-specific validation");
    }
    static void Hidden(string name)
    {
        Check(!Text().Contains("1100 N") && !Text().Contains("2200 N") &&
              !Text().Contains("700 N") && !Text().Contains("1400 N"), name);
    }
    static async Task Shot(string name, bool details)
    {
        await Section();
        if (details)
        {
            var section = Tree(window).OfType<Expander>().First(x => Convert.ToString(x.Header) == "Car guide forces — FF1 / FF2");
            var detail = Tree(section).OfType<Expander>().First(x => x != section);
            detail.IsExpanded = true;
            detail.BringIntoView();
        }
        else ((FrameworkElement)Get("carGuideForceResults")).BringIntoView();
        await Ready();
        Call("Screenshot", Path.Combine(output, name + ".png"));
    }
    static void Attach(ElevatorProject p)
    {
        Set("session", new ProjectSession(p));
        Call("ResetCalculations");
        Call("ShowPage", "CALCULATIONS");
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
                Call("ShowPage", "CALCULATIONS");
                await Section();
                Check(Session().Project.Calculations.Mode == "Basic" && Editors()[SpacingPath].Text == "2800",
                    "New project is Basic and h follows the preset (2800 mm)");
                Check(!Editors().Keys.Any(k => k.Contains("CarGuideForces") && k != SpacingPath),
                    "FF section has only one editable input, no duplicate capacity or cabin dimensions");

                var bin = AppDomain.CurrentDomain.BaseDirectory;
                var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
                p.Info.Name = "SYNTHETIC FF UI VERIFICATION — NOT PROJECT DEFAULTS";
                p.Elevator.CapacityKg = 480;
                p.Shaft.WidthMm = 1500;
                p.Shaft.DepthMm = 1800;
                p.Calculations.Mode = "Basic";
                p.Calculations.CarMassKg = null;
                p.Calculations.CounterweightMassKg = null;
                p.Calculations.MachineMassKg = null;
                p.Suspension.RopeCount = null;
                Attach(p);
                await Section();
                Calculate();
                Unavailable(SpacingPath, "Missing h");
                SetSpacing("1200");
                Calculate();
                Available(1100, 1400, 480, 1200, "Live shared internal cabin dimensions");
                Check(Text().Contains("N") && Text().Contains("kN"), "Base/design forces show N and kN units");
                Check(p.Calculations.CarMassKg == null && p.Calculations.CounterweightMassKg == null &&
                      p.Calculations.MachineMassKg == null && p.Suspension.RopeCount == null,
                    "FF calculates without unrelated chassis masses or rope-count inputs");
                await Shot("01-compact-ff-form-and-results", false);
                await Shot("02-ff-equations-and-adopted-rule", true);
                Check(Text().Contains("10 N/kg") && Text().Contains("1200") && Text().Contains("1100") && Text().Contains("1400"),
                    "Expandable equations show adopted coefficient, shoe spacing and reused cabin dimensions");
                // DESIGN-FLOW stage 2: the UI carries no interpretation remark; the definitions keep the h meaning.
                Check(!Text().Contains("Approved interpretation") && Text().Contains("centre-to-centre"),
                    "Rendered FF results carry no interpretation remark; definitions keep the h meaning");
                Check(!Text().Contains("unconfirmed") && !Text().Contains("does not establish their project X/Y") &&
                      !Text().Contains("require confirmation"),
                    "Rendered FF results no longer say the interpretation is unresolved");

                // Geometry/capacity editors use actual navigation and normal Commit.
                await Navigate("GENERAL");
                Editors()["Elevator.CapacityKg"].Text = "500";
                await Navigate("CALCULATIONS");
                Hidden("Capacity edit removes prior FF numbers before recalculation");
                Calculate();
                Available(1100, 1400, 500, 1200, "Changed canonical capacity");
                await Navigate("SHAFT");
                Editors()["Shaft.WidthMm"].Text = "1650";
                Editors()["Shaft.DepthMm"].Text = "2000";
                await Navigate("CALCULATIONS");
                Calculate();
                Available(1250, 1600, 500, 1200, "Changed shared geometry rather than retained cabin dimensions");
                await Shot("03-changed-geometry-capacity", false);

                // Reset to simple exact values for malformed draft and stale rendering checks.
                await Navigate("GENERAL");
                Editors()["Elevator.CapacityKg"].Text = "480";
                await Navigate("SHAFT");
                Editors()["Shaft.WidthMm"].Text = "1500";
                Editors()["Shaft.DepthMm"].Text = "1800";
                await Navigate("CALCULATIONS");
                Calculate();
                Available(1100, 1400, 480, 1200, "Restored synthetic live geometry");
                foreach (string bad in new[] { "", "0", "-1", "NaN", "Infinity", "invalid" })
                {
                    SetSpacing(bad);
                    Hidden("h edit immediately hides stale FF values: " + (bad == "" ? "blank" : bad));
                    Calculate();
                    Unavailable(SpacingPath, "Invalid/missing h text: " + (bad == "" ? "blank" : bad));
                }
                Check(Member(Member(p.Calculations, "CarGuideForces"), "GuideShoeSpacingMm") == null,
                    "Malformed spacing clears canonical optional value while retaining invalid textbox draft");
                await Shot("04-malformed-h-field-error", false);
                await Navigate("GENERAL");
                await Navigate("CALCULATIONS");
                Check(Editors()[SpacingPath].Text == "invalid", "Malformed h draft survives navigation");
                Calculate();
                Unavailable(SpacingPath, "Malformed h after navigation");
                await Mode("Advanced");
                Check(Editors()[SpacingPath].Text == "invalid", "Malformed h draft survives Basic to Advanced");
                Calculate();
                Unavailable(SpacingPath, "Malformed h in Advanced");
                await Mode("Basic");
                Check(Editors()[SpacingPath].Text == "invalid", "Malformed h draft survives Advanced to Basic");
                Calculate();
                Unavailable(SpacingPath, "Malformed h after mode round trip");
                SetSpacing("1200");
                Calculate();
                Available(1100, 1400, 480, 1200, "Corrected h after navigation and mode switches");

                // Missing shared capacity is independent from missing optional h.
                await Navigate("GENERAL");
                Editors()["Elevator.CapacityKg"].Text = "";
                await Navigate("CALCULATIONS");
                Calculate();
                Unavailable("Elevator.CapacityKg", "Missing reused capacity");
                await Navigate("GENERAL");
                Editors()["Elevator.CapacityKg"].Text = "480";
                await Navigate("CALCULATIONS");

                // Malformed mass, CG and active safety allocation errors are real
                // WPF drafts and must not become hidden FF requirements.
                Editors()["Calculations.CarMassKg"].Text = "invalid";
                Editors()["Calculations.MachineMassKg"].Text = "invalid";
                Editors()["Calculations.CarSideAdditionalSuspendedMassKg"].Text = "600";
                await PitChoose("CarAdditionalMassAllocation", "FractionParticipates");
                Editors()["Calculations.SafetyPit.CarEventAdditionalMassPercent"].Text = "invalid";
                await Mode("Advanced");
                Editors()["Calculations.MachineCgOffsetXMm"].Text = "invalid";
                Editors()["Calculations.HangerAttachmentOffsetXMm"].Text = "invalid";
                Calculate();
                Available(1100, 1400, 480, 1200, "Unrelated malformed mass/CG/hanger/pit drafts ignored");
                var draftErrors = (Dictionary<string, string>)Get("calculationDraftErrors");
                Check(new[] { "CarMassKg", "MachineMassKg", "MachineCgOffsetXMm", "HangerAttachmentOffsetXMm" }
                    .All(f => draftErrors.ContainsKey("Calculations." + f)),
                    "Ignored Advanced/shared errors are retained actual draft errors rather than missing fixture values");
                await Mode("Basic");
                Calculate();
                Available(1100, 1400, 480, 1200, "Independent FF mode round trip with retained unrelated drafts");
                await Shot("05-ff-independent-of-other-invalid-inputs", false);

                // Use a fresh synthetic source for persistence; deliberately
                // invalid unrelated drafts are not silently saved as valid inputs.
                var savedProject = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
                savedProject.Info.Name = "SYNTHETIC FF SAVE / REOPEN — NOT PROJECT DEFAULTS";
                savedProject.Elevator.CapacityKg = 480;
                savedProject.Shaft.WidthMm = 1500;
                savedProject.Shaft.DepthMm = 1800;
                savedProject.Calculations.Mode = "Basic";
                Attach(savedProject);
                await Section();
                SetSpacing("1200");
                Calculate();
                Session().FilePath = Path.Combine(output, "SYNTHETIC-FF-saved.elevator.json");
                Click("Save");
                var opened = new ProjectSession(new ElevatorProject());
                opened.Open(Session().FilePath);
                opened.Save(Path.Combine(output, "SYNTHETIC-FF-save-as.elevator.json"));
                Check(Convert.ToDouble(Member(Member(opened.Project.Calculations, "CarGuideForces"), "GuideShoeSpacingMm")) == 1200 &&
                      opened.Project.Calculations.Mode == "Basic", "Save/open/save-as preserve h once and Basic mode");
                Set("session", opened);
                Call("ResetCalculations");
                Call("ShowPage", "CALCULATIONS");
                await Section();
                Check(Editors()[SpacingPath].Text == "1200", "Reopened h editor restores canonical spacing");
                Calculate();
                Available(1100, 1400, 480, 1200, "Reopened project reproduces FF values");
                await Mode("Advanced");
                Session().Save(Path.Combine(output, "SYNTHETIC-FF-advanced.elevator.json"));
                var advanced = new ProjectSession(new ElevatorProject());
                advanced.Open(Path.Combine(output, "SYNTHETIC-FF-advanced.elevator.json"));
                Check(advanced.Project.Calculations.Mode == "Advanced" &&
                      Convert.ToDouble(Member(Member(advanced.Project.Calculations, "CarGuideForces"), "GuideShoeSpacingMm")) == 1200,
                    "Persisted FF values do not silently change explicit Advanced mode");
                await Mode("Basic");

                // The one new FF input cannot invalidate or become a hidden
                // requirement of independently valid chassis or pit loads.
                Editors()["Calculations.CarMassKg"].Text = "500";
                Editors()["Calculations.CounterweightMassKg"].Text = "740";
                Editors()["Calculations.MachineMassKg"].Text = "300";
                await PitChoose("SafetyGearType", "Progressive");
                await PitChoose("CarGuideCount", "2");
                await PitChoose("CarBufferCount", "1");
                await PitChoose("CounterweightBufferCount", "1");
                Click("Calculate load distribution");
                var workspace = (CalculationWorkspace)Get("calculations");
                Check(workspace.Result != null && workspace.Result.Reactions != null,
                    "Independent Basic chassis result exists before h-only edits");
                Click("Calculate P5–P7");
                var pitBefore = (SafetyPitCalculationResult)Get("safetyPitResult");
                var chassisBefore = workspace.Result;
                Check(pitBefore.P5.PerComponentLoadN == 9800 && pitBefore.P6.GroupLoadN == 39200 &&
                      pitBefore.P7.GroupLoadN == 29600, "Independent valid P5–P7 exist before h-only edits");
                SetSpacing("1500");
                Check(Object.ReferenceEquals(chassisBefore, workspace.Result) &&
                      Object.ReferenceEquals(pitBefore, Get("safetyPitResult")) &&
                      workspace.IsCurrent(Session().Project, (DesignContext)Get("context")),
                    "h-only edit preserves current Basic P1–P4 and P5–P7 results");
                Calculate();
                Available(1100, 1400, 480, 1500, "Changed h updates FF only");
                SetSpacing("invalid");
                Calculate();
                Unavailable(SpacingPath, "Independent malformed h");
                Click("Calculate load distribution");
                Click("Calculate P5–P7");
                Check(workspace.Result.Reactions != null &&
                      ((SafetyPitCalculationResult)Get("safetyPitResult")).P5.PerComponentLoadN == 9800,
                    "Malformed h does not block Basic chassis or safety/pit calculation buttons");
                SetSpacing("1200");

                window.Width = 760;
                window.Height = 640;
                await Section();
                Editors()[SpacingPath].BringIntoView();
                await Ready();
                Check(Editors()[SpacingPath].IsVisible && Editors()[SpacingPath].ActualWidth > 0,
                    "Spacing control remains visible and usable after narrow resize/scroll");
                var resultsPanel = (StackPanel)Get("carGuideForceResults");
                resultsPanel.BringIntoView();
                await Ready();
                Check(resultsPanel.IsVisible && ((ScrollViewer)Get("pageScroll")).ScrollableHeight > 0,
                    "FF results remain reachable and page scrolls in narrow window");
                Calculate();
                await Shot("06-narrow-ff-results", false);
                window.Width = 1220;
                window.Height = 820;

                var legacy = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
                legacy.Info.Name = "SYNTHETIC OLDER PROJECT FF INPUT ABSENT";
                Attach(legacy);
                await Section();
                Check(Session().Project.Calculations.Mode == "Basic" && Editors()[SpacingPath].Text == "",
                    "Older empty project stays Basic with absent h rather than fabricated default");
                Calculate();
                Unavailable(SpacingPath, "Older project missing h");
                exit = 0;
            }
            catch (Exception error)
            {
                Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = error.ToString() });
            }
            finally
            {
                Wire.Write(Path.Combine(output, "car-guide-force-ui-tests.json"), new
                {
                    Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names,
                    Method = "Real rendered WPF controls driven through normal textbox, navigation, combo and button event handlers. Not interactive mouse/keyboard testing. Synthetic inputs are not installed as project defaults."
                });
                window.Close();
            }
        };
        application.Run(window);
        return exit;
    }
}
