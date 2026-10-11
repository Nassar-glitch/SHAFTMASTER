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
    static DoorOffset Off() { var plan = Plan(); var door = DoorInput.From(Session().Project, ((DesignContext)Get("context")).Doors); return DoorOffset.From(door, DoorRules.Select(door, plan), plan); }
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
                Check(Block("Clear opening in use").Text == "In use: 1000 mm", "The size in use is shown (1000 on a 1900 shaft)");
                Check(Editors()["Doors.CarZoneMm"].Text == "130" && Editors()["Doors.SillGapMm"].Text == "25" && Editors()["Doors.LandingZoneMm"].Text == "90" && Editors()["Doors.FixedReturnMm"].Text == "200" && Editors()["Doors.StackReturnMm"].Text == "450", "Clearances show the catalog values (130 / 25 / 90, preferred returns 200 / 450)");
                Check(Doc() == "" && !Session().Project.Extensions.ContainsKey("doors"), "Nothing is locked after choosing the type");
                Check(Plan().CarFront == 245 && Plan().DoorWidth == 1000, "The front zone 245 and the selected opening reach the plan");
                Check(Block("Door fit message").Visibility == Visibility.Collapsed, "No fit message while it fits");
                await Shot("02-2s-auto");
                // ---------- a user size that does not fit ----------
                Type("Doors.OpeningMm", "1200"); await Ready();
                Check(Session().Project.UserEditedFields.Contains(DoorSettings.OpeningKey) && DoorSettings.Read(Session().Project).OpeningMm == 1200, "Typing the opening locks it");
                var msg = Block("Door fit message");
                Check(msg.Visibility == Visibility.Visible && msg.Text == "does not fit: needs car width 1650 mm, shaft width 2050 mm", "A size that does not fit shows the true minimum car and shaft width (1650 / 2050 for 1200 on 1900)");
                Check(Block("Clear opening in use").Text == "In use: 1200 mm" && Plan().DoorWidth == 1200, "The locked size is kept in use");
                Check(ResetButton("Clear opening (mm)").IsEnabled, "Reset is enabled for a locked field");
                await Shot("03-2s-1200-no-fit");
                Type("Doors.OpeningMm", "850"); await Ready();
                Check(Block("Door fit message").Text.Contains("not a standard opening"), "A non-standard size is named as such");
                ResetButton("Clear opening (mm)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(Editors()["Doors.OpeningMm"].Text == "" && !Session().Project.UserEditedFields.Contains(DoorSettings.OpeningKey) && Block("Clear opening in use").Text == "In use: 1000 mm", "Reset gives back the selected size");
                Check(Block("Door fit message").Visibility == Visibility.Collapsed, "The message goes with the reset");
                // ---------- handing ----------
                Choices()["Doors.Handing"].SelectedIndex = Array.IndexOf(DoorTypes.Handings, DoorTypes.Left); await Ready();
                Check(DoorSettings.Read(Session().Project).Handing == DoorTypes.Left && Session().Project.UserEditedFields.Contains(DoorSettings.HandingKey), "Handing Left locks");
                Check(Name(Editors()["Doors.FixedReturnMm"]) == "Car return, right, preferred (mm)" && Name(Editors()["Doors.StackReturnMm"]) == "Car return, left, preferred (mm)", "Panels stack left: the fixed return is the right one");
                Choices()["Doors.Handing"].SelectedIndex = Array.IndexOf(DoorTypes.Handings, DoorTypes.Auto); await Ready();
                Check(!Session().Project.UserEditedFields.Contains(DoorSettings.HandingKey), "Handing Auto resets");
                Check(Name(Editors()["Doors.FixedReturnMm"]) == "Car return, left, preferred (mm)" && Name(Editors()["Doors.StackReturnMm"]) == "Car return, right, preferred (mm)" && Name(Editors()["Doors.CarZoneMm"]) == "Car door depth (mm)" && Name(Editors()["Doors.LandingZoneMm"]) == "Landing door depth (mm)", "Labels name Left / Right through the handing (stack right: fixed return left) and the depths");
                Check(Name(Choices()["Doors.Handing"]) == "Panels stack to" && Name(Choices()["Doors.Type"]) == "Door type", "The handing field is called Panels stack to");
                Check(((System.Collections.IEnumerable)Choices()["Doors.Type"].ItemsSource).Cast<Choice>().Select(c => c.Name).SequenceEqual(new[] { "Manual", "Side opening (2S)", "Centre opening (2C)" }), "Door type names are Manual, Side opening (2S), Centre opening (2C)");
                // ---------- Doors Offset ----------
                Check(Editors().ContainsKey("Doors.OffsetMm") && Choices().ContainsKey("Doors.OffsetSide") && !Editors().ContainsKey("Doors.OpeningOffsetMm"), "2S page offers the Doors Offset field with a Left / Right selector (no former opening offset)");
                var off = Off();
                Check((string)Choices()["Doors.OffsetSide"].SelectedValue == DoorTypes.Left && Editors()["Doors.OffsetMm"].Text == Math.Round(Math.Abs(off.PlacedShiftMm), 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), "The default is the preferred offset from the returns, as Left (stack on the right)");
                Check(Block("Doors Offset range").Text == off.AllowedText && off.AllowedText.StartsWith("Allowed: ") && off.AllowedText.Contains("Left") && off.AllowedText.Contains(" … "), "The allowed range is shown under the field: " + off.AllowedText);
                Check(Block("Doors Offset message").Visibility == Visibility.Collapsed && !Session().Project.UserEditedFields.Contains(DoorSettings.OffsetKey) && !ResetButton("Doors Offset").IsEnabled, "Nothing locked and no message at the default");
                await Shot("03b-2s-doors-offset");
                Type("Doors.OffsetMm", "140"); await Ready();
                Check(Session().Project.UserEditedFields.Contains(DoorSettings.OffsetKey) && Session().Project.UserEditedFields.Contains(DoorSettings.OffsetSideKey) && DoorSettings.Read(Session().Project).OffsetMm == 140 && DoorSettings.Read(Session().Project).OffsetSide == DoorTypes.Left, "Typing a distance locks distance and side");
                Check(Math.Abs(Plan().CarFront - 245) < 1e-9 && Math.Abs(Off().PlacedShiftMm + 140) < 1e-6 && Block("Doors Offset message").Visibility == Visibility.Collapsed, "140 Left is placed 140 mm left of the shaft centre; in range, no message");
                Choices()["Doors.OffsetSide"].SelectedValue = DoorTypes.Right; await Ready();
                off = Off();
                Check(DoorSettings.Read(Session().Project).OffsetSide == DoorTypes.Right && Editors()["Doors.OffsetMm"].Text == "140", "Choosing Right keeps the distance and locks the side");
                Check(off.OutOfRange && Block("Doors Offset message").Visibility == Visibility.Visible && Block("Doors Offset message").Text == off.Message && off.Message.StartsWith("out of range: "), "A value beyond the range is kept and shows: " + off.Message);
                Check(Math.Abs(off.PlacedShiftMm - off.MaxShiftMm) < 1e-6 && off.PlacedShiftMm < 0 && Math.Abs(off.RequestedShiftMm - 140) < 1e-6, "The position used is the nearest allowed one, the typed value stays");
                Check(ResetButton("Doors Offset").IsEnabled, "Reset is enabled for a locked offset");
                ResetButton("Doors Offset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(!Session().Project.UserEditedFields.Contains(DoorSettings.OffsetKey) && !Session().Project.UserEditedFields.Contains(DoorSettings.OffsetSideKey) && Block("Doors Offset message").Visibility == Visibility.Collapsed && (string)Choices()["Doors.OffsetSide"].SelectedValue == DoorTypes.Left, "Reset to preset returns the default and clears the message");
                // ---------- an Advanced dimension ----------
                Type("Doors.SillGapMm", "40"); await Ready();
                Check(Plan().CarFront == 130 + 40 + 90, "A locked sill gap changes the front zone and the car front (260)");
                Check(Block("Door fit message").Text.Contains("sill gap 40 mm exceeds 35 mm"), "A sill gap above 35 mm is reported");
                await Expand();
                Check(Editors().ContainsKey("Doors.MinFixedReturnMm") && Editors()["Doors.MinFixedReturnMm"].Text == "0" && Editors()["Doors.MinStackReturnMm"].Text == "0" && Editors()["Doors.LandingFixedJambMinMm"].Text == "0" && Editors()["Doors.LandingWallClearanceMm"].Text == "25", "Minimums are fields too: returns 0, fixed jamb 0, frame to wall 25");
                Check(Editors()["Doors.CarZoneMm"].Text == "130" && ResetButton("Sill gap (mm)").IsEnabled && !ResetButton("Car door depth (mm)").IsEnabled, "Only the edited field is locked");
                ResetButton("Sill gap (mm)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(Editors()["Doors.SillGapMm"].Text == "25" && Plan().CarFront == 245, "Reset per field restores the preset");
                // ---------- unlocked values follow the shaft ----------
                Session().Project.Shaft.WidthMm = 2100; Call("ShowPage", "CAR & DOORS"); await Ready();
                Check(Block("Clear opening in use").Text == "In use: 1200 mm", "An unlocked opening follows the shaft width (1200 on 2100)");
                Session().Project.Shaft.WidthMm = 1900;
                // ---------- Automatic 2C ----------
                Call("ShowPage", "CAR & DOORS"); await Ready();
                Choices()["Doors.Type"].SelectedIndex = Array.IndexOf(DoorTypes.All, DoorTypes.Automatic2C); await Ready();
                Check(Session().Project.Doors.Type == DoorTypes.Automatic2C && !Editors().ContainsKey("Doors.StackReturnMm") && !Choices().ContainsKey("Doors.Handing"), "2C has no stacking return, offset or handing");
                Check(Editors()["Doors.CarZoneMm"].Text == "110" && Editors()["Doors.SillGapMm"].Text == "30" && Editors()["Doors.LandingZoneMm"].Text == "80", "2C front-zone split 110 / 30 / 80");
                Check(Plan().CarFront == 220, "2C front zone 220 reaches the plan");
                Check(Editors().ContainsKey("Doors.OffsetMm") && Editors()["Doors.OffsetMm"].Text == "0" && Block("Doors Offset range").Text.StartsWith("Allowed: "), "2C shows the Doors Offset, 0 (centred), with its range");
                // The automatic size takes the whole width (900 on 1900): nothing is left to move, so a typed offset is out of range.
                Check(Block("Doors Offset range").Text == "Allowed: 0 … 0", "2C with the largest fitting opening has no room to move");
                Type("Doors.OffsetMm", "5"); await Ready();
                Check(Off().OutOfRange && Block("Doors Offset message").Visibility == Visibility.Visible && Math.Abs(Off().PlacedShiftMm) < 1e-6, "A 2C offset with no room is kept, flagged and drawn centred");
                ResetButton("Doors Offset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Type("Doors.OpeningMm", "800"); await Ready();
                Type("Doors.OffsetMm", "30"); await Ready();
                Check(!Off().OutOfRange && Math.Abs(Math.Abs(Off().PlacedShiftMm) - 30) < 1e-6 && Block("Doors Offset range").Text == "Allowed: 100 Left … 100 Right", "2C 800 on 1900 follows a typed offset inside Allowed: 100 Left … 100 Right");
                ResetButton("Doors Offset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                ResetButton("Clear opening (mm)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                await Shot("04-2c-auto");
                // ---------- back to Manual ----------
                Choices()["Doors.Type"].SelectedIndex = Array.IndexOf(DoorTypes.All, DoorTypes.Manual); await Ready();
                Check(Session().Project.Doors.Type == null && Doc() == "" && !Session().Project.Extensions.ContainsKey("doors"), "Manual again: no door type, no door data");
                Check(Plan().CarFront == manualFront, "Manual again: the front allowance is the existing one");
                Check(!Editors().ContainsKey("Doors.OpeningMm") && !Editors().ContainsKey("Doors.OffsetMm"), "Manual again: the automatic fields (opening, Doors Offset) are gone");
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
