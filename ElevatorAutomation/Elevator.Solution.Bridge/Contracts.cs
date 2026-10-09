using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Elevator.Core.Geometry;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
namespace Elevator.Solution.Bridge
{
    // Transport envelopes reference the existing versioned project JSON, never a second project model.
    public sealed class GenerationRequest
    {
        public int ProtocolVersion=1;
        public string RequestId=Guid.NewGuid().ToString("N");
        public string ProjectJson;
        public GenerationOptions Options=new GenerationOptions{ArrangeViews=true};
    }
    public sealed class GenerationResponse
    {
        public int ProtocolVersion=1;
        public string RequestId,InputHash,OutputPath,Error;
        public bool Success;
        public double ElapsedSeconds;
        public string[] ViewsGenerated;
        public Dictionary<string,int> EntityCounts=new Dictionary<string,int>();
        public List<ValidationIssue> Validation=new List<ValidationIssue>();
        public string LayoutStatus,WrapAngleStatus,CodeDiameterRatioStatus;
        public string DesignJson;
        public DesignContext Context;
        public List<ViewBounds> Bounds=new List<ViewBounds>();
    }
    public static class Wire
    {
        public static string Json(object value){return Elevator.Core.Serialization.CoreJson.Create().Serialize(value);}
        public static T Read<T>(string path){return Elevator.Core.Serialization.CoreJson.Create().Deserialize<T>(File.ReadAllText(path));}
        public static void Write(string path,object value){File.WriteAllText(path,Json(value));}
        public static string HashText(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","");}
        public static string HashFile(string path){using(var sha=SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","");}
        public static ElevatorProject Validate(GenerationRequest r)
        {
            if(r==null||r.ProtocolVersion!=1||String.IsNullOrWhiteSpace(r.RequestId)||r.Options==null)throw new ArgumentException("Unsupported/incomplete generation request.");
            if(r.Options.Views==0||(r.Options.Views&~DrawingViews.All)!=0)throw new ArgumentException("Select at least one supported view.");
            return Elevator.Core.Project.ProjectJson.Deserialize(r.ProjectJson);
        }
    }
    public sealed class OutputConsent
    {
        public string Path,PreviousHash;
        public bool ReplaceExisting;
        public static OutputConsent Capture(string path,bool confirmed)
        {
            path=System.IO.Path.GetFullPath(path);
            if(System.IO.Path.GetExtension(path).ToLowerInvariant()!=".dwg")throw new ArgumentException("Output must have a .dwg extension.");
            bool exists=File.Exists(path);
            if(exists&&!confirmed)throw new IOException("Existing output requires explicit replacement confirmation.");
            return new OutputConsent{Path=path,PreviousHash=exists?Wire.HashFile(path):null,ReplaceExisting=exists&&confirmed};
        }
        public void Publish(string staged)
        {
            string folder=System.IO.Path.GetDirectoryName(Path);Directory.CreateDirectory(folder);
            // A same-directory staging file enables atomic rename/replace; never save directly to the final drawing.
            string temporary=System.IO.Path.Combine(folder,".elevator-"+Guid.NewGuid().ToString("N")+".dwg");
            try
            {
                File.Copy(staged,temporary,false);
                if(ReplaceExisting)
                {
                    if(!File.Exists(Path)||Wire.HashFile(Path)!=PreviousHash)throw new IOException("Output changed since confirmation; replacement cancelled.");
                    File.Replace(temporary,Path,null);
                }
                else File.Move(temporary,Path); // fails if another writer created the output
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
