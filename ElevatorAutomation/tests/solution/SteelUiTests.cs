using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Steel;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// S3 (steel-ui): the Shaft page of a steel structure in the real rendered WPF window: the construction choice, the profile lists, the
// plate and bolt presets, the Advanced values and the beam levels per face; each follows its preset, locks when edited and has Reset;
// the beams follow the car rail bracket spacing of the RAILS page; a concrete project has no steel content at all.
public static class SteelUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    static readonly string[] Pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> Choices() { return (Dictionary<string, ComboBox>)Get("presetChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static ElevatorProject Project() { return Session().Project; }
    static PresetLibrary Library() { return (PresetLibrary)Get("presets"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Shot(string name) { await Ready(); Call("Screenshot", Path.Combine(output, name + ".png")); }
    static void Reset(Control editor) { var item = editor.ContextMenu.Items.OfType<MenuItem>().First(); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
    static async Task Expand() { foreach (var x in Tree(window).OfType<Expander>().Where(x => Convert.ToString(x.Header) == "Advanced")) x.IsExpanded = true; await Ready(); }
    static string Json() { return ProjectJson.Serialize(Project()).ToLowerInvariant(); }
    static SteelStructure Steel() { return SteelStructure.Build(Project(), Library()); }
    static string LevelKey(string face, string key) { return "Steel.Level." + face + "." + key; }

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
                Call("ShowPage", "SHAFT"); await Ready();
                // ---------- concrete: only the construction choice ----------
                Check(Choices().ContainsKey("Steel.Construction") && (string)Choices()["Steel.Construction"].SelectedValue == SteelShaftPreset.Concrete, "A new project is concrete and the Shaft page shows the choice");
                Check(!Choices().ContainsKey("Steel.ColumnProfile") && !Editors().ContainsKey("Steel.BasePlateWidthMm"), "A concrete project shows no steel value");
                string concrete = Json();
                Check(!concrete.Contains("steel"), "A concrete project has no steel content in its file");
                // ---------- choose steel ----------
                Choices()["Steel.Construction"].SelectedValue = SteelShaftPreset.SteelStructure; await Ready();
                Check(SteelFields.IsSteel(Project(), Library()) && Project().UserEditedFields.Contains("Steel.Construction"), "Choosing Steel structure locks the construction");
                Check(Choices().ContainsKey("Steel.ColumnProfile") && (string)Choices()["Steel.ColumnProfile"].SelectedValue == "UPN 140", "The column profile shows its preset UPN 140");
                Check((string)Choices()["Steel.BeamProfile"].SelectedValue == "UPN 100" && (string)Choices()["Steel.BracingProfile"].SelectedValue == "L50x50x5", "The beam profile UPN 100 and the bracing L50x50x5");
                var listed = ((System.Collections.IEnumerable)Choices()["Steel.ColumnProfile"].ItemsSource).Cast<object>().Select(o => (string)o.GetType().GetProperty("Name").GetValue(o, null)).ToArray();
                Check(listed.SequenceEqual(new[] { "UPN 80", "UPN 100", "UPN 120", "UPN 140", "UPN 160", "UPN 180", "UPN 200" }), "The column list is the UPN catalogue");
                Check(Editors()["Steel.BasePlateWidthMm"].Text == "200" && Editors()["Steel.BasePlateDepthMm"].Text == "200" && Editors()["Steel.BasePlateThicknessMm"].Text == "15", "The base plate 200 x 200 x 15");
                Check((string)Choices()["Steel.BoltCount"].SelectedValue == "4" && Editors()["Steel.BoltDiameterMm"].Text == "20", "Four bolts of 20 mm");
                await Expand();
                Check(Editors()["Steel.GussetLengthMm"].Text == "400" && Editors()["Steel.Base.MarginMm"].Text == "200" && Editors()["Steel.Base.ThicknessMm"].Text == "250" && Editors()["Steel.Base.BarDiameterMm"].Text == "8", "The gusset and the concrete base presets are under Advanced");
                // the building slab edge and the machine housing are under Advanced too
                Check(Editors()["Steel.SlabEdgeDepthMm"].Text == "250" && Editors()["Steel.HousingHeightMm"].Text == "1950" && Editors()["Steel.HousingOverhangMm"].Text == "160", "The slab edge depth, the housing height and its roof overhang presets");
                Check(Editors()["Steel.SlabEdgeWidthMm"].Text == (Project().Shaft.WidthMm + 1700).ToString(System.Globalization.CultureInfo.InvariantCulture), "The slab edge width is the shaft width + 2 x 850");
                Editors()["Steel.HousingHeightMm"].Text = "2100"; await Ready();
                Check(Project().UserEditedFields.Contains("Steel.HousingHeightMm") && Steel().Inputs.HousingHeightMm == 2100, "Typing the housing height locks it");
                Reset(Editors()["Steel.HousingHeightMm"]); await Ready();
                Check(Editors()["Steel.HousingHeightMm"].Text == "1950" && !Project().UserEditedFields.Contains("Steel.HousingHeightMm"), "Reset gives 1950 back");
                // ---------- lock and Reset ----------
                Choices()["Steel.ColumnProfile"].SelectedValue = "UPN 160"; await Ready();
                Check(Project().UserEditedFields.Contains("Steel.ColumnProfile") && Steel().Inputs.Column.Name == "UPN 160", "Choosing another column locks it and the tower uses it");
                Reset(Choices()["Steel.ColumnProfile"]); await Ready();
                Check(!Project().UserEditedFields.Contains("Steel.ColumnProfile") && (string)Choices()["Steel.ColumnProfile"].SelectedValue == "UPN 140", "Reset gives UPN 140 back");
                Editors()["Steel.BasePlateWidthMm"].Text = "250"; await Ready();
                Check(Project().UserEditedFields.Contains("Steel.BasePlateWidthMm") && Steel().Inputs.PlateWidthMm == 250, "Typing the plate width locks it");
                Reset(Editors()["Steel.BasePlateWidthMm"]); await Ready();
                Check(Editors()["Steel.BasePlateWidthMm"].Text == "200" && !Project().UserEditedFields.Contains("Steel.BasePlateWidthMm"), "Reset gives 200 back");
                Editors()["Steel.BasePlateWidthMm"].Text = "-5"; await Ready();
                Check(!Project().UserEditedFields.Contains("Steel.BasePlateWidthMm") || Steel().Inputs.PlateWidthMm > 0, "A value that is not above zero is not taken");
                Reset(Editors()["Steel.BasePlateWidthMm"]); await Ready();
                // ---------- the beam levels per face ----------
                Call("ShowPage", "SHAFT"); await Ready(); await Expand();
                var s = Steel();
                foreach (var face in SteelFields.Faces)
                    foreach (var l in s.Levels[face])
                        Check(Editors().ContainsKey(LevelKey(face, l.Key)), "The page lists the level " + l.Key + " of the " + face + " face");
                var left = s.Levels["Left"].First(l => l.Kind == SteelLevelKind.Intermediate);
                double before = left.ElevationMm;
                Editors()[LevelKey("Left", left.Key)].Text = (before + 100).ToString(System.Globalization.CultureInfo.InvariantCulture); await Ready();
                Check(SteelFields.LevelsEdited(Project(), "Left") && !SteelFields.LevelsEdited(Project(), "Right") && Steel().Levels["Left"].Single(l => l.Key == left.Key).ElevationMm == before + 100,
                    "Typing a level locks it on its own face only");
                Check(Steel().Levels["Right"].Single(l => l.Key == left.Key).ElevationMm == before, "The other faces keep the rule");
                Call("ShowPage", "SHAFT"); await Ready(); await Expand();
                var reset = Tree(window).OfType<Button>().First(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Reset beam levels Left");
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Ready();
                Check(!SteelFields.LevelsEdited(Project()), "Reset of the face gives its levels back to the rule");
                // ---------- the beams follow the car rail spacing of the RAILS page ----------
                int perFloorAtRecommended = Steel().Levels["Left"].Count;
                Call("ShowPage", "RAILS"); await Ready();
                Editors()["Rails.CarSpacingMm"].Text = "1000"; await Ready();
                var tight = Steel();
                Check(tight.Inputs.SpacingMm == 1000 && tight.Levels["Left"].Count > perFloorAtRecommended, "A smaller car spacing on RAILS gives more beams");
                Check(tight.LargestGapMm.Value <= 1000 + 1e-6, "No gap of the side and back faces is larger than the spacing");
                Reset(Editors()["Rails.CarSpacingMm"]); await Ready();
                // ---------- back to concrete ----------
                Call("ShowPage", "SHAFT"); await Ready();
                Reset(Choices()["Steel.Construction"]); await Ready();
                Check(!SteelFields.IsSteel(Project(), Library()) && !Choices().ContainsKey("Steel.ColumnProfile"), "Reset of the construction gives the concrete shaft back");
                foreach (var f in SteelFields.All.Where(f => f.Key != "Steel.Construction")) f.Reset(Project());
                SteelFields.ResetLevels(Project());
                Check(!Json().Contains("steel"), "The file has no steel content again");
                foreach (var page in Pages) { Call("ShowPage", page); await Ready(); }
                Check(true, "All 11 pages still build");
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString(), Checks = names }); }
            finally
            {
                Wire.Write(Path.Combine(output, "steel-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real WPF controls driven by programmatic events. Not mouse/keyboard testing." });
                window.Closing += (s, ce) => { };
                window.Close();
            }
        };
        application.Run(window);
        return exit;
    }
}
