using System;
using System.IO;
using System.Security.Cryptography;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    public static class Wire
    {
        public static string Json(object value)
        {
            return Elevator.Core.Serialization.CoreJson.Create().Serialize(value);
        }

        public static T Read<T>(string path)
        {
            return Elevator.Core.Serialization.CoreJson.Create().Deserialize<T>(File.ReadAllText(path));
        }

        public static void Write(string path, object value)
        {
            File.WriteAllText(path, Json(value));
        }

        public static string HashText(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }

        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            {
                using (var s = File.OpenRead(path))
                    return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
            }
        }

        public static ElevatorProject Validate(GenerationRequest r)
        {
            if (r == null || r.ProtocolVersion != 1 || String.IsNullOrWhiteSpace(r.RequestId) || r.Options == null)
                throw new ArgumentException("Unsupported/incomplete generation request.");
            if (r.Options.Views == 0 || (r.Options.Views & ~DrawingViews.All) != 0)
                throw new ArgumentException("Select at least one supported view.");
            return Elevator.Core.Project.ProjectJson.Deserialize(r.ProjectJson);
        }
    }
}
