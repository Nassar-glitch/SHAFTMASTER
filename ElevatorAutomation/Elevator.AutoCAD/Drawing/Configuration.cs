using Elevator.Core;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Elevator.AutoCAD
{
    public static class Configuration
    {
        // The folder with config/ and Templates/: the parent of the binary folder for a release (bin-shaftmaster-vNN), an
        // ancestor for a build in artifacts/bin.
        public static readonly string Root=FindRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        public static string FindRoot(string binary)
        {
            var parent=Directory.GetParent(binary.TrimEnd(Path.DirectorySeparatorChar));
            for(var d=parent;d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"config"))&&Directory.Exists(Path.Combine(d.FullName,"Templates")))return d.FullName;
            return parent.FullName;
        }
        public static T Read<T>(string file) { return Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<T>(File.ReadAllText(Path.Combine(Root,"config",file))); }
        public static void WriteJson(string file, object value) { File.WriteAllText(file,Elevator.Core.Serialization.CoreJson.Create().Serialize(value)); }
    }
}
