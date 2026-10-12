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
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Desktop;
using Elevator.Solution.Desktop.Pages;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

// Real rendered WPF window: inputs typed into editors through their normal TextChanged handlers,
// then the export path used by "Export Calculation Sheet…" (WriteCalculationSheet) is invoked.
// The Save As dialog itself is not automated. SYNTHETIC inputs only.
public static class CalculationSheetUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    // The window state through its internal test-automation facade (TestAutomation, ARCHITECTURE-PLAN P4).
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> Choices() { return (Dictionary<string, ComboBox>)Get("calculationChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Navigate(string page)
    {
        string[] pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
        ((ListBox)Get("navigation")).SelectedIndex = Array.IndexOf(pages, page); await Ready();
    }
    static string Text(string path)
    {
        var b = new StringBuilder();
        using (var package = Package.Open(path, FileMode.Open, FileAccess.Read))
            foreach (var part in package.GetParts().Where(x => x.Uri.OriginalString.StartsWith("/word/document") || x.Uri.OriginalString.StartsWith("/word/header")))
            {
                var xml = new XmlDocument(); using (var s = part.GetStream()) xml.Load(s);
                var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                foreach (XmlNode p in xml.SelectNodes("//w:p", ns)) { foreach (XmlNode t in p.SelectNodes(".//w:t", ns)) b.Append(t.InnerText); b.Append('\n'); }
            }
        return b.ToString();
    }
    // Same removed-text list as CalculationSheetTests (client document must not contain internal/disclaimer text).
    static readonly string[] Removed = {
        "preliminary","engineering review","not certified","certified","certification","compliance","complies","statement of",
        "incomplete","not for issue","unavailable","complete the missing","requested calculations",
        "elevator_calculations_gearbox",".docx","sha-256","ecfd591133f5f97ed923f62a3a3eea60c2b97a67805e60bcb7131c3da7fb13df","paragraph","paraid","439730eb","5429655e","3897ff34",
        "gearboxnote","twostageverticalmachinedatum","releasedtwostagewithrigidspreader","-v1","engine record","result currency","fingerprint",
        "calculationsheet","shaftmaster.exe","adopted","calculated under","user-approved","approved interpretation","interpretation","reference only","synthetic",
        "equal-sharing","equal sharing","not established","rule sources","note-derived","limitations","not included","not provided","invalid text",
        "not covered","excluded","no guide strength","verification","k2","status","used by","unsaved","project file","project identifier","application" };
    static CalculationSheet Export(string file, out string text)
    {
        string path = Path.Combine(output, file); Check(!File.Exists(path), "Fresh output path: " + file);
        bool dirty = Session().Dirty;
        var sheet = (CalculationSheet)Call("WriteCalculationSheet", path);
        Check(File.Exists(path) && Session().Dirty == dirty, "Export writes " + file + " without changing project dirty state");
        text = Text(path);
        string lower = text.ToLowerInvariant();
        var found = Removed.Where(r => lower.Contains(r)).ToList();
        Check(found.Count == 0, "Removed text absent from " + file + (found.Count > 0 ? ": " + String.Join(", ", found) : ""));
        Check(text.Contains(CalculationSheet.DesignBasis) && !text.Split('\n').Where(l => !l.Contains("Δ") && !l.StartsWith("(")).Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"(?<![\d.,])0(\.0+)? (N|kN|kg|mm)\b")), "Design basis present and no zero placeholders: " + file);
        return sheet;
    }
    // Drives the export confirmation with a stub instead of a modal dialog; records the dialog shown.
    static Window shownDialog;
    static bool Confirm(CalculationSheet sheet, bool clickExport)
    {
        shownDialog = null;
        Func<Window, bool> show = w => {
            shownDialog = w;
            var b = LogicalButtons((DependencyObject)w.Content).FirstOrDefault(x => Convert.ToString(x.Content) == (clickExport ? "Export anyway" : "Cancel"));
            if (b != null) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return w.Tag is bool && (bool)w.Tag;
        };
        return (bool)Call("ConfirmCalculationSheet", sheet, show);
    }
    static IEnumerable<Button> LogicalButtons(DependencyObject node)
    {
        var b = node as Button; if (b != null) yield return b;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var x in LogicalButtons(child)) yield return x;
    }
    static string DialogText(Window w) { return String.Join("\n", LogicalText((DependencyObject)w.Content)); }
    static IEnumerable<string> LogicalText(DependencyObject node)
    {
        var t = node as TextBlock; if (t != null) yield return t.Text;
        var b = node as Button; if (b != null) yield return Convert.ToString(b.Content);
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var s in LogicalText(child)) yield return s;
    }
    static void Click(string content)
    {
        window.UpdateLayout();
        if (content.StartsWith("Calculate ")) { /* Calculate buttons were replaced by live calculation: run the debounced calculation now. */ Call("RunLiveCalculation"); return; }
        Tree(window).OfType<Button>().First(b => Convert.ToString(b.Content) == content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    static void Attach(ElevatorProject p) { Set("session", new ProjectSession(p)); Call("ResetCalculations"); Call("ShowPage", "CALCULATIONS"); }
    static bool Near(double? a, double b) { return a.HasValue && Math.Abs(a.Value - b) < 1e-6 * Math.Max(1, Math.Abs(b)); }

    [STAThread]
    public static int Main(string[] arguments)
    {
        output = arguments[0]; if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; } Directory.CreateDirectory(output);
        CompanyReportSettings.SettingsDirectory = Path.Combine(output, "settings"); // never the real %AppData%
        var application = new Application(); window = new MainWindow(new string[0]); Set("smoke", true);
        int exit = 1;
        window.Loaded += async (sender, e) =>
        {
            try
            {
                window.Width = 1280; window.Height = 860; await Ready();
                var button = Tree(window).OfType<Button>().FirstOrDefault(b => Convert.ToString(b.Content) == "Export Calculation Sheet…");
                Check(button != null && button.IsVisible && button.ActualWidth > 0, "Toolbar shows Export Calculation Sheet… action");
                Call("Screenshot", Path.Combine(output, "01-toolbar-export-action.png"));
                // Narrow window: compact label keeps Workflow actions on one row (no extra toolbar height).
                window.Width = 760; window.Height = 640; await Ready(); await Ready();
                var validate = Tree(window).OfType<Button>().First(b => Convert.ToString(b.Content) == "Validate");
                Check(Convert.ToString(button.Content) == "Calc Sheet…" && Convert.ToString(button.ToolTip).StartsWith("Export Calculation Sheet") &&
                      System.Windows.Automation.AutomationProperties.GetName(button) == "Export Calculation Sheet", "Narrow window: compact label, full name kept in tooltip and automation name");
                Check(Math.Abs(button.TransformToAncestor(window).Transform(new Point(0, 0)).Y - validate.TransformToAncestor(window).Transform(new Point(0, 0)).Y) < 1, "Narrow window: export action stays on the Workflow row");
                Call("Screenshot", Path.Combine(output, "01b-toolbar-narrow.png"));
                window.Width = 1280; window.Height = 860; await Ready(); await Ready();
                Check(Convert.ToString(button.Content) == "Export Calculation Sheet…", "Wide window restores full label");

                var bin = AppDomain.CurrentDomain.BaseDirectory;
                var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
                foreach (var f in p.Building.Floors.Take(p.Building.Floors.Count - 1)) if (!f.HeightToNextMm.HasValue) f.HeightToNextMm = 3000;
                p.Info.Name = "UI Test Tower — Lift 1"; p.Elevator.CapacityKg = 480; p.Shaft.WidthMm = 1500; p.Shaft.DepthMm = 2500;
                p.Calculations = new ChassisCalculationInputs { Mode = "Basic" };
                p.Calculations.SafetyPit = new SafetyPitInputs { SafetyGearType = "Progressive", CarGuideCount = 2, CarBufferCount = 1, CounterweightBufferCount = 1 };
                Attach(p); await Ready();
                Editors()["Calculations.CarMassKg"].Text = "500"; Editors()["Calculations.CounterweightMassKg"].Text = "740"; Editors()["Calculations.MachineMassKg"].Text = "300";
                Editors()["Calculations.CarGuideForces.GuideShoeSpacingMm"].Text = "1200"; await Ready();
                string text; var sheet = Export("ui-01-basic-complete.docx", out text);
                Check(sheet.Omitted.Count(o => !o.Name.StartsWith(CalculationSheet.RopeTractionSectionTitle)) == 0 && sheet.Mode == "Basic" && sheet.ChassisAvailable, "Typed Basic inputs export a complete sheet without pressing Calculate (rope / traction checks depend on the deflector geometry)");
                Check(Near(sheet.CarGuideForces.FF1BaseN, 1100) && Near(sheet.CarGuideForces.FF1DesignN, 2200) && Near(sheet.CarGuideForces.FF2BaseN, 1050) && Near(sheet.CarGuideForces.FF2DesignN, 2100), "UI export reproduces FF fixture 1100/2200, 1050/2100 N");
                Check(Near(sheet.SafetyPit.P5.PerComponentLoadN, 9800) && Near(sheet.SafetyPit.P6.GroupLoadN, 39200) && Near(sheet.SafetyPit.P7.GroupLoadN, 29600), "UI export P5/P6/P7 unchanged (9800 / 39200 / 29600 N)");
                if (sheet.Omitted.Count == 0 && sheet.NotOk.Count == 0) Check(Confirm(sheet, false) && shownDialog == null, "Complete sheet: no omission dialog, export proceeds");
                else { Check(Confirm(sheet, true) && shownDialog != null && sheet.Omitted.All(o => o.Name.StartsWith(CalculationSheet.RopeTractionSectionTitle)), "Complete P1–P7 / FF sheet: the dialog lists only rope / traction items (deflector geometry), Export anyway proceeds"); shownDialog = null; }

                // Results shown in the UI, then report fields edited on the General page: nothing goes stale.
                Click("Calculate load distribution"); Click("Calculate P5–P7"); Click("Calculate FF1 / FF2"); await Ready();
                var workspace = (CalculationWorkspace)Get("calculations"); var pitResult = (SafetyPitCalculationResult)Get("safetyPitResult"); var ffResult = (CarGuideForceResult)Get("carGuideForceResult");
                Check(workspace.Result != null && workspace.IsCurrent(Session().Project, (DesignContext)Get("context")) && pitResult != null && ffResult != null && ffResult.FF1DesignN.HasValue, "UI results calculated before editing report fields");
                await Navigate("GENERAL");
                Check(Tree(window).OfType<TextBlock>().Any(t => t.Text == "Calculation sheet details") && Tree(window).OfType<Button>().Any(b => Convert.ToString(b.Content) == "Company / report settings…"), "General page shows Calculation sheet details and Company / report settings…");
                foreach (var key in new[] { "Report.ClientName", "Report.DocumentNumber", "Report.Revision", "Report.PreparedBy", "Report.CheckedBy", "Report.ApprovedBy" }) Check(Editors().ContainsKey(key) && Editors()[key].Text == "", "Blank editor for " + key + " (no default)");
                Editors()["Report.ClientName"].Text = "Delta Developments"; Editors()["Report.DocumentNumber"].Text = "NL-CS-002"; Editors()["Report.Revision"].Text = "Rev 1";
                Editors()["Report.PreparedBy"].Text = "A. Engineer"; Editors()["Report.CheckedBy"].Text = "B. Checker"; Editors()["Report.ApprovedBy"].Text = "C. Approver";
                // The revision is a number: a non-number keeps its text and shows the validation message; the phase is a per-project field.
                var revisionMessage = Tree(window).OfType<TextBlock>().FirstOrDefault(t => Equals(t.Tag, "revision-message"));
                Check(revisionMessage != null && revisionMessage.Text == "Revision must be a number: 00, 01, 02 …" && Editors()["Report.Revision"].Text == "Rev 1", "Revision 'Rev 1' shows the validation message and is not changed");
                Editors()["Report.Revision"].Text = "02"; Check(revisionMessage.Text == "" && revisionMessage.Visibility == Visibility.Collapsed, "A numeric revision clears the message");
                Editors()["Report.Revision"].Text = "Rev 1";
                var phaseBox = Tree(window).OfType<TextBox>().FirstOrDefault(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Phase");
                Check(phaseBox != null && phaseBox.Text == "", "Phase field on the General page, blank by default");
                phaseBox.Text = "Tender";
                Check(workspace.Result != null && Get("safetyPitResult") == pitResult && Get("carGuideForceResult") == ffResult, "Typing report fields does not clear any displayed result");
                await Navigate("CALCULATIONS"); await Ready();
                var ctx = (DesignContext)Get("context");
                Check(workspace.Result != null && workspace.IsCurrent(Session().Project, ctx), "P1–P4 result stays current after report edits");
                Check(Get("safetyPitResult") == pitResult && SafetyPitCalculationEngine.IsCurrent(pitResult, Session().Project), "P5–P7 result stays current after report edits");
                Check(Get("carGuideForceResult") == ffResult && CarGuideForceCalculationEngine.IsCurrent(ffResult, Session().Project, ctx), "FF result stays current after report edits");
                Check(Session().Project.Report.ClientName == "Delta Developments" && Session().Project.Report.ApprovedBy == "C. Approver" && Session().Dirty, "Report fields committed to the project (unsaved changes)");
                Check(Elevator.Core.Company.DocumentData.PhaseOf(Session().Project) == "Tender" && Session().Project.Report.Revision == "Rev 1", "Phase committed as a project extension; the non-numeric revision is kept as typed");

                // Company / report settings dialog: name and logo (PNG made here), stored in the redirected settings folder.
                string logoPath = Path.Combine(output, "test-logo.png");
                { var visual = new System.Windows.Shapes.Rectangle { Width = 240, Height = 80, Fill = Brushes.SteelBlue }; visual.Measure(new Size(240, 80)); visual.Arrange(new Rect(0, 0, 240, 80));
                  var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(240, 80, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp)); using (var f = File.Create(logoPath)) enc.Save(f); }
                var settingsWindow = new CompanySettingsWindow(CompanyReportSettings.Load());
                Check(settingsWindow.CompanyBox.Text == "" && settingsWindow.Preview.Source == null, "Settings dialog starts blank");
                Check(settingsWindow.PreparedBox.Text == "" && settingsWindow.CheckedBox.Text == "" && settingsWindow.ApprovedBox.Text == "", "Settings dialog: prepared / checked / approved defaults start blank");
                settingsWindow.CompanyBox.Text = "Nile Lifts Engineering"; settingsWindow.LoadLogo(logoPath);
                settingsWindow.PreparedBox.Text = " Default Preparer "; settingsWindow.CheckedBox.Text = "Default Checker"; settingsWindow.ApprovedBox.Text = "";
                Check(settingsWindow.Preview.Source != null, "Logo preview shown");
                settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual; settingsWindow.Left = 40; settingsWindow.Top = 40; settingsWindow.Show(); await Ready();
                { var c = (FrameworkElement)settingsWindow.Content; var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)c.ActualWidth + 40, (int)c.ActualHeight + 40, 96, 96, PixelFormats.Pbgra32); bmp.Render(c); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp)); using (var f = File.Create(Path.Combine(output, "03-company-settings-dialog.png"))) enc.Save(f); }
                settingsWindow.Accept(); settingsWindow.Close();
                var stored = CompanyReportSettings.Load();
                Check(stored.CompanyName == "Nile Lifts Engineering" && stored.Logo() != null && stored.Logo().PixelWidth == 240 && Path.GetDirectoryName(stored.LogoPath) == CompanyReportSettings.SettingsDirectory, "Settings saved with a copied logo in the settings folder");
                Check(!ProjectJson.Serialize(Session().Project).Contains("Nile Lifts"), "Company settings are not stored in the project");
                Check(stored.PreparedBy == "Default Preparer" && stored.CheckedBy == "Default Checker" && stored.ApprovedBy == null && !ProjectJson.Serialize(Session().Project).Contains("Default Preparer"), "Company defaults for prepared / checked saved with the company settings, not in the project");
                sheet = Export("ui-01b-title-block.docx", out text);
                Check(text.Contains("Nile Lifts Engineering\nElevator Calculation Sheet") && text.Contains("Project\nUI Test Tower — Lift 1") && text.Contains("Doc. No.\nNL-CS-002") &&text.Contains("Client\nDelta Developments") && text.Contains("Document No.\nNL-CS-002") && text.Contains("Revision\nRev 1") && text.Contains("Prepared by\nA. Engineer"), "Exported title block and header use the report fields and company name");
                using (var package = System.IO.Packaging.Package.Open(Path.Combine(output, "ui-01b-title-block.docx"), FileMode.Open, FileAccess.Read))
                {
                    Check(package.GetPart(new Uri("/word/header1.xml", UriKind.Relative)).GetRelationships().Count(r => r.RelationshipType.EndsWith("/image")) == 1, "Logo in the header title block");
                    Check(package.GetParts().Count(x => x.ContentType == "image/svg+xml") == 9, "Nine figures (F1–F9) embedded as SVG (with PNG fallbacks)");
                }
                Check(Near(sheet.CarGuideForces.FF1DesignN, 2200) && Near(sheet.SafetyPit.P7.GroupLoadN, 29600), "Numbers unchanged with title block");
                // Save / reopen through the real Save handler.
                string saved = Path.Combine(output, "UI-report-saved.elevator.json"); Session().FilePath = saved; Click("Save");
                var reopened = new ProjectSession(new ElevatorProject()); reopened.Open(saved);
                Check(reopened.Project.Report != null && reopened.Project.Report.DocumentNumber == "NL-CS-002" && reopened.Project.Report.Revision == "Rev 1" && reopened.Project.Report.CheckedBy == "B. Checker", "Report fields survive Save and reopen");

                // Changed input on another page: UI results go stale, export recalculates.
                await Navigate("GENERAL"); Editors()["Elevator.CapacityKg"].Text = "600"; await Navigate("CALCULATIONS");
                sheet = Export("ui-02-changed-capacity.docx", out text);
                Check(Near(sheet.CarGuideForces.FF1BaseN, 1375) && Near(sheet.SafetyPit.P5.PerComponentLoadN, 11000) && text.Contains("= 1375 N") && !text.Contains("= 1100 N"), "Changed capacity recalculated at export; stale values absent");

                // Malformed text: dependent calculations omitted; the dialog lists them before saving.
                Editors()["Calculations.CarMassKg"].Text = "12a"; Editors()["Calculations.CarGuideForces.GuideShoeSpacingMm"].Text = "abc"; await Ready();
                var pending = (CalculationSheet)Call("BuildCalculationSheet");
                Check(!Confirm(pending, false) && shownDialog != null, "Omissions: dialog shown; Cancel stops the export");
                string dialog = DialogText(shownDialog);
                Check(!new[] { "Client", "Document", "Revision", "Prepared by", "Checked by", "Approved by", "Company", "logo" }.Any(w => dialog.Contains(w)), "Omission dialog does not mention title-block fields");
                Check(dialog.Contains("P1–P4 machine-room bearing reactions") && dialog.Contains("P5 safety-gear load per car guide rail") && dialog.Contains("P6 car-buffer reaction") && dialog.Contains("FF1 / FF2 car guide-rail forces") && !dialog.Contains("P7 counterweight"), "Dialog lists exactly the omitted calculations");
                Check(dialog.Contains("Car mass") && dialog.Contains("Guide-shoe spacing h") && dialog.Contains("Export anyway") && dialog.Contains("Cancel"), "Dialog names the invalid inputs and offers Export anyway / Cancel");
                Check(Confirm(pending, true) && shownDialog != null, "Export anyway continues the export");
                shownDialog = (Window)Call("OmissionDialog", pending); // fresh, unclosed copy for the screenshot
                shownDialog.WindowStartupLocation = WindowStartupLocation.Manual; shownDialog.Left = 40; shownDialog.Top = 40; shownDialog.Show(); shownDialog.UpdateLayout(); await Ready();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((FrameworkElement)shownDialog.Content).ActualWidth + 40, (int)((FrameworkElement)shownDialog.Content).ActualHeight + 40, 96, 96, PixelFormats.Pbgra32); bitmap.Render((Visual)shownDialog.Content);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(output, "02-omission-dialog.png"))) png.Save(file); shownDialog.Close();
                sheet = Export("ui-03-malformed-text.docx", out text);
                Check(sheet.Chassis.Status == "INVALID_INPUT" && sheet.SafetyPit.P5.Status == "INVALID_INPUT" && sheet.SafetyPit.P7.PerComponentLoadN.HasValue && sheet.CarGuideForces.Status == "INVALID_INPUT", "Malformed car mass / h omit P1–P6 and FF, P7 kept");
                Check(text.Contains("P7(total)") && !text.Contains("P5 =") && !text.Contains("FF1") && !text.Contains("T(car)"), "Document contains only the available calculation");

                // Corrected values, then Advanced mode selected with incomplete Advanced inputs.
                Editors()["Calculations.CarMassKg"].Text = "500"; Editors()["Calculations.CarGuideForces.GuideShoeSpacingMm"].Text = "1200"; await Ready();
                var mode = Choices()["Mode"]; int index = 0; foreach (var item in mode.Items) { if (Convert.ToString(item.GetType().GetProperty("Id").GetValue(item, null)) == "Advanced") break; ++index; }
                mode.SelectedIndex = index; await Ready();
                sheet = Export("ui-04-advanced-incomplete.docx", out text);
                Check(sheet.Mode == "Advanced" && !sheet.ChassisAvailable && !text.Contains("P1–P4") && text.Contains("1  Calculation summary") && text.Contains("3  Safety-gear and buffer loads P5–P7") && text.Contains("4  Car guide-rail forces FF1 / FF2"), "Advanced gaps: P1–P4 omitted, remaining sections renumbered");

                // Older project: nothing available, export blocked with the reason in the application.
                var legacy = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json"))); legacy.Info.Name = "Older project";
                // Its 8 mm ropes on the 250 mm deflector gave only a deflector D/d result, which is a value only since DESIGN-FLOW 11a.
                Check(!CalculationSheet.Build(legacy, (DesignContext)Get("context"), new CalculationSheetInputs()).RopeTractionAvailable, "Older project: no rope / traction section from the deflector D/d alone (DESIGN-FLOW 11a)");
                legacy.Suspension.RopeDiameterMm = null;
                Attach(legacy); await Ready();
                var none = (CalculationSheet)Call("BuildCalculationSheet");
                Check(!none.HasContent && !Confirm(none, true) && shownDialog == null, "Nothing available: export blocked without the omission dialog");
                string message = (string)Get("calculationSheetMessage");
                Check(message != null && message.Contains("No calculation sheet was exported") && message.Contains("Car mass") && message.Contains("Guide-shoe spacing h"), "Application explains why nothing was exported" + (message == null ? "" : " [" + message.Replace("\n", " | ") + "]"));
                bool blocked = false; try { Call("WriteCalculationSheet", Path.Combine(output, "ui-05-blocked.docx")); } catch (InvalidOperationException) { blocked = true; }
                Check(blocked && !File.Exists(Path.Combine(output, "ui-05-blocked.docx")), "No file is written when nothing is available");
                exit = 0;
            }
            catch (Exception error) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = error.ToString() }); }
            finally
            {
                Wire.Write(Path.Combine(output, "calculation-sheet-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real rendered WPF window; editors driven through normal TextChanged/navigation/selection handlers; export via the same WriteCalculationSheet path used after the Save As dialog; omission dialog driven through its own buttons via a non-modal stub. Save As dialog not automated." });
                window.Close();
            }
        };
        application.Run(window); return exit;
    }
}
