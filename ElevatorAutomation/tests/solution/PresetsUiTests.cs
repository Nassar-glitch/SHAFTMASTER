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
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.Suspension;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

// DESIGN-FLOW stage 2 in the real rendered WPF window: a new project opens complete; every calculation, the calculation
// sheet and the Design Notes come straight from it; managed fields lock on edit, follow while unlocked and Reset; older
// projects open unchanged and "Fill blanks from presets" fills them; a visual-tree scan finds no removed hint or remark.
public static class PresetsUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    static readonly string[] Pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
    // Hints, notes, labels and remarks removed from the UI in this stage (distinctive parts; case-sensitive).
    public static readonly string[] Removed =
    {
        "PROVISIONAL", "NOT YET EVALUABLE", "NOT PROVIDED", "Not provided", "blank = ", "Blank = ", "Blank uses", "Blank reads", "Blank retains",
        "Required for section beam selection", "Optional · speed verification unavailable", "Accepted topology is 1:1", "No native variant selector",
        "Native machine retained", "enter only a supplied project requirement", "project input pending calculation sheet", "Enter 0 deliberately",
        "Excludes rated capacity", "metadata", "MR-DS-001", "(project rule", "Presentation only", "Preliminary", "certification", "adopted note",
        "Approved interpretation", "select explicitly", "explicitly confirmed", "not supported in this phase", "source / assumption", "source / exclusion",
        "read only", "READ ONLY", "Source SHA-256", "engineering review", "review required", "REVIEW_REQUIRED", "ASSUMED", "Assume equal",
        "Native car T70", "1:1 native topology", "Existing choices", "when supplied", "Optional ·", "Free text, e.g.", "Reused", "previous numbers are hidden",
        "old values are hidden", "physical confirmation", "Separate ideal static", "Enter project inputs once", "Native car and door modules",
        "Deflector selection remains", "Rail ends, joints", "Edit names, intervals", "Insertion adds an interval", "One coordinated DWG",
        "Newly imported views", "Export the current project as STEP", "Generation success", "No current validation result", "Independent engineering",
        "Simplified static vertical distribution", "Include compensation in the effective", "No member self-weight", "This is not a measured",
        "Capacity is the existing General input", "CG offsets relative to", "Explicitly select two different", "Hanger ΔX / ΔY from", "If included:",
        "Static rated load only", "P5: safety-gear load per car guide", "Counts require explicit", "Irregular load distribution inside the car",
        "Geometry source:", "No guide-rail strength", "This event model does not depend", "Unavailable in current geometry", "Saved choice outside this list",
        "explicit decision required", "mass and CG required", "record reason below", "Assumed machine load", "Machine outline is a geometric",
        "upper-to-lower", "h is vertical shoe spacing", "Calibration status", "Reference car elevation", "Bracket source", "extension (mm)",
        "Optional contribution when included", "compensation included here", "Optional · printed", "Elevator system", "Review independent", "Engineering reviews", "remain independent"
    };
    // The window state through its internal test-automation facade (TestAutomation, ARCHITECTURE-PLAN P4).
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> PitChoices() { return (Dictionary<string, ComboBox>)Get("safetyPitChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Navigate(string page) { ((ListBox)Get("navigation")).SelectedIndex = Array.IndexOf(Pages, page); await Ready(); }
    static async Task Shot(string name) { await Ready(); Call("Screenshot", Path.Combine(output, name + ".png")); }
    static string Shown(string key) { return Editors()[key].Text; }
    static void Type(string key, string text) { Editors()[key].Text = text; }
    static Button FindButton(string content, string automation = null) { window.UpdateLayout(); return Tree(window).OfType<Button>().FirstOrDefault(b => Convert.ToString(b.Content) == content && b.IsVisible && (automation == null || System.Windows.Automation.AutomationProperties.GetName(b) == automation)); }
    static void Click(string content, string automation = null) { var b = FindButton(content, automation); if (b == null) throw new Exception("Button not found: " + content + " " + automation); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    static void Reset(string key) { var item = (MenuItem)Editors()[key].ContextMenu.Items[0]; Check(Convert.ToString(item.Header) == "Reset to preset", "Context menu offers Reset to preset: " + key); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
    static ElevatorProject NewProjectFromWindow() { return (ElevatorProject)Call("NewProject"); }
    // CW the preset gives now: P + q·Q, with the selected balance factor (BalanceFactorSteps) or the library q 0.5.
    static string CwNow(double p, double q) { var rt = Session().Project.Calculations.RopeTraction; double f = rt != null && rt.SelectedBalanceFactor.HasValue ? rt.SelectedBalanceFactor.Value : 0.5; return (p + f * q).ToString(System.Globalization.CultureInfo.InvariantCulture); }
    static void Live() { Call("RunLiveCalculation"); window.UpdateLayout(); }
    static readonly System.Text.RegularExpressions.Regex Code = new System.Text.RegularExpressions.Regex(@"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b");
    static string Canon(string json) { return Canonical(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(json)); }
    static string Canonical(object v)
    {
        var js = new System.Web.Script.Serialization.JavaScriptSerializer(); var map = v as IDictionary<string, object>;
        if (map != null) return "{" + String.Join(",", map.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => js.Serialize(k) + ":" + Canonical(map[k]))) + "}";
        var list = v as System.Collections.IEnumerable; if (list != null && !(v is string)) return "[" + String.Join(",", list.Cast<object>().Select(Canonical)) + "]";
        return js.Serialize(v);
    }
    static string ResultsText() { return String.Join("\n", new[] { "calculationResults", "ropeTractionResults", "safetyPitResults", "carGuideForceResults" }.Select(n => Get(n) as DependencyObject).Where(d => d != null).SelectMany(d => Tree(d).OfType<TextBlock>().Select(t => t.Text))); }
    static void Choose(ComboBox box, string id) { int i = 0; foreach (var item in box.Items) { if (Convert.ToString(item.GetType().GetProperty("Id").GetValue(item, null)) == id) { box.SelectedIndex = i; return; } i++; } throw new Exception("Choice not available: " + id); }
    static async Task ExpandAll() { foreach (var e in Tree(window).OfType<Expander>().ToList()) e.IsExpanded = true; await Ready(); foreach (var e in Tree(window).OfType<Expander>().ToList()) e.IsExpanded = true; await Ready(); }
    // Every visible string of the page: text blocks, text boxes, combo items, expander headers, button contents.
    static List<string> VisibleStrings()
    {
        var list = new List<string>();
        foreach (var n in Tree(window))
        {
            var tb = n as TextBlock; if (tb != null) list.Add(tb.Text);
            var box = n as TextBox; if (box != null) list.Add(box.Text);
            var combo = n as ComboBox; if (combo != null) foreach (var item in combo.Items) { var p = item.GetType().GetProperty("Name"); list.Add(p == null ? Convert.ToString(item) : Convert.ToString(p.GetValue(item, null))); }
            var ex = n as Expander; if (ex != null) list.Add(Convert.ToString(ex.Header));
            var b = n as Button; if (b != null) list.Add(Convert.ToString(b.Content));
        }
        return list.Where(s => !String.IsNullOrEmpty(s)).ToList();
    }
    static readonly List<object> scanLog = new List<object>();
    static async Task Scan(string label)
    {
        var found = new List<string>(); int strings = 0;
        foreach (var page in Pages)
        {
            await Navigate(page); if (page == "CALCULATIONS") await ExpandAll();
            var texts = VisibleStrings(); strings += texts.Count;
            foreach (var t in texts) foreach (var r in Removed) if (t.Contains(r)) found.Add(page + ": '" + r + "' in '" + (t.Length > 120 ? t.Substring(0, 120) + "…" : t) + "'");
            if (texts.Any(t => t.Trim() == "Required")) found.Add(page + ": hint 'Required'");
            foreach (var t in texts) foreach (System.Text.RegularExpressions.Match m in Code.Matches(t)) found.Add(page + ": status code '" + m.Value + "' in '" + (t.Length > 120 ? t.Substring(0, 120) + "…" : t) + "'");
        }
        scanLog.Add(new { Label = label, Strings = strings, Found = found });
        Check(found.Count == 0, "Visual-tree scan (" + label + "): " + strings + " strings on 11 pages incl. the status bar, no removed hint, remark or UPPER_SNAKE_CASE status code" + (found.Count > 0 ? " — " + String.Join(" | ", found.Take(6)) : ""));
    }
    static string Docx(string path)
    {
        var b = new StringBuilder();
        using (var package = Package.Open(path, FileMode.Open, FileAccess.Read))
            foreach (var part in package.GetParts().Where(x => x.Uri.OriginalString.StartsWith("/word/document") || x.Uri.OriginalString.StartsWith("/word/header") || x.Uri.OriginalString.StartsWith("/word/footer")))
            {
                var xml = new XmlDocument(); using (var s = part.GetStream()) xml.Load(s);
                var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                foreach (XmlNode p in xml.SelectNodes("//w:p", ns)) { foreach (XmlNode t in p.SelectNodes(".//w:t", ns)) b.Append(t.InnerText); b.Append('\n'); }
            }
        return b.ToString();
    }
    static readonly string[] Forbidden = { "preset", "Preset", "assumed", "Assumed", "PROVISIONAL", "NOT YET EVALUABLE", "NOT PROVIDED", "MR-DS-001", "project rule", "preliminary", "Preliminary", "not from catalog", "review", "Review" };
    static void Clean(string path, string label)
    {
        var text = Docx(path); var hits = Forbidden.Where(f => text.Contains(f)).ToList();
        Check(hits.Count == 0, label + " contains no forbidden string" + (hits.Count > 0 ? ": " + String.Join(", ", hits) : ""));
    }

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
                var bin = AppDomain.CurrentDomain.BaseDirectory;
                // ---------- new project, as the application opens it ----------
                Check(Get("presets") != null && Session().Project.FollowsPresets == true && Session().Project.UserEditedFields == null, "The application opens a new project that follows the presets, nothing locked");
                await Navigate("GENERAL"); await Shot("01-new-general");
                Check(Shown("Elevator.CapacityKg") == "630" && Shown("Elevator.SpeedMetresPerSecond") == "1", "General: Q 630 kg, v 1 m/s");
                Check(((TextBlock)Get("personsLabel")).Text == "8 persons" && ((TextBlock)Get("personsLabel")).IsVisible, "Persons shown next to Q: 8 persons");
                Check(FindButton("Fill blanks from presets", "Fill blanks from presets (GENERAL)") == null, "No page Fill blanks action on a complete new project");
                await Navigate("SHAFT"); await Shot("01b-new-shaft");
                Check(Shown("Shaft.WidthMm") == "1500" && Shown("Shaft.DepthMm") == "1800" && Shown("Shaft.PitDepthMm") == "1300" && Shown("Shaft.OverheadMm") == "4200", "Shaft: 1500 × 1800, pit 1300, overhead 4200");
                Check(Tree(window).OfType<TextBlock>().Any(t => t.Text.StartsWith("Car: ") && t.Text.Contains("TZ: 850")), "Calculated geometry shown at once (no Validate needed): car size, TZ 850");
                await Navigate("FLOORS"); await Shot("01c-new-floors");
                var rows = (List<FloorRow>)Get("floorRows");
                Check(rows.Count == 5 && rows.Take(4).All(r => r.Height == "3000") && rows[4].Height == "" && rows.Select(r => r.Name).SequenceEqual(new[] { "G", "1", "2", "3", "4" }), "Floors: 5 stops G + 4, intervals 3000 mm");
                var interval = Tree(window).OfType<TextBox>().First(t => Convert.ToString(t.ToolTip) == "Added interval in mm");
                Check(interval.Text == "3000", "A new floor's interval defaults to 3000 mm");
                var grid = (DataGrid)Get("floorGrid"); grid.SelectedItem = rows[4]; Tree(window).OfType<TextBox>().First(t => Convert.ToString(t.ToolTip) == "New floor name").Text = "5"; Click("Add before selected"); await Ready();
                rows = (List<FloorRow>)Get("floorRows");
                Check(rows.Count == 6 && Session().Project.Building.Floors[4].Name == "5" && Session().Project.Building.Floors[3].HeightToNextMm == 3000 && Session().Project.Building.Floors[4].HeightToNextMm == 3000, "Adding a floor with the default interval adds 3000 mm");
                await Navigate("MACHINE"); Check(Shown("Machine.TractionSheavePitchDiameterMm") == "520", "Machine: sheave pitch diameter 520 mm");
                await Navigate("SUSPENSION"); await Shot("03-new-suspension");
                var np0 = Session().Project; var rope0 = new RopeTractionCalculationEngine().Calculate(np0, (DesignContext)Get("context"), (PresetLibrary)Get("presets"));
                Check(Shown("Suspension.RopeDiameterMm") == Convert.ToString(np0.Suspension.RopeDiameterMm) && Shown("Suspension.RopeCount") == Convert.ToString(np0.Suspension.RopeCount) && np0.Suspension.RopeCount == 4 && np0.Suspension.RopeDiameterMm == 11 && Shown("Calculations.RopeTraction.MinimumBreakingLoadKn") == "53.2" && Shown("Calculations.RopeTraction.UndercutAngleDeg") == "100" && ProjectPresets.Passes(rope0), "Suspension: ropes 4 × 11 mm (ladder diameter, MBL 53.2 kN) and β 100° selected; every rope and traction check OK");
                await Navigate("RAILS"); Check(Shown("Rails.BracketMaximumSpacingMm") == "1500", "Rails: bracket spacing 1500 mm");
                await Navigate("CALCULATIONS"); await Shot("02-new-calculations");
                Check(Shown("Calculations.CarMassKg") == "700" && Shown("Calculations.CounterweightMassKg") == "1015" && Shown("Calculations.MachineMassKg") == "350" && Shown("Calculations.CarGuideForces.GuideShoeSpacingMm") == "2800", "Calculations: P 700, CW 1015, machine 350, h 2800");
                Check(Convert.ToString(PitChoices()["SafetyGearType"].SelectedValue) == "Progressive" && Convert.ToString(PitChoices()["BufferType"].SelectedValue) == "EnergyAccumulation" && Convert.ToString(PitChoices()["CarGuideCount"].SelectedValue) == "2" && Convert.ToString(PitChoices()["CarBufferCount"].SelectedValue) == "1" && Convert.ToString(PitChoices()["CounterweightBufferCount"].SelectedValue) == "1", "Pit choices: progressive, energy accumulation, 2 guide rails, 1 / 1 buffers");
                // Every calculation is available on the page with no button press (live calculation).
                await ExpandAll();
                Check(!Tree(window).OfType<Button>().Any(b => Convert.ToString(b.Content).StartsWith("Calculate ")), "No Calculate buttons on the Calculations page");
                var basic = ((CalculationWorkspace)Get("calculations")).Result;
                Check(basic != null && basic.Reactions != null && ResultsText().Contains("Basic · Calculated") && ResultsText().Contains("Derived distances"), "P1–P4 shown at once with no button press");
                var rope = (RopeTractionResult)Get("ropeTractionResult");
                Check(rope != null && rope.HasResults && rope.Traction.Count == 3 && ResultsText().Contains("Rope safety factor · ") && !ResultsText().Contains("NOT OK"), "Rope and traction checks shown at once, all OK");
                var pit = (SafetyPitCalculationResult)Get("safetyPitResult");
                Check(pit != null && pit.P5.PerComponentLoadN.HasValue && pit.P6.PerComponentLoadN.HasValue && pit.P7.PerComponentLoadN.HasValue && ResultsText().Contains("per car guide rail"), "P5–P7 shown at once");
                var ff = (CarGuideForceResult)Get("carGuideForceResult");
                Check(ff != null && ff.FF1DesignN.HasValue && ff.FF2DesignN.HasValue && ResultsText().Contains("FF1 · Base"), "FF1 / FF2 shown at once");
                var table = Tree((DependencyObject)Get("calculationResults")).OfType<Grid>().FirstOrDefault(g => g.Children.OfType<Border>().Any(b => b.Child is TextBlock && ((TextBlock)b.Child).Text == "Distances (mm)"));
                Check(table != null && table.Children.OfType<Border>().Select(b => ((TextBlock)b.Child).Text).Skip(4).Where((t, k) => k % 4 != 0).All(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^-?\d+(\.\d)?$")), "Basic P1–P4 lever arms shown as a table in mm with 0–1 decimals");
                ((ScrollViewer)Get("pageScroll")).ScrollToVerticalOffset(700); await Shot("02b-new-calculations-results");
                // Sheet and Design Notes exported straight from the new project (export paths used by the toolbar actions).
                Call("Commit");
                var sheetPath = Path.Combine(output, "new-project-calculation-sheet.docx"); var sheet = (CalculationSheet)Call("WriteCalculationSheet", sheetPath);
                var sheetText = Docx(sheetPath);
                Check(File.Exists(sheetPath) && sheet.HasContent && sheet.Omitted.Count == 0, "Calculation sheet exported from the new project with no omitted section");
                Check(sheetText.Contains("P1–P4") && sheetText.Contains("P5") && sheetText.Contains("FF1") && sheetText.Contains("Suspension rope and traction checks") && sheetText.Contains("700") && sheetText.Contains("1015"), "Sheet holds P1–P4, P5–P7, FF and the rope / traction section with P 700 / CW 1015");
                Check(sheet.NotOk.Count == 0, "Sheet from the new project has no NOT OK item" + (sheet.NotOk.Count > 0 ? ": " + String.Join(" | ", sheet.NotOk) : ""));
                Clean(sheetPath, "Calculation sheet");
                var notesPath = Path.Combine(output, "new-project-design-notes.docx"); var notes = (DesignNotes)Call("WriteDesignNotes", notesPath);
                var notesText = Docx(notesPath);
                Check(File.Exists(notesPath) && notes.Warnings.Count == 0 && notes.Recommendations.Count == 0 && notes.Notes.Count == 1 && notes.Notes[0] == "Deflector pitch diameter 400 mm gives D/d = 36.36 for 11 mm ropes; EN 81-20 5.5.2.1 requires D/d ≥ 40 for pulleys (≥ 440 mm). The smaller deflector is accounted for in the rope safety factor (EN 81-50 5.12)." && notesText.Contains("Notes"), "Design Notes of the new project: no warning or recommendation; only the deflector D/d quotation (DESIGN-FLOW 11a)" + (notes.Notes.Count == 1 ? "" : " (" + String.Join(" | ", notes.Warnings.Concat(notes.Recommendations).Concat(notes.Notes)) + ")"));
                Check(!ResultsText().Contains("Deflector D/d") && !Docx(sheetPath).Contains("Deflector D/d") && Docx(sheetPath).Contains("Deflector sheave pitch diameter"), "No deflector D/d row in the app or the sheet; the sheet shows the deflector pitch diameter as a value");
                Clean(notesPath, "Design Notes");
                Check(!notesText.Contains("MR-") && !notes.Warnings.Concat(notes.Recommendations).Any(t => t.Contains("rule MR")), "Design Notes carry no internal rule ID");
                await Scan("new project, Basic");
                // Advanced mode on the same new project: presets fill everything except the member pair.
                await Navigate("CALCULATIONS"); var mode = ((Dictionary<string, ComboBox>)Get("calculationChoices"))["Mode"]; Choose(mode, "Advanced"); await Ready(); await ExpandAll(); await Shot("02c-new-calculations-advanced");
                var c = Session().Project.Calculations;
                Check(c.CarSideAdditionalSuspendedMassKg == 0 && c.CounterweightSideAdditionalSuspendedMassKg == 0 && Shown("Calculations.MachineCgOffsetXMm") == "0" && Shown("Calculations.HangerAttachmentOffsetXMm") == "0" && !Editors().ContainsKey("Calculations.MachineCgSource") && !Editors().ContainsKey("Calculations.TransferAssumptionSource"), "Advanced: additions 0, CG / hanger offsets 0; basis texts hidden from the UI (mode " + c.Mode + ", car add " + c.CarSideAdditionalSuspendedMassKg + ", cw add " + c.CounterweightSideAdditionalSuspendedMassKg + ", cg '" + (Editors().ContainsKey("Calculations.MachineCgOffsetXMm") ? Shown("Calculations.MachineCgOffsetXMm") : "none") + "', hanger '" + (Editors().ContainsKey("Calculations.HangerAttachmentOffsetXMm") ? Shown("Calculations.HangerAttachmentOffsetXMm") : "none") + "')");
                var adv = new List<string>(); foreach (var t in VisibleStrings()) foreach (var r in Removed) if (t.Contains(r)) adv.Add(r);
                Check(adv.Count == 0, "Visual-tree scan (new project, Advanced page): no removed hint or remark" + (adv.Count > 0 ? ": " + String.Join(", ", adv.Distinct()) : ""));
                mode = ((Dictionary<string, ComboBox>)Get("calculationChoices"))["Mode"]; Choose(mode, "Basic"); await Ready();

                // ---------- lock / follow / Reset ----------
                await Navigate("GENERAL"); Type("Elevator.CapacityKg", "1000"); await Ready();
                Check(((TextBlock)Get("personsLabel")).Text == "13 persons", "Persons follow Q as it is typed (1000 → 13)");
                await Navigate("CALCULATIONS");
                Check(Shown("Calculations.CarMassKg") == "1100" && Shown("Calculations.MachineMassKg") == "450" && Shown("Calculations.CounterweightMassKg") == CwNow(1100, 1000), "Q 1000: unlocked P 1100, machine 450 and CW = P + q·Q (" + Shown("Calculations.CounterweightMassKg") + ") follow");
                Check(ResultsText().Contains("Rope safety factor · ") && !ResultsText().Contains("NOT OK") && !ResultsText().Contains("STALE") && !ResultsText().Contains("Not calculated"), "Q 1000 results recalculated live: every rope / traction check OK, no stale state");
                await Navigate("SUSPENSION"); Check(Shown("Suspension.RopeCount") == Convert.ToString(Session().Project.Suspension.RopeCount) && Shown("Suspension.RopeDiameterMm") == Convert.ToString(Session().Project.Suspension.RopeDiameterMm) && ProjectPresets.Passes(new RopeTractionCalculationEngine().Calculate(Session().Project, (DesignContext)Get("context"), (PresetLibrary)Get("presets"))), "Q 1000: the selection shown on Suspension (" + Session().Project.Suspension.RopeCount + " × " + Session().Project.Suspension.RopeDiameterMm + " mm) passes every check");
                await Navigate("CALCULATIONS"); Type("Calculations.CarMassKg", "700"); await Ready();
                Check(Shown("Calculations.CounterweightMassKg") == CwNow(700, 1000) && Session().Project.UserEditedFields.Contains("Calculations.CarMassKg"), "Typing P 700 locks P; CW follows at once (700 + q·1000 = " + Shown("Calculations.CounterweightMassKg") + ")");
                await Navigate("GENERAL"); Type("Elevator.CapacityKg", "800"); await Navigate("CALCULATIONS");
                Check(Shown("Calculations.CarMassKg") == "700" && Shown("Calculations.MachineMassKg") == "400" && Shown("Calculations.CounterweightMassKg") == CwNow(700, 800), "Q 800: locked P stays 700; machine 400 and CW = 700 + q·800 (" + Shown("Calculations.CounterweightMassKg") + ") follow");
                Reset("Calculations.CarMassKg"); await Ready();
                Check(Shown("Calculations.CarMassKg") == "850" && Shown("Calculations.CounterweightMassKg") == CwNow(850, 800) && !Session().Project.UserEditedFields.Contains("Calculations.CarMassKg"), "Reset to preset: P follows again (850) and CW with it (" + Shown("Calculations.CounterweightMassKg") + ")");
                await Shot("04-lock-follow-reset");
                Choose(PitChoices()["SafetyGearType"], "Instantaneous"); await Ready();
                Check(Session().Project.UserEditedFields.Contains("Calculations.SafetyPit.SafetyGearType") && Session().Project.Calculations.SafetyPit.SafetyGearType == "Instantaneous", "A pit choice the user changes is locked");
                string saved = Path.Combine(output, "presets-ui-saved.elevator.json"); Session().FilePath = saved; Click("Save");
                var reopened = new ProjectSession(new ElevatorProject()); reopened.Open(saved);
                Check(reopened.Project.FollowsPresets == true && reopened.Project.UserEditedFields.OrderBy(k => k).SequenceEqual(new[] { "Calculations.SafetyPit.SafetyGearType", "Elevator.CapacityKg" }) && reopened.Project.Elevator.CapacityKg == 800 && reopened.Project.Calculations.CarMassKg == 850, "Save and reopen keep FollowsPresets, the locks and the values");

                // ---------- older project: opens unchanged, Fill blanks ----------
                var oldPath = Path.Combine(output, "old-project.elevator.json"); File.Copy(Path.Combine(bin, "reviewed-example.elevator.json"), oldPath);
                var old = new ProjectSession(new ElevatorProject()); old.Open(oldPath); var oldJson = ProjectJson.Serialize(old.Project);
                Set("session", old); Call("ResetCalculations"); Call("ApplyProjectPresets", null, true); Call("Commit");
                Check(Canon(ProjectJson.Serialize(Session().Project)) == Canon(oldJson) && Session().Project.FollowsPresets == null, "Older project opens unchanged: applying presets and Commit write nothing (no flag, no value)");
                await Navigate("SHAFT"); await Navigate("GENERAL");
                Func<string> values = () => Wire.Json(ProjectPresets.Fields.Select(f => f.Key + "=" + Convert.ToString(f.Stored(Session().Project), System.Globalization.CultureInfo.InvariantCulture)).ToArray()) + Wire.Json(Session().Project.Building) + Wire.Json(Session().Project.UserEditedFields);
                var before = values();
                await Scan("older project");
                Check(values() == before && Session().Project.FollowsPresets == null && Session().Project.Elevator.SpeedMetresPerSecond == null && Session().Project.Calculations.CarMassKg == null, "Viewing every page of the older project changes no project value; blanks stay blank");
                await Navigate("GENERAL"); await Shot("05-old-general-fill-action");
                Check(Shown("Elevator.CapacityKg") == "450" && Shown("Elevator.SpeedMetresPerSecond") == "" && FindButton("Fill blanks from presets", "Fill blanks from presets (GENERAL)") != null, "Older project: Q 450 kept, blank speed stays blank; page action offered");
                Click("Fill blanks from presets", "Fill blanks from presets (GENERAL)"); await Ready();
                Check(Shown("Elevator.SpeedMetresPerSecond") == "1" && Shown("Elevator.CapacityKg") == "450" && Session().Project.FollowsPresets == true && Session().Project.Calculations.CarMassKg == null, "Page Fill blanks: speed 1; Q kept; other pages untouched");
                await Navigate("CALCULATIONS"); Check(Shown("Calculations.CarMassKg") == "", "Calculations blanks stay blank until filled");
                Click("Fill blanks from presets", "Fill blanks from presets (all pages)"); await Ready(); await Navigate("CALCULATIONS");
                Check(Shown("Calculations.CarMassKg") == "531.25" && Shown("Calculations.CounterweightMassKg") == "756.25" && Shown("Calculations.CarGuideForces.GuideShoeSpacingMm") == "2800", "Global Fill blanks: P 531.25, CW 756.25, h 2800 from Q 450");
                Check(Session().Project.Suspension.RopeCount == 6 && Session().Project.Suspension.RopeDiameterMm == 8 && Session().Project.Shaft.WidthMm == 1450, "Global Fill blanks keeps existing values (ropes 6 × 8, SW 1450)");
                await Shot("06-old-after-fill");

                // ---------- live recalculation after edits on each page ----------
                Set("session", new ProjectSession(NewProjectFromWindow())); Call("ResetCalculations"); Call("ShowPage", "CALCULATIONS"); await Ready(); Live();
                var live0 = ((CalculationWorkspace)Get("calculations")).Result; var p1 = live0.Reactions.First(r => r.Id == "P1").UpwardN;
                Func<string, double> reaction = id => ((CalculationWorkspace)Get("calculations")).Result.Reactions.First(r => r.Id == id).UpwardN;
                var edits = new[] { new[] { "GENERAL", "Elevator.CapacityKg", "800" }, new[] { "SHAFT", "Shaft.WidthMm", "1600" }, new[] { "MACHINE", "MachineRoom.MinimumDeflectorSlabClearanceMm", "50" }, new[] { "SUSPENSION", "Calculations.RopeTraction.GrooveAngleDeg", "32" }, new[] { "CALCULATIONS", "Calculations.MachineMassKg", "500" }, new[] { "CALCULATIONS", "Calculations.CarGuideForces.GuideShoeSpacingMm", "3000" } };
                foreach (var ed in edits)
                {
                    await Navigate(ed[0]); Type(ed[1], ed[2]);
                    await Task.Delay(500); window.UpdateLayout(); // the debounced (300 ms) live calculation fires on its own
                    await Navigate("CALCULATIONS"); await ExpandAll();
                    var txt = ResultsText(); var ws = (CalculationWorkspace)Get("calculations");
                    var ctxNow = (DesignContext)Get("context"); var libNow = (PresetLibrary)Get("presets");
                    bool allCurrent = ws.IsCurrent(Session().Project, ctxNow) && RopeTractionCalculationEngine.IsCurrent((RopeTractionResult)Get("ropeTractionResult"), Session().Project, ctxNow, libNow) && SafetyPitCalculationEngine.IsCurrent((SafetyPitCalculationResult)Get("safetyPitResult"), Session().Project) && CarGuideForceCalculationEngine.IsCurrent((CarGuideForceResult)Get("carGuideForceResult"), Session().Project, ctxNow);
                    Check(allCurrent && !txt.Contains("STALE") && !txt.Contains("Not calculated") && !System.Text.RegularExpressions.Regex.IsMatch(txt, @"P\d · [^\n]*\b0 kN\b") && ws.Result.Reactions.All(r => r.UpwardN != 0), "Live recalculation after editing " + ed[1] + " on " + ed[0] + ": every result current, no stale or zero value shown");
                }
                Check(reaction("P1") != p1, "Edits changed the live P1 (" + Math.Round(p1) + " → " + Math.Round(reaction("P1")) + " N)");
                await Navigate("CALCULATIONS"); Type("Calculations.MachineMassKg", "abc"); await Task.Delay(500); window.UpdateLayout();
                var invalidText = ResultsText();
                Check(invalidText.Contains("Basic · Invalid mass input") && !invalidText.Contains("Basic · Calculated") && !invalidText.Contains("Derived distances") && ((CalculationWorkspace)Get("calculations")).Result == null, "Invalid machine mass: short message, P1–P4 section shows nothing until fixed");
                Type("Calculations.MachineMassKg", "500"); await Task.Delay(500); window.UpdateLayout();
                Check(ResultsText().Contains("Basic · Calculated") && ((CalculationWorkspace)Get("calculations")).Result != null, "Fixing the input recalculates at once");
                await Scan("after live edits");
                File.WriteAllText(Path.Combine(output, "old-project-after-fill.elevator.json"), ProjectJson.Serialize(Session().Project));
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString() }); }
            finally
            {
                Wire.Write(Path.Combine(output, "visual-tree-scan.json"), new { Removed = Removed, Runs = scanLog });
                Wire.Write(Path.Combine(output, "presets-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real rendered WPF window; editors and choices driven through their normal handlers; Reset through the editor context menu; toolbar / page Fill blanks buttons clicked; sheet and Design Notes written through the toolbar export paths without the Save As dialog; visual-tree scan of every page." });
                window.Close();
            }
        };
        application.Run(window); return exit;
    }
}
