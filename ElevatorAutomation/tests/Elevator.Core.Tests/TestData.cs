using System;
using System.IO;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Core.Serialization;

namespace Elevator.Core.Tests
{
    // Entry point required by the test SDK on net8.0; the hosted console suites bring their own Main methods.
    public static class TestHost
    {
        public static int Main(string[] args) { return 0; }
    }

    // Paths and data shared by the tests.
    public static class TestData
    {
#if !NETFRAMEWORK
        // netstandard2.0 Core has no default serializer: the tests use JavaScriptCompatibleJson explicitly.
        static TestData() { CoreJson.Use(() => new JavaScriptCompatibleJson()); }
#endif
#if NETFRAMEWORK
        public const string Framework = "net48";
#else
        public const string Framework = "net8.0";
#endif
        // The ElevatorAutomation folder (where ElevatorAutomation.sln is).
        public static readonly string Root = FindRoot();
        static string FindRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "ElevatorAutomation.sln"))) return d.FullName;
            throw new InvalidOperationException("ElevatorAutomation.sln not found above " + AppContext.BaseDirectory);
        }
        static readonly object gate = new object();
        static string bin;
        // A folder with the data files the application folder holds, copied as the build copies them.
        public static string Bin
        {
            get
            {
                lock (gate)
                {
                    if (bin != null) return bin;
                    string dir = Path.Combine(Root, "artifacts", "tests", "data", Framework);
                    Directory.CreateDirectory(dir);
                    Copy("config/reviewed-context.json", dir, "reviewed-context.json");
                    Copy("config/reviewed-chassis-evidence.json", dir, "reviewed-chassis-evidence.json");
                    Copy("config/presets.json", dir, "presets.json");
                    Copy("config/preliminary-3d-reference.json", dir, "preliminary-3d-reference.json");
                    Copy("examples/ElevatorProject.example.json", dir, "reviewed-example.elevator.json");
                    return bin = dir;
                }
            }
        }
        static void Copy(string source, string dir, string name) { File.Copy(Path.Combine(Root, source), Path.Combine(dir, name), true); }
        // A fresh, empty output folder under artifacts/tests/output/<framework>/.
        public static string NewOutput(string name)
        {
            string dir = Path.Combine(Root, "artifacts", "tests", "output", Framework, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + name);
            Directory.CreateDirectory(dir);
            return dir;
        }
        public static DesignContext Context()
        {
            var json = CoreJson.Create();
            var ctx = json.Deserialize<DesignContext>(File.ReadAllText(Path.Combine(Bin, "reviewed-context.json")));
            ctx.ChassisEvidence = json.Deserialize<ChassisGeometryEvidence>(File.ReadAllText(Path.Combine(Bin, "reviewed-chassis-evidence.json")));
            return ctx;
        }
    }
}

#if !NETFRAMEWORK
namespace Elevator.Solution.Bridge
{
    // net8.0 stand-in for the Bridge's Wire (the Bridge is .NET Framework only): the same calls over CoreJson.
    public static class Wire
    {
        public static string Json(object value) { return Elevator.Core.Serialization.CoreJson.Create().Serialize(value); }
        public static T Read<T>(string path) { return Elevator.Core.Serialization.CoreJson.Create().Deserialize<T>(System.IO.File.ReadAllText(path)); }
        public static void Write(string path, object value) { System.IO.File.WriteAllText(path, Json(value)); }
    }
}
#endif

#if !NETFRAMEWORK
namespace System.Web.Script.Serialization
{
    // net8.0 stand-in for the JavaScriptSerializer calls in the hosted console suites (PresetsTests' order-independent
    // comparison): the same calls over JavaScriptCompatibleJson.
    public sealed class JavaScriptSerializer
    {
        readonly Elevator.Core.Serialization.JavaScriptCompatibleJson json = new Elevator.Core.Serialization.JavaScriptCompatibleJson();
        public int MaxJsonLength { get; set; }
        public string Serialize(object value) { return json.Serialize(value); }
        public object DeserializeObject(string input) { return json.DeserializeObject(input); }
        public T Deserialize<T>(string input) { return json.Deserialize<T>(input); }
    }
}
#endif