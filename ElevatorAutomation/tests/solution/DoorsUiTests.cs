using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// DESIGN-FLOW "Doors" (D1) in the real rendered WPF window: the Car & Doors page offers the door type; an automatic type shows the
// clear opening (empty while the rules select it, with the size in use), the handing and the Advanced dimensions with Reset per
// field; a value that does not fit shows a short "does not fit" message; Manual leaves the project as it was.
public static class DoorsUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    static readonly string[] Pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> Choices() { return (Dictionary<string, ComboBox>)Get("presetChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Shot(string name) { await Ready(); Call("Screenshot", Path.Combine(output, name + ".png")); }
    static string Name(DependencyObject d) { return System.Windows.Automation.AutomationProperties.GetName(d); }
    static TextBlock Block(string automationName) { window.UpdateLayout(); return Tree(window).OfType<TextBlock>().FirstOrDefault(t => Name(t) == automationName); }
    static Button ResetButton(string label) { window.UpdateLayout(); return Tree(window).OfType<Button>().FirstOrDefault(b => Name(b) == "Reset " + label); }
    static bool AnyText(string part) { window.UpdateLayout(); return Tree(window).OfType<TextBlock>().Any(t => t.IsVisible && (t.Text ?? "").Contains(part)); }
    static string Doc() { return String.Join("|", Session().Project.UserEditedFields == null ? new string[0] : Session().Project.UserEditedFields.Where(k => k.StartsWith("Doors.")).ToArray()); }
    static ElevatorParameters Plan() { return new ElevatorDesignEngine().Calculate(Session().Project, (DesignContext)Get("context"), DrawingViews.Plan).Plan; }
    static async Task Expand() { foreach (var x in Tree(window).OfType<Expander>().Where(x => Convert.ToString(x.Header) == "Advanced dimensions")) x.IsExpanded = true; await Ready(); }
    static void Type(string key, string text) { Editors()[key].Text = text; }

    [STAThread]
    public static int Main(string[] arguments)
    {
        output = arguments[0]; if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; } Directory.CreateDirectory(output);
        var application = new Application(); window = new MainWindow(new string[0]); Set("smoke", true);
        int exit = 1;
        window.Loaded += async (sender, e) =>
        {
            try
            {
                window.Width = 1280; window.Height = 900; await Ready();
                var p = Session().Project;
                ProjectPresets.Lock(p, "Shaft.WidthMm"); ProjectPresets.Lock(p, "Shaft.DepthMm");
                p.Shaft.WidthMm = 1900; p.Shaft.DepthMm = 2800;
                string before = ProjectJson.Serialize(p);
                // ---------- Manual: the page is as before, plus the door type ----------
                Call("ShowPage", "CAR & DOORS"); await Ready();
                Check(Choices().ContainsKey("Doors.Type") && (string)Choices()["Doors.Type"].SelectedValue == DoorTypes.Manual, "Manual door type is shown and selected on a new project");
                Check(!Editors().ContainsKey("Doors.OpeningMm") && Block("Door fit message") == null, "Manual shows no opening, handing or clearance fields");
                double manualFront = Plan().CarFront;
                Check(manualFront == 125, "Manual front allowance is the existing 125 mm");
                await Shot("01-manual");
                // ---------- Automatic 2S ----------
                Choices()["Doors.Type"].SelectedIndex = Array.IndexOf(DoorTypes.All, DoorTypes.Automatic2S); await Ready();
                Check(Session().Project.Doors.Type == DoorTypes.Automatic2S, "Choosing Automatic 2S sets the project door type");
                Check(Editors().ContainsKey("Doors.OpeningMm") && Editors().ContainsKey("Doors.CarZoneMm") && Editors().ContainsKey("Doors.StackReturnMm") && Choices().ContainsKey("Doors.Handing"), "2S page offers opening, handing and every clearance");
                Check(Editors()["Doors.OpeningMm"].Text == "", "The opening is empty while the rules select it");
                Check(Block("Clear opening in use").Text == "In use: 900 mm", "The size in use is shown (900 on a 1900 shaft)");
                Check(Editors()["Doors.CarZoneMm"].Text == "130" && Editors()["Doors.SillGapMm"].Text == "25" && Editors()["Doors.LandingZoneMm"].Text == "90" && Editors()["Doors.FixedReturnMm"].Text == "200" && Editors()["Doors.StackReturnMm"].Text == "400", "Clearances show the catalog values (130 / 25 / 90, returns 200 / 400)");
                Check(Doc() == "" && !Session().Project.Extensions.ContainsKey("doors"), "Nothing is locked after choosing the type");
                Check(Plan().CarFront == 245 && Plan().DoorWidth == 900, "The front zone 245 and the selected opening reach the plan");
                Check(Block("Door fit message").Visibility == Visibility.Collapsed, "No fit message while it fits");
                await Shot("02-2s-auto");
                // ---------- a user size that does not fit ----------
                Type("Doors.OpeningMm", "1200"); await Ready();
                Check(Session().Project.UserEditedFields.Contains(DoorSettings.OpeningKey) && DoorSettings.Read(Session().Project).OpeningMm == 1200, "Typing the opening locks it");
                var msg = Block("Door fit message");
                Check(msg.Visibility == Visibility.Visible && msg.Text.StartsWith("does not fit: needs car width"), "A size that does not fit shows 'does not fit: needs car width ...'");
                Check(Block("Clear opening in use").Text == "In use: 1200 mm" && Plan().DoorWidth == 1200, "The locked size is kept in use");
                Check(ResetButton("Clear opening (mm)").IsEnabled, "Reset is enabled for a locked field");
                await Shot("03-2s-1200-no-fit");
                Type("Doors.OpeningMm", "850"); await Ready();
                Check(Block("Door fit message").Text.Contains("not a standard opening"), "A non-standard size is named as such");
                ResetButton("Clear opening (mm)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(Editors()["Doors.OpeningMm"].Text == "" && !Session().Project.UserEditedFields.Contains(DoorSettings.OpeningKey) && Block("Clear opening in use").Text == "In use: 900 mm", "Reset gives back the selected size");
                Check(Block("Door fit message").Visibility == Visibility.Collapsed, "The message goes with the reset");
                // ---------- handing ----------
                Choices()["Doors.Handing"].SelectedIndex = Array.IndexOf(DoorTypes.Handings, DoorTypes.Left); await Ready();
                Check(DoorSettings.Read(Session().Project).Handing == DoorTypes.Left && Session().Project.UserEditedFields.Contains(DoorSettings.HandingKey), "Handing Left locks");
                Choices()["Doors.Handing"].SelectedIndex = Array.IndexOf(DoorTypes.Handings, DoorTypes.Auto); await Ready();
                Check(!Session().Project.UserEditedFields.Contains(DoorSettings.HandingKey), "Handing Auto resets");
                // ---------- an Advanced dimension ----------
                Type("Doors.SillGapMm", "40"); await Ready();
                Check(Plan().CarFront == 130 + 40 + 90, "A locked sill gap changes the front zone and the car front (260)");
                Check(Block("Door fit message").Text.Contains("sill gap 40 mm exceeds 35 mm"), "A sill gap above 35 mm is reported");
                await Expand();
                Check(Editors()["Doors.CarZoneMm"].Text == "130" && ResetButton("Sill gap (mm)").IsEnabled && !ResetButton("Car door zone (mm)").IsEnabled, "Only the edited field is locked");
                ResetButton("Sill gap (mm)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(Editors()["Doors.SillGapMm"].Text == "25" && Plan().CarFront == 245, "Reset per field restores the preset");
                // ---------- unlocked values follow the shaft ----------
                Session().Project.Shaft.WidthMm = 2100; Call("ShowPage", "CAR & DOORS"); await Ready();
                Check(Block("Clear opening in use").Text == "In use: 1000 mm", "An unlocked opening follows the shaft width (1000 on 2100)");
                Session().Project.Shaft.WidthMm = 1900;
                // ---------- Automatic 2C ----------
                Call("ShowPage", "CAR & DOORS"); await Ready();
                Choices()["Doors.Type"].SelectedIndex = Array.IndexOf(DoorTypes.All, DoorTypes.Automatic2C); await Ready();
                Check(Session().Project.Doors.Type == DoorTypes.Automatic2C && !Editors().ContainsKey("Doors.StackReturnMm") && !Choices().ContainsKey("Doors.Handing"), "2C has no stacking return, offset or handing");
                Check(Editors()["Doors.CarZoneMm"].Text == "110" && Editors()["Doors.SillGapMm"].Text == "30" && Editors()["Doors.LandingZoneMm"].Text == "80", "2C front-zone split 110 / 30 / 80");
                Check(Plan().CarFront == 220, "2C front zone 220 reaches the plan");
                await Shot("04-2c-auto");
                // ---------- back to Manual ----------
                Choices()["Doors.Type"].SelectedIndex = Array.IndexOf(DoorTypes.All, DoorTypes.Manual); await Ready();
                Check(Session().Project.Doors.Type == null && Doc() == "" && !Session().Project.Extensions.ContainsKey("doors"), "Manual again: no door type, no door data");
                Check(Plan().CarFront == manualFront, "Manual again: the front allowance is the existing one");
                Check(!Editors().ContainsKey("Doors.OpeningMm"), "Manual again: the automatic fields are gone");
                // The page of every other part is unaffected.
                foreach (var page in Pages) { Call("ShowPage", page); await Ready(); }
                Check(true, "All 11 pages still build");
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString(), Checks = names }); }
            finally
            {
                Wire.Write(Path.Combine(output, "doors-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real WPF controls driven by programmatic events. Not mouse/keyboard testing." });
                window.Closing += (s, ce) => { };
                window.Close();
            }
        };
        application.Run(window);
        return exit;
    }
}
