using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Bridge;

// Exercises the packaged STEP input path and native AutoCAD generation/reopen.
// Does not claim a SOLIDWORKS export or replace the independent FF equation tests.
public static class CarGuideForceIntegrationTests
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
    static ElevatorProject Project()
    {
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        p.Info.Name = "SYNTHETIC FF NATIVE / STEP REGRESSION — NOT PROJECT DEFAULTS";
        p.Calculations.Mode = "Basic";
        return p;
    }
    static string Canonical(object value)
    {
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        var map = value as IDictionary<string, object>;
        if (map != null) return "{" + String.Join(",", map.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => serializer.Serialize(k) + ":" + Canonical(map[k]))) + "}";
        var list = value as System.Collections.IEnumerable;
        if (list != null && !(value is string)) return "[" + String.Join(",", list.Cast<object>().Select(Canonical)) + "]";
        return serializer.Serialize(value);
    }
    static string ModelJson(ElevatorProject p)
    {
        ElevatorDesignResult design;
        var parameters = StepInputFactory.Create(p, Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json")),
            Wire.Read<StepModelParameters>(Path.Combine(bin, "preliminary-3d-reference.json")), out design);
        Check(design.CanGenerate && design.Plan != null && design.SectionAA != null,
            "STEP shared design remains available with h=" + (p.Calculations.CarGuideForces == null ? "absent group" : Convert.ToString(p.Calculations.CarGuideForces.GuideShoeSpacingMm)));
        return Canonical(new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(Wire.Json(parameters)));
    }
    static void Native(double? spacing, string label)
    {
        var p = Project();
        p.Calculations.CarGuideForces = new CarGuideForceInputs { GuideShoeSpacingMm = spacing };
        var options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true };
        var target = Path.Combine(output, label + "-AllFour.dwg");
        var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = options };
        var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
        var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
        Wire.Write(Path.Combine(output, label + "-generation.json"), response);
        Check(response.Success && response.ViewsGenerated.Length == 4 && File.Exists(target),
            label + ": all four native views generated and reopened successfully");
        var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
        Check(verified.Success && verified.InputHash == Wire.HashText(request.ProjectJson),
            label + ": fresh native verification matches exact input snapshot");
        var reopened = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(client.LastJobDirectory, "reopened-project.elevator.json")));
        Check(reopened.Calculations.CarGuideForces != null &&
              reopened.Calculations.CarGuideForces.GuideShoeSpacingMm == spacing && reopened.Calculations.Mode == "Basic",
            label + ": unified DWG project retains nullable h and selected mode after save/reopen");
        Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)),
            label + ": native generated/reopened semantic component counts match");
        string evidence = Path.Combine(output, label + "-job-evidence");
        Directory.CreateDirectory(evidence);
        foreach (string name in new[] { "request.json", "project.elevator.json", "reopened-project.elevator.json", "response.json", "verified.json", "generate.log", "verify.log" })
            File.Copy(Path.Combine(client.LastJobDirectory, name), Path.Combine(evidence, name), false);
    }
    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        bin = Path.GetFullPath(args[1]);
        output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        int exit = 1;
        try
        {
            var p = Project();
            string baseline = ModelJson(p);
            p.Calculations.CarGuideForces = new CarGuideForceInputs();
            foreach (double? h in new double?[] { null, 1200, 0, -1, Double.NaN, Double.PositiveInfinity })
            {
                p.Calculations.CarGuideForces.GuideShoeSpacingMm = h;
                Check(ModelJson(p) == baseline, "FF h does not alter any STEP model parameter: " + Convert.ToString(h));
            }
            File.WriteAllText(Path.Combine(output, "step-parameters-without-ff.json"), baseline);
            p.Calculations.CarGuideForces.GuideShoeSpacingMm = 1200;
            File.WriteAllText(Path.Combine(output, "step-parameters-with-ff.json"), ModelJson(p));
            Native(1200, "StoredFFInput");
            Native(null, "MissingOrMalformedDraftCanonicalH");
            exit = 0;
        }
        catch (Exception error) { Wire.Write(Path.Combine(output, "integration-error.json"), new { Error = error.ToString() }); }
        finally
        {
            Wire.Write(Path.Combine(output, "car-guide-force-integration-tests.json"), new
            {
                Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names,
                Method = "Packaged STEP StepInputFactory.Create parameter comparison, without SOLIDWORKS export. Two actual four-view DWGs generated and reopened/audited in fresh AutoCAD Core Console processes; unified project input read back from each DWG. A malformed UI draft is canonical null and validated independently in rendered WPF tests."
            });
        }
        return exit;
    }
}
