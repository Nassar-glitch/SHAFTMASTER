#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Elevator.Core.Application;
using Elevator.Core;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
using Elevator.Solution.Bridge;
using Xunit;
using Xunit.Abstractions;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Core.Tests
{
    // Evidence for the serializer decision (ARCHITECTURE-PLAN P2): on .NET Framework, JavaScriptCompatibleJson (the
    // netstandard2.0 serializer) against JavaScriptSerializer (the application's), in the same process. Texts are compared
    // byte for byte after sorting the members of every object (JsonMembers.Sort): JavaScriptSerializer itself writes members
    // in reflection order, which changes within one process (two serializations of the same object can differ in order).
    public class SerializerParityTests
    {
        readonly ITestOutputHelper output;
        public SerializerParityTests(ITestOutputHelper output) { this.output = output; }
        static readonly JavaScriptSerializer Jss = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 1000 };
        static readonly JavaScriptCompatibleJson Compat = new JavaScriptCompatibleJson();
        static string N(string json) { return JsonMembers.Sort(json); }
        static void AssertJson(string expected, string actual) { Assert.Equal(N(expected), N(actual)); }
        static void Same(object value, string label) { Assert.True(N(Jss.Serialize(value)) == N(Compat.Serialize(value)), "Serialize differs: " + label + "\n JSS:    " + Clip(Jss.Serialize(value)) + "\n compat: " + Clip(Compat.Serialize(value))); }
        static string Clip(string s) { return s.Length > 400 ? s.Substring(0, 400) + "..." : s; }

        [Fact]
        public void Every_character_is_escaped_the_same()
        {
            for (int c = 0; c <= 0xFFFF; c++) { string s = "a" + (char)c + "b"; Assert.True(Jss.Serialize(s) == Compat.Serialize(s), "char U+" + c.ToString("X4") + ": " + Jss.Serialize(s) + " vs " + Compat.Serialize(s)); }
            Same('\0', "char NUL"); Same('x', "char x"); Same("", "empty string");
        }

        [Fact]
        public void Numbers_are_written_the_same()
        {
            var special = new double[] { 0, -0.0, 1, -1, 0.1, 0.2, 0.1 + 0.2, 1.0 / 3, 2.0 / 3, 1e-5, 1e-7, 123456789012345678, 1e15, 1e16, 1e17, 1e21, 1e22, 1e-300, 4.9e-324, double.MaxValue, double.MinValue, double.Epsilon, Math.PI, Math.E, 12347.128322572622, 1015.0000000000001, 0.30000000000000004, 9007199254740993, 152.06893346, 5.5e-6 };
            foreach (var d in special) Same(d, "double " + d.ToString("R", CultureInfo.InvariantCulture));
            // Normal doubles must match. Subnormal doubles (|x| < 2.2E-308) are counted separately: there .NET Framework's own
            // "R" check accepts some 15-digit texts that do not convert back exactly; such values never occur in project data.
            var rnd = new Random(20261009); int n = 0, normalDiff = 0, subnormal = 0, subnormalDiff = 0; var examples = new List<string>(); var unexpected = new List<string>();
            for (int i = 0; i < 100000; i++)
            {
                var bytes = new byte[8]; rnd.NextBytes(bytes); double d = BitConverter.ToDouble(bytes, 0);
                double e = Math.Round(rnd.NextDouble() * Math.Pow(10, rnd.Next(-6, 9)), rnd.Next(0, 12));
                foreach (var x in new[] { d, e })
                {
                    if (double.IsNaN(x) || double.IsInfinity(x)) continue; n++;
                    bool sub = x != 0 && Math.Abs(x) < 2.2250738585072014E-308; if (sub) subnormal++;
                    if (Jss.Serialize(x) == Compat.Serialize(x)) continue;
                    if (sub) subnormalDiff++; else { normalDiff++; if (examples.Count < 5) examples.Add(Jss.Serialize(x) + " vs " + Compat.Serialize(x)); }
                    // The only allowed difference: the same value written with 15 instead of 17 significant digits, or the reverse.
                    var pair = new[] { Jss.Serialize(x), Compat.Serialize(x) }; var forms = new[] { x.ToString("G15", CultureInfo.InvariantCulture), x.ToString("G17", CultureInfo.InvariantCulture) };
                    if (!pair.OrderBy(s => s).SequenceEqual(forms.OrderBy(s => s))) unexpected.Add(pair[0] + " vs " + pair[1]);
                }
            }
            output.WriteLine("Random doubles: " + n + " compared; normal mismatches " + normalDiff + "; subnormal " + subnormal + " of which " + subnormalDiff + " differ");
            output.WriteLine("Examples (JSS vs compatible): " + string.Join("; ", examples));
            foreach (var f in new float[] { 0.1f, 1f / 3, 3.4028235e38f, 1e-7f, 152.06894f }) Same(f, "float");
            foreach (var m in new decimal[] { 0m, 1.50m, -2.25m, 79228162514264337593543950335m, 0.0000001m }) Same(m, "decimal");
            foreach (var v in new object[] { 1, -1, int.MaxValue, long.MinValue, (short)3, (byte)4, uint.MaxValue, ulong.MaxValue, true, false, null, RopeTractionCalculationEngine.TractionCase.Braking, DrawingViews.All, Guid.Parse("b2cbbdda-983f-4d17-bb56-afbefa196127"), new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc) }) Same(v, "value " + v);
            output.WriteLine("Doubles compared: " + (n + special.Length));
            // Known difference (reported in docs/TESTING.md): .NET Framework "R" picks 15 or 17 digits with its own inexact
            // conversion, so a small share of doubles gets the other (equally exact) text. This test records the share.
            Assert.True(unexpected.Count == 0, "Differences other than 15 vs 17 digits: " + string.Join("; ", unexpected.Take(5)));
        }

        [Fact]
        public void Number_literals_are_read_as_the_same_types()
        {
            foreach (var literal in new[] { "0", "-0", "1", "2147483647", "2147483648", "-2147483649", "9223372036854775807", "9223372036854775808", "1.50", "1e5", "1E-05", "-0.0", "12347.128322572622", "0.1", "79228162514264337593543950336", "1.7976931348623157E+308", "[1,2.5,\"x\",null,true]", "{\"a\":{\"b\":[1,{\"c\":2}]}}" })
            {
                object a = Jss.DeserializeObject(literal), b = Compat.DeserializeObject(literal);
                Assert.True(Describe(a) == Describe(b), "Literal " + literal + ": " + Describe(a) + " vs " + Describe(b));
            }
        }
        static string Describe(object o)
        {
            if (o == null) return "null";
            var d = o as IDictionary<string, object>; if (d != null) return "{" + string.Join(",", d.Select(e => e.Key + ":" + Describe(e.Value))) + "}";
            var l = o as System.Collections.IEnumerable; if (l != null && !(o is string)) return "[" + string.Join(",", l.Cast<object>().Select(Describe)) + "]";
            return o.GetType().Name + "(" + Convert.ToString(o, CultureInfo.InvariantCulture) + ")";
        }

        // Project JSON: every golden fixture, the approved round-trip files and the JSON read back from a DWG.
        public static IEnumerable<object[]> ProjectFiles()
        {
            var golden = Path.Combine(TestData.Root, "tests", "golden");
            return Directory.GetFiles(Path.Combine(golden, "fixtures"), "*.elevator.json")
                .Concat(Directory.GetFiles(Path.Combine(golden, "approved"), "roundtrip.elevator.json", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(Path.Combine(golden, "approved-native"), "*.elevator.json", SearchOption.AllDirectories))
                .Concat(new[] { Path.Combine(TestData.Root, "examples", "ElevatorProject.example.json"), Path.Combine(golden, "approved", "new-project-from-presets", "new-project.elevator.json") })
                .Select(f => new object[] { f.Substring(TestData.Root.Length + 1) });
        }

        [Theory]
        [MemberData(nameof(ProjectFiles))]
        public void Project_json_reads_and_writes_the_same(string file)
        {
            string json = File.ReadAllText(Path.Combine(TestData.Root, file));
            AssertJson(Jss.Serialize(Jss.DeserializeObject(json)), Jss.Serialize(Compat.DeserializeObject(json)));
            var viaJss = Jss.Deserialize<ProjectDocument>(json); var viaCompat = Compat.Deserialize<ProjectDocument>(json);
            AssertJson(Jss.Serialize(viaJss), Jss.Serialize(viaCompat));
            AssertJson(Jss.Serialize(viaJss), Compat.Serialize(viaJss));
            AssertJson(ProjectJson.Serialize(viaJss.project), Compat.Serialize(new ProjectDocument { project = viaJss.project }));
        }

        [Theory]
        [InlineData("config/presets.json")]
        [InlineData("config/reviewed-context.json")]
        [InlineData("config/reviewed-chassis-evidence.json")]
        [InlineData("config/preliminary-3d-reference.json")]
        [InlineData("config/equipment-profile.json")]
        public void Configuration_files_read_the_same(string file)
        {
            string json = File.ReadAllText(Path.Combine(TestData.Root, file));
            AssertJson(Jss.Serialize(Jss.DeserializeObject(json)), Jss.Serialize(Compat.DeserializeObject(json)));
            Type type = file.EndsWith("presets.json") ? typeof(PresetLibrary) : file.EndsWith("reviewed-context.json") ? typeof(DesignContext) : file.EndsWith("chassis-evidence.json") ? typeof(ChassisGeometryEvidence) : file.EndsWith("equipment-profile.json") ? typeof(EquipmentProfile) : null;
            if (type == null) return;
            var a = typeof(JavaScriptSerializer).GetMethod("Deserialize", new[] { typeof(string) }).MakeGenericMethod(type).Invoke(Jss, new object[] { json });
            var b = typeof(JavaScriptCompatibleJson).GetMethod("Deserialize").MakeGenericMethod(type).Invoke(Compat, new object[] { json });
            AssertJson(Jss.Serialize(a), Jss.Serialize(b));
            AssertJson(Jss.Serialize(a), Compat.Serialize(a));
        }

        // Every calculation result of every golden fixture written the same, and every fingerprint computed the same with either serializer.
        [Theory]
        [MemberData(nameof(GoldenCoreTests.FixtureNames), MemberType = typeof(GoldenCoreTests))]
        public void Results_and_fingerprints_are_the_same(string name)
        {
            var ctx = TestData.Context(); var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            string path = Path.Combine(TestData.Root, "tests", "golden", "fixtures", name + ".elevator.json");
            object[] viaJss = Results(path, ctx, lib);
            object[] viaCompat;
            try { CoreJson.Override = () => new JavaScriptCompatibleJson(); viaCompat = Results(path, ctx, PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"))); }
            finally { CoreJson.Override = null; }
            for (int i = 0; i < viaJss.Length; i++)
            {
                Same(viaJss[i], name + " result " + i);
                AssertJson(Jss.Serialize(viaJss[i]), Jss.Serialize(viaCompat[i]));
            }
        }
        static object[] Results(string path, DesignContext ctx, PresetLibrary lib)
        {
            var p = ProjectJson.Deserialize(File.ReadAllText(path));
            RopeSizing sizing = null; if (p.FollowsPresets == true) { sizing = ProjectPresets.Apply(p, lib, ctx); if (sizing != null) sizing.Milliseconds = 0; }
            var chassis = new CalculationWorkspace().Calculate(p, ctx);
            var pit = new SafetyPitCalculationEngine().Calculate(p);
            var ff = new CarGuideForceCalculationEngine().Calculate(p, new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, ctx), ctx);
            var rope = new RopeTractionCalculationEngine().Calculate(p, ctx, lib);
            var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
            return new object[] { sizing, chassis, pit, ff, rope, design, p, lib.Hash() };
        }

        // Bridge contracts: the generation request of every fixture, and the responses and STEP parameters of the latest local runs.
        [Fact]
        public void Bridge_contracts_are_the_same()
        {
            int n = 0;
            foreach (var f in Directory.GetFiles(Path.Combine(TestData.Root, "tests", "golden", "fixtures"), "*.elevator.json"))
            {
                var request = new GenerationRequest { ProjectJson = File.ReadAllText(f), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
                Same(request, "request " + Path.GetFileName(f)); AssertJson(Wire.Json(request), Compat.Serialize(request));
                AssertJson(Wire.Json(Jss.Deserialize<GenerationRequest>(Wire.Json(request))), Wire.Json(Compat.Deserialize<GenerationRequest>(Wire.Json(request)))); n++;
            }
            var results = Path.Combine(TestData.Root, "artifacts", "test-results");
            var local = Directory.Exists(results) ? Directory.GetFiles(results, "generation.json", SearchOption.AllDirectories).Take(6).ToList() : new List<string>();
            foreach (var f in local)
            {
                string json = File.ReadAllText(f);
                var a = Jss.Deserialize<GenerationResponse>(json); var b = Compat.Deserialize<GenerationResponse>(json);
                AssertJson(Wire.Json(a), Wire.Json(b)); Same(a, "response " + f); n++;
            }
            var steps = Directory.Exists(results) ? Directory.GetFiles(results, "step-parameters-*.json", SearchOption.AllDirectories).Take(4).ToList() : new List<string>();
            foreach (var f in steps)
            {
                string json = File.ReadAllText(f);
                var a = Jss.Deserialize<StepModelParameters>(json); var b = Compat.Deserialize<StepModelParameters>(json);
                AssertJson(Wire.Json(a), Wire.Json(b)); Same(a, "STEP parameters " + f); n++;
            }
            output.WriteLine("Contracts compared: " + n + " (" + local.Count + " local generation responses, " + steps.Count + " local STEP parameter files)");
        }
    }
}
#endif
