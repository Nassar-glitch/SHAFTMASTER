using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Doors;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

// D2: automatic doors in the STEP model. The parameters carry the opening position and the door panels / frame as simple boxes; with
// --export the packaged worker builds the solids in SOLIDWORKS and the verification must show no fault and no intersection.
public static class DoorsStepTests
{
    static string root, bin, output;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++count; names.Add(name); }

    static ElevatorProject Project(string type, double width, double depth, double opening)
    {
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        foreach (var f in p.Building.Floors.Take(p.Building.Floors.Count - 1)) f.HeightToNextMm = 3000;
        p.Shaft.WidthMm = width;
        p.Shaft.DepthMm = depth;
        if (type != null)
        {
            DoorFlow.SetType(p, type);
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, opening);
        }
        return p;
    }

    static StepModelParameters Map(ElevatorProject p, out ElevatorDesignResult design)
    {
        var ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json"));
        var reference = Wire.Read<StepModelParameters>(Path.Combine(bin, "preliminary-3d-reference.json"));
        return StepInputFactory.Create(p, ctx, reference, out design);
    }

    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]); bin = Path.GetFullPath(args[1]); output = Path.GetFullPath(args[2]);
        // --export: SOLIDWORKS export of the 1720 mm cases (a Manual baseline and both types); --export-all adds the 1300 mm case.
        bool exportAll = args.Contains("--export-all"), export = args.Contains("--export") || exportAll;
        int baselineSolids = -1;
        Directory.CreateDirectory(output);
        int exit = 1;
        try
        {
            // The STEP model needs TZ <= 1100 (reviewed two-sheave arrangement): shallow shafts.
            var cases = new[]
            {
                new { Id = "2S-800-1720", Type = DoorTypes.Automatic2S, Width = 1720.0, Depth = 2300.0, Opening = 800.0 },
                new { Id = "2C-800-1720", Type = DoorTypes.Automatic2C, Width = 1720.0, Depth = 2300.0, Opening = 800.0 },
                new { Id = "2S-700-1300", Type = DoorTypes.Automatic2S, Width = 1300.0, Depth = 2300.0, Opening = 700.0 }
            };
            foreach (var c in cases)
            {
                ElevatorDesignResult manualDesign, design;
                var manual = Map(Project(null, c.Width, c.Depth, 0), out manualDesign);
                Check(manual.LandingDoorPanels == null && manual.CarDoorPanels == null && manual.DoorOpeningLeft == 0 && manual.DoorOpeningRight == 0,
                    c.Id + ": a Manual door leaves the automatic door parameters empty");
                var project = Project(c.Type, c.Width, c.Depth, c.Opening);
                var m = Map(project, out design);
                var layout = DoorRules.Evaluate(project, null, design.Plan);
                Check(layout.Fits && layout.OpeningMm == c.Opening, c.Id + ": the door fits");
                Check(Math.Abs(m.DoorOpeningLeft - layout.OpeningLeftX) < 1e-9 && Math.Abs(m.DoorOpeningRight - layout.OpeningRightX) < 1e-9
                    && Math.Abs(m.DoorWidth - c.Opening) < 1e-9, c.Id + ": opening position and width");
                Check(m.LandingDoorPanels.Length == 12 && m.CarDoorPanels.Length == 12, c.Id + ": two landing and two car panels");
                int frames = m.LandingDoorFrame.Length / 6;
                Check(c.Type == DoorTypes.Automatic2C ? frames == 1 : frames >= 2, c.Id + ": frame bodies " + frames);
                // Panels inside the shaft, in front of the car, not beyond the opening by more than the over-travel.
                for (int k = 0; k + 5 < m.CarDoorPanels.Length; k += 6)
                    Check(m.CarDoorPanels[k + 1] + m.CarDoorPanels[k + 3] <= m.CarFront + 1e-9 && m.CarDoorPanels[k] >= 0, c.Id + ": car panel " + (k / 6 + 1) + " in front of the car");
                for (int k = 0; k + 5 < m.LandingDoorPanels.Length; k += 6)
                    Check(m.LandingDoorPanels[k + 1] >= 0 && m.LandingDoorPanels[k + 1] + m.LandingDoorPanels[k + 3] <= layout.LandingZoneMm + 1e-9, c.Id + ": landing panel " + (k / 6 + 1) + " in the landing zone");
                Wire.Write(Path.Combine(output, c.Id + "-parameters.json"), m);
                if (!export || (c.Width < 1700 && !exportAll))
                    continue;
                var client = new StepExportClient(bin);
                int baseSolids = c.Width < 1700 || baselineSolids < 0
                    ? client.ExportAsync(Project(null, c.Width, c.Depth, 0), Path.Combine(output, c.Id + "-manual.step"), false, Console.WriteLine).GetAwaiter().GetResult().Verification.ReopenedSolidCount
                    : baselineSolids;
                if (c.Width >= 1700)
                    baselineSolids = baseSolids;
                var result = client.ExportAsync(project, Path.Combine(output, c.Id + ".step"), false, Console.WriteLine).GetAwaiter().GetResult();
                Wire.Write(Path.Combine(output, c.Id + "-export.json"), result);
                int served = m.ServedFloors == null ? m.FloorElevations.Length : m.ServedFloors.Count(s => s);
                int expected = baseSolids + served * ((m.LandingDoorPanels.Length + m.LandingDoorFrame.Length) / 6 - 2) + (m.CarDoorPanels.Length / 6 - 2);
                Check(result.Verification.FaultCount == 0 && result.Verification.Intersections.Length == 0, c.Id + ": STEP verification: no fault, no intersection");
                Check(result.Verification.ReopenedSolidCount == expected, c.Id + ": solids " + result.Verification.ReopenedSolidCount + " = Manual " + baseSolids + " + door bodies (" + expected + ")");
            }
            exit = 0;
            Console.WriteLine("PASS: " + count + " doors STEP assertions" + (export ? " (SOLIDWORKS export)" : " (parameters only)") + ".");
        }
        catch (Exception e) { Wire.Write(Path.Combine(output, "doors-step-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); }
        Wire.Write(Path.Combine(output, "doors-step-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names, Export = export });
        return exit;
    }
}
