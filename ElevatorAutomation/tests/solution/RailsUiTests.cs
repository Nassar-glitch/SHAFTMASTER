using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Reporting.Figures;
using Elevator.Reporting.Sheet;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// DESIGN-FLOW "Guide rails" (stage R2) in the real rendered WPF window: the RAILS page shows the five choices in the agreed order, each with
// its recommendation as the preset; an edit locks, Reset gives the recommendation back; a choice that fails is kept and the page shows no
// warning text; the Advanced values follow the library until edited; the chosen profiles and the spacing reach the project, the design
// result, the calculation sheet and the Summary table.
public static class RailsUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>(); static readonly List<string> numbers = new List<string>();
    static readonly string[] Pages = { "GENERAL", "SHAFT", "FLOORS", "CAR & DOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS", "DRAWINGS", "VALIDATION", "ABOUT" };
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static Dictionary<string, TextBox> Editors() { return (Dictionary<string, TextBox>)Get("editors"); }
    static Dictionary<string, ComboBox> Choices() { return (Dictionary<string, ComboBox>)Get("presetChoices"); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static ElevatorProject Project() { return Session().Project; }
    static DesignContext Context() { return (DesignContext)Get("context"); }
    static PresetLibrary Library() { return (PresetLibrary)Get("presets"); }
    static IEnumerable<DependencyObject> Tree(DependencyObject node) { yield return node; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); ++i) foreach (var c in Tree(VisualTreeHelper.GetChild(node, i))) yield return c; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static async Task Shot(string name) { await Ready(); Call("Screenshot", Path.Combine(output, name + ".png")); }
    static string Name(DependencyObject d) { return System.Windows.Automation.AutomationProperties.GetName(d); }
    static bool Edited(string key) { return Project().UserEditedFields != null && Project().UserEditedFields.Contains(key); }
    static void Type(string key, string text) { Editors()[key].Text = text; }
    static string Shown(string key) { return (string)Choices()[key].SelectedValue; }
    static PresetContext Ctx() { return new PresetContext { Project = Project(), Library = Library(), Design = Context() }; }
    static object Eff(string key) { return ProjectPresets.Find(key).Effective(Ctx()); }
    static GuideRailCheckResult Check_() { return GuideRailCheckEngine.Calculate(Project(), Context(), Library()); }
    // The reset entry of the context menu of an editor, as the user would pick it.
    static void Reset(Control editor) { var item = editor.ContextMenu.Items.OfType<MenuItem>().First(); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
    static async Task Expand() { foreach (var x in Tree(window).OfType<Expander>().Where(x => Convert.ToString(x.Header) == "Advanced")) x.IsExpanded = true; await Ready(); }
    static IEnumerable<string> RowLabels() { return Tree((DependencyObject)Get("content")).OfType<TextBlock>().Where(t => t.FontSize == 13 && t.IsVisible).Select(t => t.Text); }
    static string Spacing(string key) { return Editors()[key].Text; }

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
                Call("ShowPage", "RAILS"); await Ready();
                // ---------- order and recommendations ----------
                var labels = RowLabels().ToList();
                var wanted = new[] { "Machine supported on the guide rails", "Car rail profile", "Car rail bracket spacing (mm)", "Counterweight rail profile", "Counterweight rail bracket spacing (mm)" };
                var at = wanted.Select(w => labels.IndexOf(w)).ToList();
                Check(at.All(i => i >= 0) && at.SequenceEqual(at.OrderBy(i => i)), "The page shows the five choices in the DESIGN-FLOW order");
                Check(labels.IndexOf("Number of rails carrying the machine") < 0 || labels.IndexOf("Number of rails carrying the machine") > at.Last(), "The Advanced values come after the five choices");
                var carProfile = (string)GuideRailRecommendation.ForField(Ctx(), ProjectPresets.CarProfileKey);
                var cwProfile = (string)GuideRailRecommendation.ForField(Ctx(), ProjectPresets.CwProfileKey);
                Check(!String.IsNullOrEmpty(carProfile) && Shown(ProjectPresets.CarProfileKey) == carProfile, "The car profile shows its recommendation: " + carProfile);
                Check(Shown(ProjectPresets.CwProfileKey) == cwProfile, "The counterweight profile shows its recommendation: " + cwProfile);
                Check(Shown(ProjectPresets.MachineOnRailsKey) == (Library().GuideRailsOrDefault().MachineOnRails ? "Yes" : "No"), "Machine on the rails shows the library value");
                double carSpacing = Convert.ToDouble(GuideRailRecommendation.ForField(Ctx(), ProjectPresets.CarSpacingKey), System.Globalization.CultureInfo.InvariantCulture);
                Check(Math.Abs(double.Parse(Spacing(ProjectPresets.CarSpacingKey), System.Globalization.CultureInfo.InvariantCulture) - carSpacing) < 1e-9, "The car spacing shows its recommendation: " + carSpacing);
                Check(Project().UserEditedFields == null || !Project().UserEditedFields.Any(k => k.StartsWith("Rails.")), "Nothing is locked on a new project");
                Check(Project().Rails.BracketMaximumSpacingMm.HasValue && Math.Abs(Project().Rails.BracketMaximumSpacingMm.Value - carSpacing) < 1e-9, "The maximum bracket spacing of the drawings follows the car spacing");
                Check(Project().Rails.CarProfileShown() == carProfile && Project().Rails.CwProfileShown() == cwProfile, "The recommended profiles are in the project");
                await Shot("01-rails-new");
                // ---------- lock and Reset ----------
                var profiles = Library().GuideRailsOrDefault().Profiles.Select(p => p.Name).ToList();
                string other = profiles.Last(p => p != carProfile);
                Choices()[ProjectPresets.CarProfileKey].SelectedValue = other; await Ready();
                Check(Edited(ProjectPresets.CarProfileKey) && Project().Rails.CarProfile == other, "Choosing another car profile locks it");
                Check(Project().Rails.CarProfileShown() == other, "The chosen profile is the one the drawings use");
                Check(Edited(ProjectPresets.CarProfileKey) && !Edited(ProjectPresets.CarSpacingKey), "The spacing is still unlocked");
                double followed = Convert.ToDouble(GuideRailRecommendation.ForField(Ctx(), ProjectPresets.CarSpacingKey), System.Globalization.CultureInfo.InvariantCulture);
                Check(Math.Abs(double.Parse(Spacing(ProjectPresets.CarSpacingKey), System.Globalization.CultureInfo.InvariantCulture) - followed) < 1e-9, "An unlocked spacing follows the chosen profile");
                Reset(Choices()[ProjectPresets.CarProfileKey]); await Ready();
                Check(!Edited(ProjectPresets.CarProfileKey) && Shown(ProjectPresets.CarProfileKey) == carProfile, "Reset gives the recommended profile back");
                // ---------- typed spacing and live follow ----------
                Type(ProjectPresets.CarSpacingKey, "1250"); await Ready();
                Check(Edited(ProjectPresets.CarSpacingKey) && Project().Rails.CarSpacingMm == 1250, "Typing the car spacing locks it");
                Check(Project().Rails.BracketMaximumSpacingMm == 1250, "The maximum bracket spacing follows the typed car spacing");
                Reset(Editors()[ProjectPresets.CarSpacingKey]); await Ready();
                Check(!Edited(ProjectPresets.CarSpacingKey) && Math.Abs(double.Parse(Spacing(ProjectPresets.CarSpacingKey), System.Globalization.CultureInfo.InvariantCulture) - carSpacing) < 1e-9, "Reset gives the recommended spacing back");
                // ---------- a failing choice is kept and the page shows no warning ----------
                Choices()[ProjectPresets.CarProfileKey].SelectedValue = profiles.First(); await Ready();
                Type(ProjectPresets.CarSpacingKey, "3000"); await Ready();
                var result = Check_();
                Check(result.Available && !result.Car.Ok, "The smallest profile at 3000 mm does not pass");
                Check(Project().Rails.CarProfile == profiles.First() && Project().Rails.CarSpacingMm == 3000, "The failing choice is kept");
                var text = Tree(window).OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? "").ToList();
                Check(!text.Any(t => t.IndexOf("NOT OK", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("exceed", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("review", StringComparison.OrdinalIgnoreCase) >= 0),
                    "The page shows no warning text for a failing choice");
                var sheet = CalculationSheet.Build(Project(), Context(), new CalculationSheetInputs { Presets = Library() });
                Check(sheet.GuideRailsAvailable && !sheet.GuideRails.Car.Ok, "The failing result is in the calculation sheet");
                await Shot("02-rails-failing-choice");
                Reset(Editors()[ProjectPresets.CarSpacingKey]); Reset(Choices()[ProjectPresets.CarProfileKey]); await Ready();
                // ---------- machine on the rails ----------
                bool machineBefore = Library().GuideRailsOrDefault().MachineOnRails;
                Choices()[ProjectPresets.MachineOnRailsKey].SelectedValue = machineBefore ? "No" : "Yes"; await Ready();
                Check(Edited(ProjectPresets.MachineOnRailsKey) && Project().Rails.MachineOnRails == !machineBefore, "Machine on the rails locks when changed");
                Reset(Choices()[ProjectPresets.MachineOnRailsKey]); await Ready();
                Check(!Edited(ProjectPresets.MachineOnRailsKey), "Machine on the rails resets");
                // ---------- Advanced ----------
                await Expand();
                foreach (var k in new[] { "Rails.MachineRailCount", "Rails.ClipPushThroughN", "Rails.CwShoeSpacingMm", "Rails.StructureDeflectionMm", "Rails.CarShoeLiningMm", "Rails.CwShoeLiningMm" })
                    Check(Editors().ContainsKey(k) && Editors()[k].Text != "", "Advanced value " + k + " shows the library value");
                foreach (var k in new[] { "Rails.CarShoeType", "Rails.CwShoeType", "Rails.A5Band" })
                    Check(Choices().ContainsKey(k) && Choices()[k].SelectedValue != null, "Advanced list " + k + " shows the catalog value");
                var lib = Library().GuideRailsOrDefault();
                Check(Editors()["Rails.MachineRailCount"].Text == lib.MachineRailCount.ToString() && Editors()["Rails.CwShoeSpacingMm"].Text == lib.CwShoeSpacingMm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "The Advanced numbers are the library values");
                Check(Project().UserEditedFields == null || !Project().UserEditedFields.Any(k => k.StartsWith("Rails.")), "Nothing is locked by showing the Advanced values");
                double fr0 = GuideRailCheckEngine.Calculate(Project(), Context(), Library()).Inputs.Preset.ClipPushThroughN;
                Type("Rails.ClipPushThroughN", "5000"); await Ready();
                Check(Edited("Rails.ClipPushThroughN") && Project().Rails.ClipPushThroughN == 5000, "Typing an Advanced value locks it");
                Check(Check_().Inputs.Preset.ClipPushThroughN == 5000 && fr0 == lib.ClipPushThroughN, "The check uses the edited value, the library value is untouched");
                Reset(Editors()["Rails.ClipPushThroughN"]); await Ready();
                Check(!Edited("Rails.ClipPushThroughN") && Check_().Inputs.Preset.ClipPushThroughN == lib.ClipPushThroughN, "Reset gives the library value back");
                string shoe = lib.Profile(carProfile).ShoeType, otherShoe = shoe == RailProfile.Sliding ? RailProfile.Roller : RailProfile.Sliding;
                Choices()["Rails.CarShoeType"].SelectedValue = otherShoe; await Ready();
                Check(Edited("Rails.CarShoeType") && Check_().Car.Profile.ShoeType == otherShoe && Check_().Counterweight.Profile.ShoeType == lib.Profile(Check_().Counterweight.Profile.Name).ShoeType, "The car guide-shoe type changes the car line only");
                Reset(Choices()["Rails.CarShoeType"]); await Ready();
                Check(!Edited("Rails.CarShoeType") && Check_().Car.Profile.ShoeType == shoe, "The shoe type resets to the catalog value");
                await Shot("03-rails-advanced");
                // ---------- values shown only while they matter ----------
                Check(Choices()[ProjectPresets.MachineOnRailsKey].SelectedValue as string == "No" && !Editors()["Rails.MachineRailCount"].IsVisible, "Machine on the rails = No hides the number of rails carrying the machine");
                Choices()[ProjectPresets.MachineOnRailsKey].SelectedValue = "Yes"; await Ready();
                Check(Editors()["Rails.MachineRailCount"].IsVisible && Editors()["Rails.MachineRailCount"].Text == "4", "Machine on the rails = Yes shows the number of rails with its preset 4");
                Reset(Choices()[ProjectPresets.MachineOnRailsKey]); await Ready();
                Check(!Editors()["Rails.MachineRailCount"].IsVisible, "Back to No: hidden again");
                Check(!Editors()["Rails.ClipPushThroughN"].IsVisible, "The clip push-through force is hidden while the travel is within 40 m");
                Check(Editors()["Rails.CarShoeLiningMm"].IsVisible, "The car lining length is shown for sliding shoes");
                Choices()["Rails.CarShoeType"].SelectedValue = RailProfile.Roller; await Ready();
                Check(!Editors()["Rails.CarShoeLiningMm"].IsVisible, "The car lining length is hidden for roller shoes");
                Reset(Choices()["Rails.CarShoeType"]); await Ready();
                // ---------- the counterweight spacing follows the car spacing ----------
                Type(ProjectPresets.CarSpacingKey, "1250"); await Ready();
                Check(!Edited(ProjectPresets.CwSpacingKey) && Spacing(ProjectPresets.CwSpacingKey) == "1250" && Project().Rails.CwSpacingMm == 1250, "An unlocked counterweight spacing follows a typed car spacing");
                Type(ProjectPresets.CwSpacingKey, "1500"); await Ready();
                Check(Edited(ProjectPresets.CwSpacingKey) && Project().Rails.CwSpacingMm == 1500, "Typing the counterweight spacing locks it");
                Type(ProjectPresets.CarSpacingKey, "1000"); await Ready();
                Check(Project().Rails.CwSpacingMm == 1500 && Spacing(ProjectPresets.CwSpacingKey) == "1500", "A locked counterweight spacing no longer follows");
                Reset(Editors()[ProjectPresets.CwSpacingKey]); await Ready();
                Check(!Edited(ProjectPresets.CwSpacingKey) && Spacing(ProjectPresets.CwSpacingKey) == "1000", "Reset brings the follow back");
                Reset(Editors()[ProjectPresets.CarSpacingKey]); await Ready();
                // ---------- one bracket per slab: T70/65/9, Q 480, 3000 mm (no clamp, no rejection) ----------
                ProjectPresets.Lock(Project(), "Elevator.CapacityKg"); Project().Elevator.CapacityKg = 480;
                Call("ApplyProjectPresets"); await Ready();
                Choices()[ProjectPresets.CarProfileKey].SelectedValue = "T70/65/9"; await Ready();
                Type(ProjectPresets.CarSpacingKey, "3000"); await Ready();
                Check(Project().Rails.CarSpacingMm == 3000 && Project().Rails.CwSpacingMm == 3000 && Project().Rails.BracketMaximumSpacingMm == 3000, "3000 mm is kept as the car spacing, the counterweight spacing and the bracket maximum");
                var big = Check_();
                Check(big.Available && big.Car.SpacingMm == 3000 && big.Car.Profile.Name == "T70/65/9", "The check runs with T70/65/9 at 3000 mm, Q 480");
                foreach (var cs in big.Car.Cases)
                    foreach (var it in cs.Items.Where(i => i.Id == "sm" || i.Id == "scomp" || i.Id == "sbuck" || i.Id == "sf" || i.Id == "dx" || i.Id == "dy" || i.Id == "lambda"))
                        numbers.Add("Car, " + cs.Title + ", " + it.Title + " = " + it.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + (it.Permissible.HasValue ? " (permissible " + it.Permissible.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ") " + (it.Ok == true ? "OK" : "NOT OK") : ""));
                numbers.Add("Car line: " + (big.Car.Ok ? "OK" : "NOT OK") + "; counterweight line T50/50/5-type check at 3000: " + (big.Counterweight.Ok ? "OK" : "NOT OK") + " (" + big.Counterweight.Profile.Name + ")");
                var aa3 = LegacyProjectAdapter.ToSectionAA(Project());
                Check(aa3.BracketMaximum == 3000, "Section A-A takes 3000 mm");
                var d3 = new ElevatorDesignEngine().Calculate(Project(), Context(), DrawingViews.All);
                Check(d3.CanGenerate && d3.SectionAA != null, "The design with 3000 mm generates (no validation block)");
                Check(d3.SectionAA.Brackets.ConsecutiveSpacings.Count > 0 && d3.SectionAA.Brackets.ConsecutiveSpacings.All(s => s <= 3000 + 1e-7), "The A-A bracket layout is built at 3000 mm");
                sheet = CalculationSheet.Build(Project(), Context(), new CalculationSheetInputs { Presets = Library() });
                Check(sheet.GuideRailsAvailable && sheet.GuideRails.Car.SpacingMm == 3000, "The calculation sheet carries 3000 mm");
                Reset(Editors()[ProjectPresets.CarSpacingKey]); Reset(Choices()[ProjectPresets.CarProfileKey]); await Ready();
                // ---------- the chosen values everywhere ----------
                Choices()[ProjectPresets.CwProfileKey].SelectedValue = profiles.Last(); await Ready();
                var design = new ElevatorDesignEngine().Calculate(Project(), Context(), DrawingViews.Plan);
                Check(design.CarRailProfile == Project().Rails.CarProfileShown() && design.CounterweightRailProfile == profiles.Last(), "The design result carries the chosen profiles");
                sheet = CalculationSheet.Build(Project(), Context(), new CalculationSheetInputs { Presets = Library() });
                var proportional = ProportionalRailSection.Parse(profiles.Last());
                Check(sheet.CounterweightRail is ProportionalRailSection && proportional != null && Math.Abs(((ProportionalRailSection)sheet.CounterweightRail).BaseWidthMm - proportional.BaseWidthMm) < 1e-9, "The sheet figure of the counterweight rail is the chosen profile");
                var legacy = LegacyProjectAdapter.ToLegacy(Project());
                Check(legacy.CarRailProfile == Project().Rails.CarProfileShown() && legacy.CounterweightRailProfile == profiles.Last(), "The plan input carries the chosen profiles");
                var aa = LegacyProjectAdapter.ToSectionAA(Project());
                Check(aa.CarRailProfile == Project().Rails.CarProfileShown() && aa.CwRailProfile == profiles.Last() && aa.BracketMaximum == Project().Rails.BracketMaximumSpacingMm, "Section A-A takes the chosen profiles and the bracket spacing");
                Reset(Choices()[ProjectPresets.CwProfileKey]); await Ready();
                foreach (var page in Pages) { Call("ShowPage", page); await Ready(); }
                Check(true, "All 11 pages still build");
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString(), Numbers = numbers, Checks = names }); }
            finally
            {
                Wire.Write(Path.Combine(output, "rails-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Numbers = numbers, Checks = names, Method = "Real WPF controls driven by programmatic events. Not mouse/keyboard testing." });
                window.Closing += (s, ce) => { };
                window.Close();
            }
        };
        application.Run(window);
        return exit;
    }
}
