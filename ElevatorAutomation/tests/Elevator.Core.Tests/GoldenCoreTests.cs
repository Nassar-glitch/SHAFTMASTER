using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Core.Serialization;
using Elevator.Core.Suspension;
using Xunit;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Core.Tests
{
    // The Core-level golden captures of tests/golden/GoldenTests.cs (round-trip project JSON, results, fingerprints, new
    // project from presets), compared byte for byte with the SAME approved files (tests/golden/approved). The calculation
    // sheet, Design Notes and figure captures need the WPF report code and stay in the console golden suite (tier 1).
    public class GoldenCoreTests
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly string Fixtures = Path.Combine(TestData.Root, "tests", "golden", "fixtures");
        static readonly string Approved = Path.Combine(TestData.Root, "tests", "golden", "approved");
        static readonly Lazy<DesignContext> Ctx = new Lazy<DesignContext>(TestData.Context);
        static readonly Lazy<PresetLibrary> Lib = new Lazy<PresetLibrary>(() => PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")));

        public sealed class GoldenOptions { public string Description, CompanyName, LogoFile, CarGuideForceDraftError; public Dictionary<string, string> ChassisDraftErrors, SafetyPitDraftErrors; }

        public static IEnumerable<object[]> FixtureNames()
        {
            return Directory.GetFiles(Fixtures, "*.elevator.json").Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new object[] { f.Substring(0, f.Length - ".elevator.json".Length) });
        }

        [Theory]
        [MemberData(nameof(FixtureNames))]
        public void Fixture_matches_approved_results_fingerprints_and_roundtrip(string name)
        {
            foreach (var capture in Capture(name)) AssertApproved(capture.Key, capture.Value);
        }

        [Fact]
        public void New_project_from_presets_matches_approved()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            var p = ProjectPresets.CreateNew(Lib.Value, Ctx.Value, seed.Counterweight, seed.Rails.BracketSource);
            string saved = Path.Combine(TestData.NewOutput("new-project"), "new-project.saved.elevator.json");
            new ProjectSession(p).Save(saved);
            AssertApproved("new-project-from-presets/new-project.elevator.json", Utf8.GetBytes(JsonMembers.Sort(NormaliseGuids(File.ReadAllText(saved, Utf8)))));
        }

        static void AssertApproved(string rel, byte[] content)
        {
            string path = Path.Combine(Approved, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), "Approved file missing: " + rel);
            var expected = File.ReadAllBytes(path);
#if !NETFRAMEWORK
            // .NET 8 differs from .NET Framework in two numeric ways that no serializer can remove: double.ToString() without a
            // format (shortest round-trip instead of 15 digits, in calculation detail texts) and the last bit of some Math
            // functions. Lines that differ only in their numbers, all within 1e-12 relative, are accepted and reported.
            if (!expected.SequenceEqual(content) && NumbersOnly(Utf8.GetString(expected), Utf8.GetString(content), rel)) return;
#endif
            if (!expected.SequenceEqual(content))
            {
                string received = Path.Combine(TestData.NewOutput("mismatch"), rel.Replace('/', '_') + ".received");
                File.WriteAllBytes(received, content);
                Assert.Fail("Golden mismatch " + rel + " (" + TestData.Framework + "); received: " + received + "; first difference: " + FirstDifference(Utf8.GetString(expected), Utf8.GetString(content)));
            }
        }
#if !NETFRAMEWORK
        static readonly Regex Number = new Regex(@"-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?");
        public static readonly List<string> Tolerated = new List<string>();
        static bool NumbersOnly(string approved, string received, string rel)
        {
            var x = approved.Split('\n'); var y = received.Split('\n');
            if (x.Length != y.Length) return false;
            var lines = new List<string>();
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] == y[i]) continue;
                if (Number.Replace(x[i], "#") != Number.Replace(y[i], "#")) return false;
                var a = Number.Matches(x[i]).Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
                var b = Number.Matches(y[i]).Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
                if (a.Count != b.Count) return false;
                for (int k = 0; k < a.Count; k++) if (Math.Abs(a[k] - b[k]) > 1e-12 * Math.Max(1, Math.Max(Math.Abs(a[k]), Math.Abs(b[k])))) return false;
                lines.Add((i + 1).ToString(CultureInfo.InvariantCulture));
            }
            lock (Tolerated) Tolerated.Add(rel + ": lines " + string.Join(",", lines));
            Console.WriteLine("Numbers-only difference (net8.0) accepted: " + rel + ": lines " + string.Join(",", lines));
            return true;
        }
#endif
        static string FirstDifference(string a, string b)
        {
            var x = a.Split('\n'); var y = b.Split('\n');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                string l = i < x.Length ? x[i] : "<end>", r = i < y.Length ? y[i] : "<end>";
                if (l != r) return "line " + (i + 1) + ": approved [" + l.Trim() + "] received [" + r.Trim() + "]";
            }
            return "none";
        }

        // ---------- the same capture as tests/golden/GoldenTests.cs (Core part) ----------
        static readonly WarmCache<Dictionary<string, byte[]>> Captured = new WarmCache<Dictionary<string, byte[]>>(() => FixtureNames().Select(x => (string)x[0]), CaptureNow);
        static Dictionary<string, byte[]> Capture(string name) { return Captured.Get(name); }
        static Dictionary<string, byte[]> CaptureNow(string name)
        {
            var result = new Dictionary<string, byte[]>();
            var lib = Lib.Value; var ctx = Ctx.Value;
            string fixturePath = Path.Combine(Fixtures, name + ".elevator.json");
            string optionsPath = Path.Combine(Fixtures, name + ".options.json");
            var options = File.Exists(optionsPath) ? CoreJson.CreateWithDefaultLimit().Deserialize<GoldenOptions>(File.ReadAllText(optionsPath)) : new GoldenOptions();
            var session = new ProjectSession(null); session.Open(fixturePath);
            string saved = Path.Combine(TestData.NewOutput("roundtrip"), name + ".saved.elevator.json");
            session.Save(saved);
            result[name + "/roundtrip.elevator.json"] = Utf8.GetBytes(JsonMembers.Sort(File.ReadAllText(saved, Utf8)));
            var p = session.Project;
            object selection = null;
            if (p.FollowsPresets == true) selection = Try(() => { var o = ProjectPresets.Apply(p, lib, ctx); if (o != null) o.Milliseconds = 0; return o; });
            var workspace = new CalculationWorkspace();
            object chassis = Try(() => workspace.Calculate(p, ctx));
            object pit = Try(() => new SafetyPitCalculationEngine().Calculate(p));
            object ff = Try(() => new CarGuideForceCalculationEngine().Calculate(p, new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, ctx), ctx));
            object rope = Try(() => new RopeTractionCalculationEngine().Calculate(p, ctx, lib));
            var rt = p.Calculations == null ? null : p.Calculations.RopeTraction;
            var ropeResult = rope as RopeTractionResult;
            var values = new
            {
                Q = p.Elevator == null ? null : p.Elevator.CapacityKg,
                SW = p.Shaft == null ? (double?)null : p.Shaft.WidthMm, SD = p.Shaft == null ? (double?)null : p.Shaft.DepthMm,
                n = p.Suspension == null ? null : p.Suspension.RopeCount, d = p.Suspension == null ? null : p.Suspension.RopeDiameterMm,
                Dt = p.Machine == null ? null : p.Machine.TractionSheavePitchDiameterMm,
                Beta = ropeResult == null || ropeResult.Inputs == null ? null : ropeResult.Inputs.UndercutAngleDeg,
                SelectedBeta = rt == null ? null : rt.SelectedUndercutAngleDeg,
                BalanceFactor = rt != null && rt.SelectedBalanceFactor.HasValue ? rt.SelectedBalanceFactor : (p.FollowsPresets == true ? lib.CounterweightBalanceFactor : null),
                SelectedBalanceFactor = rt == null ? null : rt.SelectedBalanceFactor,
                P = p.Calculations == null ? null : p.Calculations.CarMassKg,
                CW = p.Calculations == null ? null : p.Calculations.CounterweightMassKg,
                Machine = p.Calculations == null ? null : p.Calculations.MachineMassKg,
                Locked = p.UserEditedFields == null ? null : p.UserEditedFields.OrderBy(k => k, StringComparer.Ordinal).ToList()
            };
            result[name + "/results.json"] = Utf8.GetBytes(Pretty(new { Fixture = name, options.Description, p.FollowsPresets, Mode = p.Calculations == null ? null : CalculationModes.Resolve(p.Calculations), Selection = selection, Values = values, Chassis = chassis, SafetyPit = pit, CarGuideForces = ff, RopeTraction = rope }) + "\n");
            var cr = chassis as ChassisCalculationResult; var pr = pit as SafetyPitCalculationResult; var fr = ff as CarGuideForceResult;
            result[name + "/fingerprints.json"] = Utf8.GetBytes(Pretty(new
            {
                Chassis = cr == null ? null : cr.InputFingerprint, ChassisCurrent = cr == null ? (bool?)null : workspace.IsCurrent(p, ctx),
                SafetyPit = pr == null ? null : pr.InputFingerprint, SafetyPitCurrent = pr == null ? (bool?)null : SafetyPitCalculationEngine.IsCurrent(pr, p),
                CarGuideForces = fr == null ? null : fr.InputFingerprint, CarGuideForcesCurrent = fr == null ? (bool?)null : CarGuideForceCalculationEngine.IsCurrent(fr, p, ctx),
                RopeTraction = ropeResult == null ? null : ropeResult.InputFingerprint, RopeTractionCurrent = ropeResult == null ? (bool?)null : RopeTractionCalculationEngine.IsCurrent(ropeResult, p, ctx, lib),
                PresetLibrary = lib.Hash()
            }) + "\n");
            return result;
        }
        static object Try(Func<object> f) { try { return f(); } catch (Exception e) { return "EXCEPTION " + e.GetType().FullName + ": " + e.Message; } }
        static string NormaliseGuids(string json)
        {
            var map = new Dictionary<string, string>();
            return Regex.Replace(json, "\"[0-9a-f]{32}\"", m => { string v; if (!map.TryGetValue(m.Value, out v)) { v = "\"GUID-" + (map.Count + 1) + "\""; map[m.Value] = v; } return v; });
        }
        static string Pretty(object value)
        {
            var js = CoreJson.Create();
            var b = new StringBuilder(); Write(js.DeserializeObject(js.Serialize(value)), js, b, 0); return b.ToString();
        }
        static void Write(object v, IJsonSerializer js, StringBuilder b, int depth)
        {
            string pad = new string(' ', 2 * (depth + 1)), end = new string(' ', 2 * depth);
            var map = v as IDictionary<string, object>;
            if (map != null)
            {
                if (map.Count == 0) { b.Append("{}"); return; }
                b.Append("{\n"); bool first = true;
                foreach (var k in map.Keys.OrderBy(k => k, StringComparer.Ordinal)) { if (!first) b.Append(",\n"); first = false; b.Append(pad).Append(js.Serialize(k)).Append(": "); Write(map[k], js, b, depth + 1); }
                b.Append('\n').Append(end).Append('}'); return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null && !(v is string))
            {
                var items = list.Cast<object>().ToList();
                if (items.Count == 0) { b.Append("[]"); return; }
                b.Append("[\n");
                for (int i = 0; i < items.Count; i++) { if (i > 0) b.Append(",\n"); b.Append(pad); Write(items[i], js, b, depth + 1); }
                b.Append('\n').Append(end).Append(']'); return;
            }
            b.Append(js.Serialize(v));
        }
    }
}
