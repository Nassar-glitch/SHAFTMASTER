using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Layouts;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

// L2c: regenerates a project (a saved elevator.json) with the A0 sheet and the title-block values of an earlier generation, through the
// packaged GenerationClient and the Core Console host: the user's own project, to compare the new sheet with his edit.
// usage: L2cRegenerate <root> <bin> <project.elevator.json> <title-block.json> <logo.png|-> <logoWidthPx> <logoHeightPx> <out.dwg>
public static class L2cRegenerate
{
    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), bin = Path.GetFullPath(args[1]), outDwg = Path.GetFullPath(args[7]);
        var project = ProjectJson.Deserialize(File.ReadAllText(args[2]));
        var attributes = Wire.Read<List<TitleBlockAttribute>>(args[3]);
        foreach (var a in attributes)
        {
            a.Value = a.Style == XarabText.ArabicStyle ? XarabText.Decode(a.DrawingText ?? "") : (a.DrawingText ?? "");
            a.Script = a.Style == XarabText.ArabicStyle ? TextScript.Arabic : TextScript.Latin;
        }
        var sheet = new SheetRequest { Attributes = attributes, DoorType = project.Doors == null ? null : project.Doors.Type };
        if (args[4] != "-")
        {
            sheet.LogoFile = args[4];
            sheet.LogoWidthPx = Int32.Parse(args[5]);
            sheet.LogoHeightPx = Int32.Parse(args[6]);
        }
        var request = new GenerationRequest
        {
            ProjectJson = ProjectJson.Serialize(project),
            Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true },
            Sheet = sheet
        };
        var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
        if (File.Exists(outDwg))
            File.Delete(outDwg);
        var response = client.Run(request, OutputConsent.Capture(outDwg, false), Console.WriteLine);
        Wire.Write(Path.ChangeExtension(outDwg, ".response.json"), response);
        Console.WriteLine(response.Success ? "OK " + outDwg : "FAIL " + response.Error);
        return response.Success ? 0 : 1;
    }
}
