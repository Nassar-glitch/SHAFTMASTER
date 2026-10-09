using System;
using System.Web.Script.Serialization;
namespace Elevator.Core.Project
{
    public static class ProjectJson
    {
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; }
        public static string Serialize(ElevatorProject project)
        {
            if (project == null) throw new ArgumentNullException("project");
            return Serializer().Serialize(new ProjectDocument { project = project });
        }
        public static ElevatorProject Deserialize(string json)
        {
            var raw = Serializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string,object>;
            if (raw == null || !raw.ContainsKey("schemaVersion") || !(raw["schemaVersion"] is int) || (int)raw["schemaVersion"] != 1)
                throw new ArgumentException("Unsupported or missing ElevatorProject schemaVersion.");
            var document = Serializer().Deserialize<ProjectDocument>(json);
            if (document.project == null) throw new ArgumentException("Project document is missing project.");
            return document.project;
        }
        public static ElevatorProject Clone(ElevatorProject project) { return Deserialize(Serialize(project)); }
    }
}
