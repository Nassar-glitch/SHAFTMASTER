using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // Counts the checks made inside the converted suites and writes them at the end of the run (checks-<framework>.json in the
    // output root); tools/test.ps1 adds them to the xUnit test count.
    public static class Suite
    {
        static readonly object gate = new object();
        static readonly System.Collections.Generic.SortedDictionary<string, int> counts = new System.Collections.Generic.SortedDictionary<string, int>();
        static Suite() { AppDomain.CurrentDomain.ProcessExit += (sender, e) => Write(); }
        public static void Count(string suite)
        {
            lock (gate) { int n; counts.TryGetValue(suite, out n); counts[suite] = n + 1; }
        }
        static void Write()
        {
            lock (gate)
            {
                if (counts.Count == 0) return;
                int total = 0; var parts = new System.Collections.Generic.List<string>();
                foreach (var c in counts) { total += c.Value; parts.Add("\"" + c.Key + "\":" + c.Value); }
                string dir = Path.Combine(TestData.OutputRoot, TestData.Framework);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "checks-" + TestData.Framework + ".json"), "{\"Framework\":\"" + TestData.Framework + "\",\"Checks\":" + total + ",\"Suites\":{" + String.Join(",", parts.ToArray()) + "}}");
            }
        }
    }

    // The result of an expensive per-fixture computation. The first request computes every name in parallel (all cores), later
    // requests read the stored result (or the stored failure).
    public sealed class WarmCache<T>
    {
        readonly Lazy<System.Collections.Generic.Dictionary<string, KeyValuePair<T, Exception>>> all;

        public WarmCache(Func<System.Collections.Generic.IEnumerable<string>> names, Func<string, T> compute)
        {
            all = new Lazy<System.Collections.Generic.Dictionary<string, KeyValuePair<T, Exception>>>(() =>
            {
                var list = names().ToArray();
                var results = new KeyValuePair<T, Exception>[list.Length];
                System.Threading.Tasks.Parallel.For(0, list.Length, i =>
                {
                    try { results[i] = new KeyValuePair<T, Exception>(compute(list[i]), null); }
                    catch (Exception e) { results[i] = new KeyValuePair<T, Exception>(default(T), e); }
                });
                var map = new System.Collections.Generic.Dictionary<string, KeyValuePair<T, Exception>>();
                for (int i = 0; i < list.Length; i++) map[list[i]] = results[i];
                return map;
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public T Get(string name)
        {
            var entry = all.Value[name];
            if (entry.Value != null) throw new Exception("Computing " + name + " failed: " + entry.Value.Message, entry.Value);
            return entry.Key;
        }
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
        // Another test process (the Reporting tests) may be copying the same files: a file that is already identical is left alone.
        static void Copy(string source, string dir, string name)
        {
            string from = Path.Combine(Root, source), to = Path.Combine(dir, name);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(to) && File.ReadAllBytes(to).SequenceEqual(File.ReadAllBytes(from))) return;
                    File.Copy(from, to, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 20) throw;
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
        // Where the tests write files: ELEVATOR_TEST_OUT (set by tools/test.ps1) or artifacts/tests/output.
        public static string OutputRoot
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("ELEVATOR_TEST_OUT");
                return String.IsNullOrEmpty(dir) ? Path.Combine(Root, "artifacts", "tests", "output") : dir;
            }
        }
        // A fresh, empty output folder under <OutputRoot>/<framework>/.
        public static string NewOutput(string name)
        {
            string dir = Path.Combine(OutputRoot, Framework, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + name);
            Directory.CreateDirectory(dir);
            return dir;
        }
        // The folder <OutputRoot>/<framework>/<name>, created on first use and reused by the suite's tests.
        public static string Folder(string name)
        {
            string dir = Path.Combine(OutputRoot, Framework, name);
            Directory.CreateDirectory(dir);
            return dir;
        }
        static readonly Lazy<DesignContext> context = new Lazy<DesignContext>(Load);
        // The reviewed design context, loaded once and shared by the tests (they do not change it), so the rope selection cache is shared.
        public static DesignContext Context() { return context.Value; }
        static DesignContext Load()
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