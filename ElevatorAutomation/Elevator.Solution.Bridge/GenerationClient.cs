using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Elevator.Core.Project;
namespace Elevator.Solution.Bridge
{
    public sealed class GenerationClient
    {
        public readonly string Root,Bin,ConsolePath;
        public string LastJobDirectory;
        public GenerationClient(string root,string bin,string consolePath){Root=root;Bin=bin;ConsolePath=consolePath;}
        public Task<GenerationResponse> GenerateAsync(ElevatorProject project,GenerationOptions options,OutputConsent consent,Action<string> progress)
        {
            // Snapshot synchronously before background work; further UI changes cannot modify an in-flight request.
            var request=new GenerationRequest{ProjectJson=ProjectJson.Serialize(project),Options=new GenerationOptions{Views=options.Views,ArrangeViews=options.ArrangeViews}};
            return Task.Run(()=>Run(request,consent,progress));
        }
        public GenerationResponse Run(GenerationRequest request,OutputConsent consent,Action<string> progress)
        {
            var timer=Stopwatch.StartNew();
            var failure=new GenerationResponse{RequestId=request.RequestId,InputHash=Wire.HashText(request.ProjectJson)};
            try
            {
                var project=Wire.Validate(request);
                var issues=ProjectValidator.Validate(project,request.Options.Views);
                if(issues.Any(i=>i.BlocksGeneration)){failure.Validation=issues;throw new ArgumentException(String.Join("; ",issues.Where(i=>i.BlocksGeneration).Select(i=>i.Message)));}
                if(!File.Exists(ConsolePath))throw new FileNotFoundException("AutoCAD 2023 Core Console is required.",ConsolePath);
                LastJobDirectory=Path.Combine(Root,"output","solution-v01","jobs",request.RequestId);
                Directory.CreateDirectory(LastJobDirectory);
                Wire.Write(Path.Combine(LastJobDirectory,"request.json"),request);
                File.WriteAllText(Path.Combine(LastJobDirectory,"project.elevator.json"),request.ProjectJson);
                string scratch=Path.Combine(LastJobDirectory,"disposable.dwg");
                File.Copy(Path.Combine(Root,"Templates","ExistingPlan_Master.dwg"),scratch,false);
                progress("Generating in a disposable AutoCAD drawing…");
                RunHost(scratch,"generate",LastJobDirectory);
                var result=ReadResponse(request,"response.json");
                if(!result.Success)return result;
                progress("Reopening and auditing the generated DWG in a fresh process…");
                string staged=Path.Combine(LastJobDirectory,"generated.dwg");
                RunHost(staged,"verify",LastJobDirectory);
                var verified=ReadResponse(request,"verified.json");
                if(!verified.Success)return verified;
                progress("Publishing the verified drawing…");
                consent.Publish(staged);
                result.OutputPath=consent.Path;result.ElapsedSeconds=timer.Elapsed.TotalSeconds;
                Wire.Write(Path.Combine(LastJobDirectory,"published.json"),result);
                return result;
            }
            catch(Exception e){failure.Error=e.ToString();failure.ElapsedSeconds=timer.Elapsed.TotalSeconds;return failure;}
        }
        GenerationResponse ReadResponse(GenerationRequest request,string filename)
        {
            var r=Wire.Read<GenerationResponse>(Path.Combine(LastJobDirectory,filename));
            if(r.ProtocolVersion!=1||r.RequestId!=request.RequestId||r.InputHash!=Wire.HashText(request.ProjectJson))throw new InvalidDataException("Host response does not match the project snapshot.");
            return r;
        }
        public void RunHost(string drawing,string mode,string job)
        {
            string plugin=Path.Combine(Bin,"Elevator.Solution.Host.dll").Replace('\\','/');
            if(plugin.Contains("\"")||plugin.Contains("\n"))throw new ArgumentException("Invalid installation path.");
            string script=Path.Combine(job,mode+".scr");
            // Only runtime bootstrap: the host calls the typed API; no elevator drawing commands are sent.
            File.WriteAllText(script,"FILEDIA\n0\nSECURELOAD\n0\n_.NETLOAD\n\""+plugin+"\"\nSOLUTIONHOST\n"+job+"\n"+mode+"\n_.QUIT\n_N\n");
            var psi=new ProcessStartInfo(ConsolePath,"/i \""+drawing+"\" /s \""+script+"\" /l en-US"){
                UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=job};
            using(var process=Process.Start(psi))
            {
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(600000)){process.Kill();process.WaitForExit();throw new TimeoutException("Disposable AutoCAD host exceeded ten minutes; no output published.");}
                Task.WaitAll(stdout,stderr);
                File.WriteAllText(Path.Combine(job,mode+".log"),stdout.Result+stderr.Result);
                if(process.ExitCode!=0)throw new IOException("AutoCAD host exited with code "+process.ExitCode+"; see job log.");
            }
        }
    }
}
