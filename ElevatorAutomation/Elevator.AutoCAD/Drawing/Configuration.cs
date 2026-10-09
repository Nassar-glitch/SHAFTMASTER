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
        public static readonly string Root=Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName;
        public static T Read<T>(string file) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(Path.Combine(Root,"config",file))); }
        public static void WriteJson(string file, object value) { var s=new JavaScriptSerializer();s.MaxJsonLength=Int32.MaxValue;File.WriteAllText(file,s.Serialize(value)); }
    }
}
