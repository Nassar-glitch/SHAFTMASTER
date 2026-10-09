using System;
using System.IO;
using Elevator.Solution.Bridge;
class SolutionRoofRegression
{
    static int Main(string[] args)
    {
        var request=Wire.Read<GenerationRequest>(args[1]);
        request.RequestId=Guid.NewGuid().ToString("N");
        var client=new GenerationClient(args[0],Path.Combine(args[0],"bin-solution-v01"),@"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
        var result=client.Run(request,OutputConsent.Capture(args[2],false),Console.WriteLine);
        Console.WriteLine("JOB="+client.LastJobDirectory);
        Console.WriteLine("SUCCESS="+result.Success+" OUTPUT="+result.OutputPath);
        Console.WriteLine("VIEWS="+String.Join(",",result.ViewsGenerated??new string[0]));
        Console.WriteLine("LAYOUT="+result.LayoutStatus+" WRAP="+result.WrapAngleStatus+" CODE="+result.CodeDiameterRatioStatus);
        if(!result.Success)Console.WriteLine(result.Error);
        return result.Success?0:1;
    }
}
