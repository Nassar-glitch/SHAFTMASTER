using System;
using System.IO;
using System.Security.Cryptography;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Reading, writing, hashing and validating the bridge contracts.</summary>
    public static class Wire
    {
        /// <summary>Writes an object as JSON.</summary>
        public static string Json(object value)
        {
            return Elevator.Core.Serialization.CoreJson.Create().Serialize(value);
        }

        /// <summary>Reads a JSON file into a type.</summary>
        public static T Read<T>(string path)
        {
            return Elevator.Core.Serialization.CoreJson.Create().Deserialize<T>(File.ReadAllText(path));
        }

        /// <summary>Writes an object to a JSON file.</summary>
        public static void Write(string path, object value)
        {
            File.WriteAllText(path, Json(value));
        }

        /// <summary>SHA-256 of a text.</summary>
        public static string HashText(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }

        /// <summary>SHA-256 of a file.</summary>
        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            {
                using (var s = File.OpenRead(path))
                    return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
            }
        }

        /// <summary>Checks a generation request and returns its project.</summary>
        public static ElevatorProject Validate(GenerationRequest r)
        {
            if (r == null || r.ProtocolVersion != BridgeContract.Version || String.IsNullOrWhiteSpace(r.RequestId) || r.Options == null)
                throw new ArgumentException("Unsupported/incomplete generation request.");
            if (r.Options.Views == 0 || (r.Options.Views & ~DrawingViews.All) != 0)
                throw new ArgumentException("Select at least one supported view.");
            return Elevator.Core.Project.ProjectJson.Deserialize(r.ProjectJson);
        }
    }
}
