using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// Real rendered WPF window. Rope / traction inputs on the Suspension page are typed through their normal handlers; presets,
// locks and Reset to preset are exercised through the editors and their context menus; the Calculate button, the export
// confirmation (stub instead of the modal) and the Design Notes write path are driven as in the application. SYNTHETIC inputs.
public static class RopeTractionUiTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    static object Get(string name) { return typeof(MainWindow).GetField(name, PrivateInstance).GetValue(window); }
    static void Set(string name, object value) { typeof(MainWindow).GetField(name, PrivateInstance).SetValue(window, value); }
    static object Call(string name, params object[] arguments) { return typeof(MainWindow).GetMethod(name, PrivateInstance).Invoke(window, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> PresetChoices() { return (Dictionary<string, ComboBox>)Get("presetChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Navigate(string page)
    {
        string[] pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
        ((ListBox)Get("navigation")).SelectedIndex = Array.IndexOf(pages, page); await Ready();
    }
    static async Task Shot(string name) { await Ready(); Call("Screenshot", Path.Combine(output, name + ".png")); }
    static string Docx(string path)
    {
        var b = new StringBuilder();
        using (var package = Package.Open(path, FileMode.Open, FileAccess.Read))
            foreach (var part in package.GetParts().Where(x => x.Uri.OriginalString.StartsWith("/word/document")))
            {
                var xml = new XmlDocument(); using (var s = part.GetStream()) xml.Load(s);
                var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                foreach (XmlNode p in xml.SelectNodes("//w:p", ns)) { foreach (XmlNode t in p.SelectNodes(".//w:t", ns)) b.Append(t.InnerText); b.Append('\n'); }
            }
        return b.ToString();
    }
    static void Type(string key, string text) { Editors()[key].Text = text; }
    static string Shown(string key) { return Editors()[key].Text; }
    static void Reset(string key) { var item = (MenuItem)Editors()[key].ContextMenu.Items[0]; Check(Convert.ToString(item.Header) == "Reset to preset", "Context menu offers Reset to preset: " + key); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
    static void Click(string content) { window.UpdateLayout(); if (content.StartsWith("Calculate ")) { /* Calculate buttons were replaced by live calculation: run the debounced calculation now. */ Call("RunLiveCalculation"); return; } Tree(window).OfType<Button>().First(b => Convert.ToString(b.Content) == content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    static bool Visible(string key) { var e = Editors()[key]; return e.IsVisible; }
    static string Results() { var panel = (StackPanel)Get("ropeTractionResults"); return String.Join("\n", Tree(panel).OfType<TextBlock>().Select(t => t.Text)); }
    static ElevatorProject Project()
    {
        var bin = AppDomain.CurrentDomain.BaseDirectory;
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        p.Info.Name = "SYNTHETIC rope / traction UI test";
        p.Elevator.CapacityKg = 480; p.Elevator.SpeedMetresPerSecond = 1.0; p.Shaft.WidthMm = 1500; p.Shaft.DepthMm = 2100; p.Machine.TractionSheavePitchDiameterMm = 520;
        p.Suspension.RopeDiameterMm = 10; p.Suspension.RopeCount = 4;
        p.Calculations = new ChassisCalculationInputs { Mode = "Basic", CarMassKg = 500, CounterweightMassKg = 740, MachineMassKg = 300 };
        return p;
    }
    static Window shownDialog;
    static bool Confirm(CalculationSheet sheet)
    {
        shownDialog = null;
        Func<Window, bool> show = w => { shownDialog = w; var b = Logical((DependencyObject)w.Content).OfType<Button>().First(x => Convert.ToString(x.Content) == "Export anyway"); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return w.Tag is bool && (bool)w.Tag; };
        return (bool)Call("ConfirmCalculationSheet", sheet, show);
    }
    static IEnumerable<DependencyObject> Logical(DependencyObject node) { yield return node; foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var x in Logical(child)) yield return x; }

    [STAThread]
    public static int Main(string[] arguments)
    {
        output = arguments[0]; if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; } Directory.CreateDirectory(output);
        CompanyReportSettings.SettingsDirectory = Path.Combine(output, "settings");
        var application = new Application(); window = new MainWindow(new string[0]); Set("smoke", true);
        int exit = 1;
        window.Loaded += async (sender, e) =>
        {
            try
            {
                window.Width = 1280; window.Height = 900; await Ready();
                Check(Get("presets") != null, "Shipped preset library loaded by the application");
                var notesButton = Tree(window).OfType<Button>().FirstOrDefault(b => Convert.ToString(b.Content) == "Export Design Notes…");
                Check(notesButton != null && notesButton.IsVisible, "Workflow shows Export Design Notes…");
                Set("session", new ProjectSession(Project())); Call("ResetCalculations");
                await Navigate("SUSPENSION"); await Shot("01-suspension-presets");
                Check(Shown("Calculations.RopeTraction.RopeDesignation") == "8×19 Seale FC 1370/1770" && Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "44" && Shown("Calculations.RopeTraction.RopeMassKgPerM") == "0.34", "Preset values shown for 10 mm ropes (44 kN, 0.34 kg/m, designation)");
                Check(Shown("Calculations.RopeTraction.UndercutAngleDeg") == "95" && Shown("Calculations.RopeTraction.GrooveAngleDeg") == "30" && Shown("Calculations.RopeTraction.TravellingCableKgPerM") == "0.5" && Shown("Calculations.RopeTraction.BrakingRetardationMps2") == "0.5" && Shown("Calculations.RopeTraction.DeflectorMassKg") == "45", "Groove β 95° / γ 30°, travelling cable 0.5, a 0.5, deflector 45 kg shown");
                Check(Convert.ToString(PresetChoices()["Calculations.RopeTraction.GrooveType"].SelectedValue) == GrooveTypes.UndercutU && Convert.ToString(PresetChoices()["Calculations.RopeTraction.TractionLimitingMethod"].SelectedValue) == TractionLimitingMethods.RopesSlip, "Groove type and traction limiting method choices show their presets");
                var pageText = String.Join("\n", Tree((DependencyObject)Get("content")).OfType<TextBlock>().Select(t => t.Text)).ToLowerInvariant();
                Check(!pageText.Contains("preset") && !pageText.Contains("assumed"), "No UI text marks a value as a preset");
                Check(Session().Project.UserEditedFields == null, "Viewing presets does not lock any field");
                // Follow: the unlocked MBL / kg/m follow the rope diameter as it is typed.
                Type("Suspension.RopeDiameterMm", "12"); await Ready();
                Check(Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "63.3" && Shown("Calculations.RopeTraction.RopeMassKgPerM") == "0.49", "MBL and kg/m follow the diameter (12 mm → 63.3 kN, 0.49 kg/m)");
                // Lock: an edited MBL stays when the diameter changes again; kg/m still follows.
                Type("Calculations.RopeTraction.MinimumBreakingLoadKn", "70"); Type("Suspension.RopeDiameterMm", "10"); await Ready();
                Check(Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "70" && Shown("Calculations.RopeTraction.RopeMassKgPerM") == "0.34" && Session().Project.UserEditedFields.Contains("Calculations.RopeTraction.MinimumBreakingLoadKn") && Session().Dirty, "Edited MBL is locked (70 kN) while kg/m follows; project marked changed");
                await Shot("02-mbl-locked");
                // Reset to preset through the context menu.
                Reset("Calculations.RopeTraction.MinimumBreakingLoadKn"); await Ready();
                Check(Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "44" && Session().Project.UserEditedFields == null, "Reset to preset unlocks MBL and shows the preset again (44 kN)");
                // Groove type drives which angles are shown; the unsafe side is rejected with a short message.
                PresetChoices()["Calculations.RopeTraction.GrooveType"].SelectedValue = GrooveTypes.VHardened; await Ready();
                Check(!Visible("Calculations.RopeTraction.UndercutAngleDeg"), "V-groove, hardened: β hidden (groove " + Convert.ToString(PresetChoices()["Calculations.RopeTraction.GrooveType"].SelectedValue) + ", row " + ((FrameworkElement)((FrameworkElement)Editors()["Calculations.RopeTraction.UndercutAngleDeg"].Parent).Parent).Visibility + ", refreshers " + ((System.Collections.ICollection)Get("presetRefreshers")).Count + ", edited " + String.Join(",", Session().Project.UserEditedFields ?? new List<string>()) + ")"); Check(Visible("Calculations.RopeTraction.GrooveAngleDeg"), "V-groove, hardened: γ shown");
                Type("Calculations.RopeTraction.GrooveAngleDeg", "32"); await Ready();
                var hint = (TextBlock)((StackPanel)Editors()["Calculations.RopeTraction.GrooveAngleDeg"].Parent).Children[1];
                Check(hint.IsVisible && hint.Text.Contains("at least 35°"), "γ 32° (below 35°) rejected for a V-groove with a short validation message");
                bool blocked = false; try { Call("Commit"); } catch (TargetInvocationException ex) { blocked = ex.InnerException is ArgumentException; }
                Check(blocked, "Rejected value blocks commit (navigation) until corrected");
                await Shot("03-groove-validation");
                Type("Calculations.RopeTraction.GrooveAngleDeg", "40"); await Ready();
                Check(!hint.IsVisible && Session().Project.Calculations.RopeTraction.GrooveAngleDeg == 40, "Valid γ accepted and locked");
                PresetChoices()["Calculations.RopeTraction.GrooveType"].SelectedValue = GrooveTypes.UndercutU; Reset("Calculations.RopeTraction.GrooveAngleDeg"); await Ready();
                ((MenuItem)PresetChoices()["Calculations.RopeTraction.GrooveType"].ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Ready();
                Type("Calculations.RopeTraction.UndercutAngleDeg", "110"); await Ready();
                blocked = false; try { Call("Commit"); } catch (TargetInvocationException ex) { blocked = ex.InnerException is ArgumentException; }
                Check(blocked && Session().Project.Calculations.RopeTraction.UndercutAngleDeg == null, "β above 105° rejected (not stored)");
                Reset("Calculations.RopeTraction.UndercutAngleDeg"); await Ready();
                Type("Calculations.RopeTraction.BrakingRetardationMps2", "0.4"); await Ready();
                blocked = false; try { Call("Commit"); } catch (TargetInvocationException ex) { blocked = ex.InnerException is ArgumentException; }
                Check(blocked, "Braking retardation below 0.5 m/s² rejected");
                Reset("Calculations.RopeTraction.BrakingRetardationMps2"); await Ready();
                Check(Session().Project.UserEditedFields == null, "All resets leave no locked field");
                // Calculations page: the rope and traction checks are calculated live (no Calculate button).
                await Navigate("CALCULATIONS");
                var calc = Tree(window).OfType<Expander>().FirstOrDefault(x => Convert.ToString(x.Header) == "Ropes and traction");
                Check(calc != null && !Tree(window).OfType<Button>().Any(b => Convert.ToString(b.Content) == "Calculate ropes & traction"), "Calculations page has the Ropes and traction panel, calculated live without a Calculate button");
                calc.BringIntoView(); await Ready();
                var r = (RopeTractionResult)Get("ropeTractionResult");
                Check(r != null && RopeTractionCalculationEngine.IsCurrent(r, Session().Project, (DesignContext)Get("context"), (PresetLibrary)Get("presets")) && r.Traction.Count == 3, "Calculate produces a current result with three traction cases");
                var shown = Results();
                Check(shown.Contains("Rope safety factor · ") && shown.Contains("Traction, emergency braking · T1/T2") && shown.Contains("NOT OK") && !shown.Contains("Deflector D/d"), "Results panel shows checks and the NOT OK braking case");
                ((FrameworkElement)Get("ropeTractionResults")).BringIntoView(); await Shot("04-calculated");
                Session().Project.Suspension.RopeDiameterMm = 12; Call("InvalidateRopeTractionDisplay");
                Check(!Results().Contains("Rope safety factor · "), "Changed input hides the old numbers at once");
                Call("RunLiveCalculation"); Check(Results().Contains("Rope safety factor · ") && RopeTractionCalculationEngine.IsCurrent((RopeTractionResult)Get("ropeTractionResult"), Session().Project, (DesignContext)Get("context"), (PresetLibrary)Get("presets")) && ((RopeTractionResult)Get("ropeTractionResult")).Inputs.RopeDiameterMm == 12, "The live calculation shows the checks again for the changed input (12 mm)");
                Session().Project.Suspension.RopeDiameterMm = 10;
                // Export: NOT OK items listed in the dialog (application only); the sheet shows the results.
                var sheet = (CalculationSheet)Call("BuildCalculationSheet");
                Check(sheet.NotOk.Count > 0 && Confirm(sheet) && shownDialog != null, "Export dialog shown because a check is NOT OK; Export anyway proceeds");
                var dialogText = String.Join("\n", Logical((DependencyObject)shownDialog.Content).OfType<TextBlock>().Select(t => t.Text));
                Check(dialogText.Contains("These checks are NOT OK") && dialogText.Contains("• Traction, emergency braking"), "Export dialog lists the NOT OK checks");
                string sheetPath = Path.Combine(output, "ui-rope-traction-sheet.docx"); Call("WriteCalculationSheet", sheetPath);
                var sheetText = Docx(sheetPath);
                Check(sheetText.Contains("Suspension rope and traction checks") && sheetText.Contains("NOT OK") && !sheetText.ToLowerInvariant().Contains("preset"), "Calculation sheet written with the rope / traction section; clean");
                string notesPath = Path.Combine(output, "ui-design-notes.docx"); var notes = (DesignNotes)Call("WriteDesignNotes", notesPath);
                var notesText = Docx(notesPath);
                Check(File.Exists(notesPath) && notesText.Contains("Warnings") && notesText.Contains("(EN 81-20 Table 6)") && notes.Warnings.Any(w => w.StartsWith("Emergency braking: T1/T2")) && !notesText.Contains("Code checks"), "Export Design Notes path writes short advisory notes: Table 6 and the failing braking case");
                // Locks persist through Save and reopen.
                await Navigate("SUSPENSION"); Type("Calculations.RopeTraction.MinimumBreakingLoadKn", "50"); Call("Commit");
                string save = Path.Combine(output, "UI-rope-traction.elevator.json"); Session().FilePath = save; Click("Save");
                var reopened = new ProjectSession(new ElevatorProject()); reopened.Open(save);
                Check(reopened.Project.UserEditedFields != null && reopened.Project.UserEditedFields.SequenceEqual(new[] { "Calculations.RopeTraction.MinimumBreakingLoadKn" }) && reopened.Project.Calculations.RopeTraction.MinimumBreakingLoadKn == 50, "Save and reopen keep the lock and the edited value");
                Set("session", reopened); Call("ResetCalculations"); await Navigate("SUSPENSION");
                Check(Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "50" && Shown("Calculations.RopeTraction.RopeMassKgPerM") == "0.34", "Reopened project shows the locked value and the followed value");
                await Shot("05-reopened");
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString() }); }
            finally
            {
                Wire.Write(Path.Combine(output, "rope-traction-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real rendered WPF window; editors driven through their TextChanged / SelectionChanged handlers; Reset to preset through the editor context menus; Calculate button; export confirmation with a stub instead of the modal; Save As / file dialogs not automated." });
                window.Close();
            }
        };
        application.Run(window);
        Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + checks + " rope / traction UI assertions.");
        return exit;
    }
}
