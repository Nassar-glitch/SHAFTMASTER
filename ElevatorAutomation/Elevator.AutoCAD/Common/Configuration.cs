using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Locates the application folder and reads its configuration files.</summary>
    public static class Configuration
    {
        // The folder with config/ and Templates/: the parent of the binary folder for a release (bin-shaftmaster-vNN), an
        // ancestor for a build in artifacts/bin.
        public static readonly string Root = FindRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        /// <summary>Finds the folder that holds config and Templates, for a release or a build.</summary>
        public static string FindRoot(string binary)
        {
            var parent = Directory.GetParent(binary.TrimEnd(Path.DirectorySeparatorChar));
            for (var d = parent; d != null; d = d.Parent)
            {
                if (Directory.Exists(Path.Combine(d.FullName, "config")) && Directory.Exists(Path.Combine(d.FullName, "Templates")))
                    return d.FullName;
            }
            return parent.FullName;
        }

        /// <summary>Reads a JSON configuration file into a type.</summary>
        public static T Read<T>(string file)
        {
            return Elevator.Core
                .Serialization
                .CoreJson
                .CreateWithDefaultLimit()
                .Deserialize<T>(File.ReadAllText(Path.Combine(Root, "config", file)));
        }

        /// <summary>Writes an object as a JSON file.</summary>
        public static void WriteJson(string file, object value)
        {
            File.WriteAllText(file, Elevator.Core.Serialization.CoreJson.Create().Serialize(value));
        }
    }
}
