using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
using Elevator.Core.Tests;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
using Elevator.Reporting.Sheet;
#if NETFRAMEWORK
using Elevator.Solution.Desktop.Common;
#endif
using Xunit;


namespace Elevator.Reporting.Tests
{
    // DOCX content of the calculation sheet and the Design Notes without Word (ARCHITECTURE-PLAN P3), for the golden fixtures:
    // the text equals the approved golden text (tests/golden/approved), forbidden strings are absent, every figure is an SVG
    // part, and the page frame and title block have their structure. Ported from tests/solution/CalculationSheetTests.cs.
    public class ReportContentTests
    {
        static readonly DateTime Clock = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Local);
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly string Golden = Path.Combine(TestData.Root, "tests", "golden");
        const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        // CalculationSheetTests.Removed: text that must not appear in the client document (case-insensitive).
        static readonly string[] Removed = {
            "preliminary","engineering review","not certified","certified","certification","compliance","complies","statement of",
            "incomplete","not for issue","unavailable","complete the missing","requested calculations",
            "elevator_calculations_gearbox",".docx","sha-256","paragraph","paraid",
            "gearboxnote","twostageverticalmachinedatum","releasedtwostagewithrigidspreader","-v1","engine record","result currency","fingerprint",
            "calculationsheettests","shaftmaster.exe","adopted","calculated under","user-approved","approved interpretation","interpretation","reference only","synthetic",
            "equal-sharing","equal sharing","not established","rule sources","note-derived","limitations","not included","not provided","invalid text",
            "not covered","excluded","no guide strength","verification","k2","status","used by","unsaved","project file","project identifier","application" };

        public sealed class GoldenOptions { public string Description, CompanyName, LogoFile, CarGuideForceDraftError; public Dictionary<string, string> ChassisDraftErrors, SafetyPitDraftErrors; }

        public static IEnumerable<object[]> Fixtures()
        {
            return Directory.GetFiles(Path.Combine(Golden, "fixtures"), "*.elevator.json").Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new object[] { f.Substring(0, f.Length - ".elevator.json".Length) });
        }

        // ---------- rendering ----------
        sealed class TestRasterizer : IFigureRasterizer
        {
            static readonly byte[] OnePixelPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
            public void Initialise() { }
            public byte[] Png(Figure f, double dpi) { return OnePixelPng; }
        }
        sealed class Rendered { public CalculationSheet Sheet; public byte[] SheetDocx; public byte[] NotesDocx; public List<string> Svgs = new List<string>(); }
        // One render per fixture, shared by the tests that use it; the first request renders all fixtures in parallel.
        static readonly WarmCache<Rendered> cache = new WarmCache<Rendered>(() => Fixtures().Select(x => (string)x[0]), RenderThread);
        static readonly object setup = new object();
        static Rendered Render(string name) { return cache.Get(name); }
        static Rendered RenderThread(string name)
        {
            Rendered r = null; Exception error = null;
            // WPF measurement needs an STA thread on .NET Framework.
            var t = new Thread(() => { try { r = RenderNow(name); } catch (Exception e) { error = e; } });
#if NETFRAMEWORK
            t.SetApartmentState(ApartmentState.STA);
#endif
            t.Start(); t.Join();
            if (error != null) throw new Exception("Rendering " + name + " failed", error);
            return r;
        }
        static Rendered RenderNow(string name)
        {
            lock (setup)
            {
                ReportClock.Now = () => Clock;
#if NETFRAMEWORK
                if (FigureRender.Rasterizer == null) FigureRender.Rasterizer = new WpfFigureRasterizer();
#else
                if (FigureRender.Rasterizer == null) FigureRender.Rasterizer = new TestRasterizer();
#endif
            }
            var ctx = TestData.Context(); var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            string optionsPath = Path.Combine(Golden, "fixtures", name + ".options.json");
            var o = File.Exists(optionsPath) ? CoreJson.CreateWithDefaultLimit().Deserialize<GoldenOptions>(File.ReadAllText(optionsPath)) : new GoldenOptions();
            var session = new ProjectSession(null); session.Open(Path.Combine(Golden, "fixtures", name + ".elevator.json"));
            var p = session.Project;
            if (p.FollowsPresets == true) ProjectPresets.Apply(p, lib, ctx);
            Func<CalculationSheetInputs> inputs = () =>
            {
                var i = new CalculationSheetInputs { Presets = lib, CompanyName = o.CompanyName, CarGuideForceDraftError = o.CarGuideForceDraftError };
                if (o.LogoFile != null) i.Logo = DocxImage.FromFile(Path.Combine(TestData.Root, o.LogoFile));
                if (o.ChassisDraftErrors != null) foreach (var e in o.ChassisDraftErrors) i.ChassisDraftErrors[e.Key] = e.Value;
                if (o.SafetyPitDraftErrors != null) foreach (var e in o.SafetyPitDraftErrors) i.SafetyPitDraftErrors[e.Key] = e.Value;
                return i;
            };
            var r = new Rendered { Sheet = CalculationSheet.Build(p, ctx, inputs()) };
            if (r.Sheet.HasContent)
            {
                using (var m = new MemoryStream()) { r.Sheet.Render().Save(m); r.SheetDocx = m.ToArray(); }
                foreach (var f in r.Sheet.Figures) r.Svgs.Add(FigureRender.Svg(f));
            }
            using (var m = new MemoryStream()) { Elevator.Reporting.DesignNotes.DesignNotes.Build(p, ctx, inputs()).Render().Save(m); r.NotesDocx = m.ToArray(); }
            return r;
        }

        // ---------- DOCX reading ----------
        static string Text(byte[] docx)
        {
            var b = new StringBuilder();
            using (var m = new MemoryStream(docx))
            using (var package = Package.Open(m, FileMode.Open, FileAccess.Read))
            {
                foreach (var part in package.GetParts().Where(x => x.Uri.OriginalString.StartsWith("/word/document") || x.Uri.OriginalString.StartsWith("/word/header") || x.Uri.OriginalString.StartsWith("/word/footer")).OrderBy(x => x.Uri.OriginalString == "/word/document.xml" ? 0 : 1).ThenBy(x => x.Uri.OriginalString, StringComparer.Ordinal))
                {
                    var xml = Xml(part); var ns = Ns(xml);
                    b.Append("=== ").Append(part.Uri.OriginalString).Append('\n');
                    foreach (XmlNode p in xml.SelectNodes("//w:p", ns))
                    {
                        foreach (XmlNode t in p.SelectNodes(".//w:t|.//w:br|.//w:tab|.//w:instrText", ns))
                            b.Append(t.LocalName == "br" ? "\n" : t.LocalName == "tab" ? "\t" : t.LocalName == "instrText" ? "{" + t.InnerText.Trim() + "}" : t.InnerText);
                        b.Append('\n');
                    }
                }
            }
            return b.ToString();
        }
        static XmlDocument Xml(PackagePart part) { var x = new XmlDocument(); using (var s = part.GetStream()) x.Load(s); return x; }
        static XmlNamespaceManager Ns(XmlDocument x) { var ns = new XmlNamespaceManager(x.NameTable); ns.AddNamespace("w", W); return ns; }
        static T WithPackage<T>(byte[] docx, Func<Package, T> f) { using (var m = new MemoryStream(docx)) using (var p = Package.Open(m, FileMode.Open, FileAccess.Read)) return f(p); }

        static void AssertApprovedText(string rel, string text)
        {
            string path = Path.Combine(Golden, "approved", rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), "No approved text " + rel);
            string approved = File.ReadAllText(path, Utf8);
            if (approved == text) return;
#if !NETFRAMEWORK
            // .NET 8: numbers written with double.ToString() can have more digits (docs/TESTING.md); text otherwise identical.
            var x = approved.Split('\n'); var y = text.Split('\n');
            var number = new Regex(@"-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?");
            if (x.Length == y.Length && Enumerable.Range(0, x.Length).All(i => x[i] == y[i] || (number.Replace(x[i], "#") == number.Replace(y[i], "#") && NumbersClose(number, x[i], y[i])))) return;
#endif
            Assert.Fail("Text differs from approved " + rel);
        }
        static bool NumbersClose(Regex number, string a, string b)
        {
            var p = number.Matches(a).Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            var q = number.Matches(b).Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            return p.Count == q.Count && p.Zip(q, (u, v) => Math.Abs(u - v) <= 1e-12 * Math.Max(1, Math.Max(Math.Abs(u), Math.Abs(v)))).All(ok => ok);
        }

        // ---------- tests ----------
        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Sheet_and_design_notes_text_equal_the_approved_golden_text(string name)
        {
            var r = Render(name);
            Assert.Equal(File.Exists(Path.Combine(Golden, "approved", name, "sheet.txt")), r.Sheet.HasContent);
            if (r.Sheet.HasContent) AssertApprovedText(name + "/sheet.txt", Text(r.SheetDocx));
            AssertApprovedText(name + "/design-notes.txt", Text(r.NotesDocx));
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Sheet_has_no_forbidden_text_and_every_figure_as_svg(string name)
        {
            var r = Render(name);
            if (!r.Sheet.HasContent) return;
            string props = WithPackage(r.SheetDocx, p => string.Concat(p.GetParts().Where(x => x.ContentType.Contains("core-properties")).Select(x => Xml(x).InnerText)));
            string all = (Text(r.SheetDocx) + "\n" + props).ToLowerInvariant();
            foreach (var bad in Removed) Assert.False(all.Contains(bad), name + ": forbidden text '" + bad + "'");
            string sheetText = Text(r.SheetDocx);
            Assert.False(sheetText.Contains("NaN") || sheetText.Contains("Infinity"), name + ": non-finite number");
            Assert.Contains(CalculationSheet.DesignBasis, Text(r.SheetDocx));
            var svgParts = WithPackage(r.SheetDocx, p => p.GetParts().Where(x => x.ContentType == "image/svg+xml").Select(x => { using (var s = x.GetStream()) using (var rd = new StreamReader(s)) return rd.ReadToEnd(); }).ToList());
            Assert.Equal(r.Sheet.Figures.Count, svgParts.Count);
            Assert.Empty(r.Sheet.FigureErrors);
            foreach (var svg in svgParts) { var x = new XmlDocument(); x.LoadXml(svg); Assert.Equal("svg", x.DocumentElement.LocalName); Assert.NotNull(x.DocumentElement.Attributes["data-figure"]); }
            Assert.Equal(r.Svgs.OrderBy(s => s, StringComparer.Ordinal), svgParts.OrderBy(s => s, StringComparer.Ordinal));
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Sheet_has_the_page_frame_header_title_block_and_footer(string name)
        {
            var r = Render(name);
            if (!r.Sheet.HasContent) return;
            WithPackage(r.SheetDocx, p =>
            {
                var doc = Xml(p.GetPart(new Uri("/word/document.xml", UriKind.Relative))); var ns = Ns(doc);
                var sect = doc.SelectSingleNode("//w:body/w:sectPr", ns);
                Assert.NotNull(sect);
                Assert.NotNull(sect.SelectSingleNode("w:pgBorders", ns));
                Assert.Single(sect.SelectNodes("w:headerReference", ns).Cast<XmlNode>());
                Assert.Single(sect.SelectNodes("w:footerReference", ns).Cast<XmlNode>());
                Assert.Null(sect.SelectSingleNode("w:titlePg", ns));
                var header = Xml(p.GetPart(new Uri("/word/header1.xml", UriKind.Relative)));
                Assert.NotEmpty(header.SelectNodes("//w:tbl", Ns(header)).Cast<XmlNode>());
                var footer = Xml(p.GetPart(new Uri("/word/footer1.xml", UriKind.Relative)));
                Assert.Contains("Prepared with ShaftMaster", footer.InnerText);
                return true;
            });
            string text = Text(r.SheetDocx);
            Assert.Contains("Elevator Calculation Sheet\n", text);
            Assert.Contains("Calculation\nKey result\n", text);
        }

        [Fact]
        public void Title_block_shows_filled_fields_and_leaves_blank_fields_out()
        {
            string filled = Text(Render("titleblock-filled").SheetDocx), blank = Text(Render("titleblock-blank").SheetDocx);
            foreach (var v in new[] { "Delta Developments", "NL-CS-001", "Nile Lifts Engineering", "A. Engineer", "B. Checker", "C. Approver" }) Assert.Contains(v, filled);
            Assert.Contains("Doc. No.", filled);
            Assert.DoesNotContain("Doc. No.", blank);
            Assert.DoesNotContain("Client", blank);
            int headerImages = WithPackage(Render("titleblock-filled").SheetDocx, p => p.GetPart(new Uri("/word/header1.xml", UriKind.Relative)).GetRelationships().Count(x => x.RelationshipType.EndsWith("/image")));
            Assert.Equal(1, headerImages);
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Design_notes_have_no_tables_and_short_notes(string name)
        {
            var r = Render(name);
            WithPackage(r.NotesDocx, p =>
            {
                var doc = Xml(p.GetPart(new Uri("/word/document.xml", UriKind.Relative)));
                Assert.Empty(doc.SelectNodes("//w:body//w:tbl", Ns(doc)).Cast<XmlNode>());
                return true;
            });
            // The body only (as CalculationSheetTests.DesignNotesClean).
            string text = Text(r.NotesDocx), lower = text.Split(new[] { "=== /word/header", "=== /word/footer" }, StringSplitOptions.None)[0].ToLowerInvariant();
            foreach (var bad in new[] { "basis", "code checks", "status", "project value", "requirement", "calculation sheet shows" }) Assert.DoesNotContain(bad, lower);
            Assert.True(text.Contains("No design notes for this project.") || text.Contains("Warnings") || text.Contains("Recommendations") || text.Contains("Notes"), name + ": Design Notes heading");
        }
    }
}
