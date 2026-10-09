using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Bridge;

// Tier-3 golden capture (ARCHITECTURE-PLAN P0): the default new project (golden fixture new-project-q630, presets applied as
// in the application) generated as all four native views through the packaged AutoCAD host. Approved: the generated and
// reopened entity counts per view and the project JSON read back from the saved DWG (member order normalised, see tests/golden/JsonMembers.cs). Mode: compare (default) or approve
// (initial baseline only: tests/golden/approved-native must be empty).
public static class NativeGoldenTests
{
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), bin = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]), mode = args.Length > 3 ? args[3] : "compare";
        string approved = Path.Combine(root, "tests", "golden", "approved-native");
        Directory.CreateDirectory(output);
        var mismatches = new List<string>(); int files = 0; int exit = 1;
        try
        {
            if (mode == "approve" && Directory.Exists(approved) && Directory.EnumerateFileSystemEntries(approved).Any()) throw new InvalidOperationException("Approve creates the initial baseline only; the folder is not empty: " + approved);
            var ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json"));
            ctx.ChassisEvidence = Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin, "reviewed-chassis-evidence.json"));
            var lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
            var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(root, "tests", "golden", "fixtures", "new-project-q630.elevator.json")));
            ProjectPresets.Apply(p, lib, ctx);
            var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
            var target = Path.Combine(output, "NewProject-AllFour.dwg");
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
            Wire.Write(Path.Combine(output, "generation.json"), response);
            if (!response.Success) throw new InvalidOperationException("Native generation failed: " + response.Error);
            var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
            var js = new JavaScriptSerializer();
            var counts = new StringBuilder();
            counts.Append("Views generated: ").Append(String.Join(", ", response.ViewsGenerated)).Append('\n');
            foreach (var k in response.EntityCounts.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                int reopened; verified.EntityCounts.TryGetValue(k, out reopened);
                counts.Append(k).Append(": generated ").Append(response.EntityCounts[k]).Append(", reopened ").Append(verified.EntityCounts.ContainsKey(k) ? reopened.ToString() : "-").Append('\n');
            }
            var captures = new Dictionary<string, byte[]>
            {
                { "new-project-q630/entity-counts.txt", Utf8.GetBytes(counts.ToString()) },
                { "new-project-q630/reopened-project.elevator.json", Utf8.GetBytes(JsonMembers.Sort(File.ReadAllText(Path.Combine(client.LastJobDirectory, "reopened-project.elevator.json"), Utf8))) }
            };
            foreach (var c in captures)
            {
                files++;
                string a = Path.Combine(approved, c.Key.Replace('/', Path.DirectorySeparatorChar)), got = Path.Combine(output, "captured", c.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(got)); File.WriteAllBytes(got, c.Value);
                if (mode == "approve") { Directory.CreateDirectory(Path.GetDirectoryName(a)); File.WriteAllBytes(a, c.Value); continue; }
                if (!File.Exists(a) || !File.ReadAllBytes(a).SequenceEqual(c.Value)) { mismatches.Add(c.Key); File.WriteAllBytes(got + ".received", c.Value); }
            }
            exit = mismatches.Count == 0 ? 0 : 1;
            Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + files + " native golden files " + (mode == "approve" ? "approved" : "compared") + (mismatches.Count > 0 ? "; mismatches: " + String.Join(", ", mismatches) : "") + ".");
        }
        catch (Exception e) { Wire.Write(Path.Combine(output, "native-golden-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); exit = 1; }
        Wire.Write(Path.Combine(output, "native-golden-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Mode = mode, Files = files, Mismatches = mismatches });
        return exit;
    }
}
