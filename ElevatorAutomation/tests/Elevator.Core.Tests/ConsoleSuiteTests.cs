using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Elevator.Core.Tests
{
    // The Core console suites of tests/solution, run unchanged inside xUnit (ARCHITECTURE-PLAN P2; the console runners stay
    // until P7). Each suite writes its result file into a fresh folder; the test passes when the suite returns 0 and its
    // result file says PASS.
    public class ConsoleSuiteTests
    {
        readonly ITestOutputHelper output;
        public ConsoleSuiteTests(ITestOutputHelper output) { this.output = output; }

        int Run(Func<string[], int> main, string name, string resultFile, params string[] extra)
        {
            string dir = TestData.NewOutput(name);
            int exit = main(new[] { TestData.Bin, dir }.Concat(extra).ToArray());
            string result = Path.Combine(dir, resultFile);
            Assert.True(exit == 0, name + " returned " + exit + "; see " + dir);
            Assert.True(File.Exists(result), "No result file " + result);
            string text = File.ReadAllText(result);
            Assert.Contains("\"Status\":\"PASS\"", text);
            var m = Regex.Match(text, "\"Assertions\":(\\d+)");
            int count = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            output.WriteLine(name + " (" + TestData.Framework + "): PASS, " + count + " assertions; " + dir);
            return count;
        }

        [Fact]
        public void Rope_traction_core() { Assert.Equal(60, Run(RopeTractionTests.Main, "rope-core", "rope-traction-tests.json")); }

        [Fact]
        public void Car_guide_force_core() { Assert.Equal(131, Run(CarGuideForceTests.Main, "ff-core", "ff-core-tests.json", TestData.Root)); }

        [Fact]
        public void Safety_pit_core() { Assert.Equal(117, Run(SafetyPitTests.Main, "safety-pit-core", "safety-pit-tests.json", TestData.Root)); }

        // Needs the Design Notes (Elevator.Reporting since P3): runs on net8.0 and net48.
        [Fact]
        public void Presets_core() { Assert.Equal(160, Run(PresetsTests.Main, "presets-core", "presets-tests.json")); }
    }
}
