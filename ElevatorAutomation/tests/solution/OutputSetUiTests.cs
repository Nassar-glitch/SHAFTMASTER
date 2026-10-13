using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Elevator.Core.Application;
using Elevator.Core.Company;
using Elevator.Core.Layouts;
using Elevator.Core.Project;
using Elevator.Solution.Desktop;
using Elevator.Solution.Bridge;

// L3 tier 2 (output-set-ui): the Generate Drawings flow of the real window. One run writes the DWG with the sheet, its PDF and the
// project file under one base name; a second run into the same folder asks (overwrite / next revision / cancel) and never replaces
// silently; the next revision raises the project revision and writes a second set; cancel writes nothing; a PDF step that fails keeps
// the DWG and the project file and says what failed. Needs AutoCAD 2023 (every run generates through the Core Console).
public static class OutputSetUiTests
{
    static MainWindow window; static string output; static int checks; static readonly List<string> names = new List<string>();
    static object Get(string name) { return window.Automation.Get(name); }
    static void Set(string name, object value) { window.Automation.Set(name, value); }
    static object Call(string name, params object[] arguments) { return window.Automation.Call(name, arguments); }
    static ProjectSession Session() { return (ProjectSession)Get("session"); }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); ++checks; names.Add(name); }
    static async Task Ready() { window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout(); }
    static string Hash(string path) { return Wire.HashFile(path); }

    [STAThread]
    public static int Main(string[] arguments)
    {
        output = arguments[0];
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; }
        Directory.CreateDirectory(output);
        var application = new Application(); window = new MainWindow(new string[0]);
        int exit = 1;
        window.Loaded += async (sender, e) =>
        {
            try
            {
                window.Width = 1280; window.Height = 900; await Ready();
                var p = Session().Project;
                p.Info.Name = "OUTPUT SET UI TEST";
                p.Report = new ProjectReportDetails { ClientName = "Delta Developments", DocumentNumber = "NL-UI", Revision = "", PreparedBy = "A. Engineer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver" };
                string folder = Path.Combine(output, "set");
                Set("outputFolder", folder);
                Call("ShowPage", "DRAWINGS"); await Ready();
                var company = Elevator.Reporting.Sheet.CompanyReportSettings.Load().ToCompanySettings();
                OutputSetPlan lastPlan = null;
                OutputChoice answer = OutputChoice.Cancel;
                int asked = 0;
                Set("outputPrompt", (Func<OutputSetPlan, OutputChoice>)(plan => { lastPlan = plan; asked++; return answer; }));

                // ---------- 1. a free folder: no question, three files, one base name ----------
                string base00 = DrawingFileName.BaseName(p, company);
                Check(base00.EndsWith("-MODEL00"), "The base name of a new project ends with MODEL00 (" + base00 + ")");
                await (Task)Call("Generate");
                Check(asked == 0, "A free output folder gives no question");
                var set = (OutputSetResult)Get("lastSet");
                Check(set != null && set.Generation.Success && set.PdfError == null && set.ProjectError == null, "The first run writes the whole set" + (set != null ? " (" + set.PdfError + set.ProjectError + ")" : ""));
                Check(Path.GetFileName(set.Dwg) == base00 + ".dwg" && Path.GetFileName(set.Pdf) == base00 + ".pdf" && Path.GetFileName(set.ProjectFile) == base00 + ".elevator.json", "The DWG, the PDF and the project file share the base name");
                Check(File.Exists(set.Dwg) && File.Exists(set.Pdf) && File.Exists(set.ProjectFile) && Directory.GetFiles(folder).Length == 3, "Exactly the three files exist in the chosen folder");
                Check(set.Generation.Sheet != null && set.Generation.Sheet.LayoutName == SheetLayout.Name, "The DWG carries the sheet layout");
                string status = Convert.ToString(((System.Windows.Controls.TextBlock)Get("status")).ToolTip);
                Check(status.Contains(set.Dwg) && status.Contains(set.Pdf) && status.Contains(set.ProjectFile), "The status reports the three paths (" + status + ")");
                Check(Get("outputFolderButton") != null && ((System.Windows.Controls.Button)Get("outputFolderButton")).Visibility == Visibility.Visible, "The Open Output Folder action is offered");
                string h00 = Hash(set.Dwg), p00 = Hash(set.Pdf), j00 = Hash(set.ProjectFile);

                // ---------- 2. the same folder again: asked, next revision ----------
                answer = OutputChoice.NextRevision; asked = 0;
                await (Task)Call("Generate");
                Check(asked == 1 && lastPlan != null && lastPlan.Conflict && lastPlan.Existing.Count == 3 && lastPlan.NextRevision == 1, "Existing files are asked about, with the next revision 01 offered");
                var set01 = (OutputSetResult)Get("lastSet");
                string base01 = base00.Substring(0, base00.Length - 2) + "01";
                Check(set01.Success && Path.GetFileName(set01.Dwg) == base01 + ".dwg" && File.Exists(set01.Pdf) && File.Exists(set01.ProjectFile), "The next revision writes a second set under MODEL01");
                Check(Hash(set.Dwg) == h00 && Hash(set.Pdf) == p00 && Hash(set.ProjectFile) == j00, "The first set is untouched");
                Check(Session().Project.Report.Revision == "01", "The project revision is raised to 01");
                Check(ProjectJson.Deserialize(File.ReadAllText(set01.ProjectFile)).Report.Revision == "01", "The project file of the second set holds revision 01");

                // ---------- 3. cancel: nothing is written ----------
                answer = OutputChoice.Cancel; asked = 0;
                var before = Directory.GetFiles(folder).Select(f => f + "|" + File.GetLastWriteTimeUtc(f).Ticks).OrderBy(x => x).ToArray();
                await (Task)Call("Generate");
                var after = Directory.GetFiles(folder).Select(f => f + "|" + File.GetLastWriteTimeUtc(f).Ticks).OrderBy(x => x).ToArray();
                Check(asked == 1 && before.SequenceEqual(after), "Cancel writes nothing");

                // ---------- 4. overwrite: the same names, replaced after the new set is verified ----------
                answer = OutputChoice.Overwrite; asked = 0;
                var stampBefore = File.GetLastWriteTimeUtc(set01.Pdf);
                await Task.Delay(1100);
                await (Task)Call("Generate");
                var set01b = (OutputSetResult)Get("lastSet");
                Check(asked == 1 && set01b.Success && set01b.Dwg == set01.Dwg && File.GetLastWriteTimeUtc(set01b.Pdf) > stampBefore, "Overwrite replaces the three files of the same revision");
                Check(Directory.GetFiles(folder).Length == 6, "Overwrite adds no file");

                // ---------- 5. a PDF that cannot be written does not lose the DWG ----------
                string folder2 = Path.Combine(output, "pdf-fails");
                Set("outputFolder", folder2); Call("ShowPage", "GENERAL"); Call("ShowPage", "DRAWINGS"); await Ready(); // the page shows the folder it commits
                Directory.CreateDirectory(Path.Combine(folder2, base01 + ".pdf")); // a directory where the PDF goes
                Session().Project.Report.Revision = "01";
                answer = OutputChoice.Overwrite; asked = 0;
                await (Task)Call("Generate");
                var broken = (OutputSetResult)Get("lastSet");
                Check(broken.Generation.Success && File.Exists(broken.Dwg) && File.Exists(broken.ProjectFile), "A failing PDF step still leaves the DWG and the project file (project error: " + broken.ProjectError + ", pdf error: " + broken.PdfError + ", gen " + broken.Generation.Success + ", dwg " + File.Exists(broken.Dwg) + ")");
                Check(!String.IsNullOrEmpty(broken.PdfError), "The result says what failed (" + broken.PdfError + ") asked=" + asked + " same=" + Object.ReferenceEquals(broken, set01b) + " status=" + Convert.ToString(((System.Windows.Controls.TextBlock)Get("status")).ToolTip));
                Check(((System.Windows.Controls.TextBlock)Get("status")).Text.Contains("PDF was not written"), "The status tells that the PDF was not written");
                exit = 0;
            }
            catch (Exception ex) { Wire.Write(Path.Combine(output, "ui-error.json"), new { Error = ex.ToString(), Checks = names }); }
            finally
            {
                Wire.Write(Path.Combine(output, "output-set-ui-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = checks, Checks = names, Method = "Real WPF window driven through its test facade; generation through the Core Console." });
                window.Closing += (s, ce) => { };
                window.Close();
            }
        };
        application.Run(window);
        Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + checks + " output set UI assertions.");
        return exit;
    }
}
