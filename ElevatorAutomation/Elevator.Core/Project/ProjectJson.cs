using System;
using Elevator.Core.Serialization;

namespace Elevator.Core.Project
{
    /// <summary>Reads and writes project files (JSON) and clones projects.</summary>
    public static class ProjectJson
    {
        static IJsonSerializer Serializer()
        {
            return CoreJson.Create();
        }

        /// <summary>Writes a project as JSON.</summary>
        public static string Serialize(ElevatorProject project)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            return Serializer().Serialize(new ProjectDocument { project = project });
        }

        /// <summary>Reads a project from JSON.</summary>
        public static ElevatorProject Deserialize(string json)
        {
            var raw = Serializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
            if (raw == null || !raw.ContainsKey("schemaVersion") || !(raw["schemaVersion"] is int) || (int)raw["schemaVersion"] != 1)
                throw new ArgumentException("Unsupported or missing ElevatorProject schemaVersion.");
            var document = Serializer().Deserialize<ProjectDocument>(json);
            if (document.project == null)
                throw new ArgumentException("Project document is missing project.");
            return document.project;
        }

        /// <summary>Copies a project through its JSON.</summary>
        public static ElevatorProject Clone(ElevatorProject project)
        {
            return Deserialize(Serialize(project));
        }
    }
}
