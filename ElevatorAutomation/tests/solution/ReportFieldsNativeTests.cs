using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

// Generates the same project twice as native four-view DWGs, once with the optional calculation-sheet report fields
// blank and once filled (client, document number, revision, names), and reopens each in a fresh AutoCAD Core Console.
// The report fields belong to the Word calculation sheet only: the drawing geometry and entity counts must be identical.
public static class ReportFieldsNativeTests
{
    static string bin, output, root;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        ++count;
        names.Add(name);
    }
    static ElevatorProject Project(bool filled)
    {
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        p.Info.Name = "SYNTHETIC REPORT FIELDS NATIVE REGRESSION — NOT PROJECT DEFAULTS";
        p.Report = filled ? new ProjectReportDetails
        {
            ClientName = "Release Check Client", DocumentNumber = "RC-CS-001", Revision = "B",
            PreparedBy = "A. Preparer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver"
        } : null;
        return p;
    }
    sealed class Run { public GenerationResponse Response, Verified; public ElevatorProject Reopened; }
    static Run Generate(bool filled, string label)
    {
        var p = Project(filled);
        var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
        var target = Path.Combine(output, label + "-AllFour.dwg");
        var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
        var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
        Wire.Write(Path.Combine(output, label + "-generation.json"), response);
        Check(response.Success && response.ViewsGenerated.Length == 4 && File.Exists(target), label + ": four native views generated and saved");
        var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
        Check(verified.Success && verified.InputHash == Wire.HashText(request.ProjectJson), label + ": fresh reopen verification matches the exact input snapshot");
        Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)), label + ": generated and reopened entity counts match");
        var reopened = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(client.LastJobDirectory, "reopened-project.elevator.json")));
        string evidence = Path.Combine(output, label + "-job-evidence");
        Directory.CreateDirectory(evidence);
        foreach (string name in new[] { "request.json", "project.elevator.json", "reopened-project.elevator.json", "response.json", "verified.json", "generate.log", "verify.log" })
            File.Copy(Path.Combine(client.LastJobDirectory, name), Path.Combine(evidence, name), false);
        return new Run { Response = response, Verified = verified, Reopened = reopened };
    }
    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        bin = Path.GetFullPath(args[1]);
        output = Path.GetFullPath(args[2]);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Output folder is not empty: " + output); return 2; }
        Directory.CreateDirectory(output);
        int exit = 1;
        try
        {
            var blank = Generate(false, "ReportFieldsBlank");
            var filled = Generate(true, "ReportFieldsFilled");
            Check(blank.Response.ViewsGenerated.SequenceEqual(filled.Response.ViewsGenerated), "Same four views generated with and without report fields");
            Check(blank.Response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(filled.Response.EntityCounts.OrderBy(k => k.Key)), "Entity counts identical with and without report fields (" + blank.Response.EntityCounts.Count + " categories, " + blank.Response.EntityCounts.Values.Sum() + " entities)");
            Check(blank.Verified.EntityCounts.OrderBy(k => k.Key).SequenceEqual(filled.Verified.EntityCounts.OrderBy(k => k.Key)), "Reopened entity counts identical with and without report fields");
            Check(!String.IsNullOrEmpty(blank.Response.DesignJson) && blank.Response.DesignJson == filled.Response.DesignJson, "Drawing design geometry (DesignJson) identical with and without report fields");
            Check(blank.Response.LayoutStatus == filled.Response.LayoutStatus && blank.Response.WrapAngleStatus == filled.Response.WrapAngleStatus && blank.Response.CodeDiameterRatioStatus == filled.Response.CodeDiameterRatioStatus, "Layout, wrap and diameter-ratio statuses identical");
            var r = filled.Reopened.Report;
            Check(r != null && r.ClientName == "Release Check Client" && r.DocumentNumber == "RC-CS-001" && r.Revision == "B" && r.PreparedBy == "A. Preparer" && r.CheckedBy == "B. Checker" && r.ApprovedBy == "C. Approver", "Filled report fields survive DWG save and reopen in the unified project");
            Check(blank.Reopened.Report == null || (String.IsNullOrEmpty(blank.Reopened.Report.ClientName) && String.IsNullOrEmpty(blank.Reopened.Report.DocumentNumber)), "Blank report fields stay blank after reopen");
            Wire.Write(Path.Combine(output, "entity-counts.json"), new { Blank = blank.Response.EntityCounts, Filled = filled.Response.EntityCounts, BlankReopened = blank.Verified.EntityCounts, FilledReopened = filled.Verified.EntityCounts });
            exit = 0;
        }
        catch (Exception error) { Wire.Write(Path.Combine(output, "report-fields-native-error.json"), new { Error = error.ToString() }); }
        finally
        {
            Wire.Write(Path.Combine(output, "report-fields-native-tests.json"), new
            {
                Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names,
                Method = "Two four-view DWGs of the same synthetic project (report fields blank / filled) generated through the packaged GenerationClient and reopened in fresh AutoCAD Core Console processes; entity counts, design geometry JSON and statuses compared; unified project read back from each DWG."
            });
        }
        Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + count + " report-field native assertions.");
        return exit;
    }
}
