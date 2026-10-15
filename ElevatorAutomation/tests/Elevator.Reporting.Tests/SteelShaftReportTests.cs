using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Steel;
using Elevator.Core.Tests;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Figures;
using Elevator.Reporting.Sheet;
#if NETFRAMEWORK
using Elevator.Solution.Desktop.Common;
#endif
using Xunit;

namespace Elevator.Reporting.Tests
{
    // The "Shaft structure" input block of the calculation sheet and the Design Notes line of the steel shaft (DESIGN-FLOW "Steel shaft
    // structure"): only for a steel shaft, inputs only, no structural check, no remark text.
    public class SteelShaftReportTests
    {
        static ElevatorProject Project(bool steel, double? beamSpacing = null, double? floorHeight = null)
        {
            var lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
            var ctx = TestData.Context();
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
            if (floorHeight.HasValue)
            {
                foreach (var f in p.Building.Floors.Where(f => f.HeightToNextMm.HasValue))
                    f.HeightToNextMm = floorHeight;
            }
            if (steel)
                SteelFields.Construction.Edit(p, SteelShaftPreset.SteelStructure);
            if (beamSpacing.HasValue)
                ProjectPresets.Find(ProjectPresets.CarSpacingKey).Edit(p, beamSpacing.Value);
            ProjectPresets.Apply(p, lib, ctx);
            return p;
        }

        static CalculationSheetInputs Inputs()
        {
            return new CalculationSheetInputs { Presets = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json")) };
        }

        sealed class OnePixel : IFigureRasterizer
        {
            static readonly byte[] Png1 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
            public void Initialise() { }
            public byte[] Png(Figure f, double dpi) { return Png1; }
        }

        // WPF measurement needs an STA thread on .NET Framework (same set-up as DocumentDataTests).
        static string SheetText(CalculationSheet sheet)
        {
            string text = null; Exception error = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (typeof(FigureRender))
                    {
#if NETFRAMEWORK
                        if (FigureRender.Rasterizer == null) FigureRender.Rasterizer = new WpfFigureRasterizer();
#else
                        if (FigureRender.Rasterizer == null) FigureRender.Rasterizer = new OnePixel();
#endif
                    }
                    text = SheetTextNow(sheet);
                }
                catch (Exception e) { error = e; }
            });
#if NETFRAMEWORK
            t.SetApartmentState(ApartmentState.STA);
#endif
            t.Start(); t.Join();
            if (error != null) throw new Exception("Rendering failed", error);
            return text;
        }

        static string SheetTextNow(CalculationSheet sheet)
        {
            using (var m = new MemoryStream())
            {
                sheet.Render().Save(m);
                m.Position = 0;
                using (var package = Package.Open(m, FileMode.Open, FileAccess.Read))
                {
                    var part = package.GetPart(new Uri("/word/document.xml", UriKind.Relative));
                    var xml = new XmlDocument();
                    using (var s = part.GetStream())
                        xml.Load(s);
                    var ns = new XmlNamespaceManager(xml.NameTable);
                    ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                    var b = new StringBuilder();
                    foreach (XmlNode paragraph in xml.SelectNodes("//w:p", ns))
                    {
                        foreach (XmlNode t in paragraph.SelectNodes(".//w:t", ns))
                            b.Append(t.InnerText);
                        b.Append('\n');
                    }
                    return b.ToString();
                }
            }
        }

        [Fact]
        public void A_concrete_sheet_has_no_shaft_structure_block_and_no_steel_note()
        {
            var p = Project(false);
            var sheet = CalculationSheet.Build(p, TestData.Context(), Inputs());
            Assert.Null(sheet.Steel);
            Assert.Empty(sheet.SteelRows());
            Assert.DoesNotContain("Shaft structure", SheetText(sheet));
            var notes = DesignNotes.DesignNotes.Build(p, TestData.Context(), Inputs());
            Assert.DoesNotContain(notes.Recommendations, r => r.Contains("door-head"));
        }

        [Fact]
        public void A_steel_sheet_has_the_input_block_with_the_construction_profiles_and_beam_levels()
        {
            var p = Project(true);
            var sheet = CalculationSheet.Build(p, TestData.Context(), Inputs());
            Assert.NotNull(sheet.Steel);
            var rows = sheet.SteelRows().ToDictionary(r => r[0], r => r[1]);
            Assert.Equal("Steel structure (UPN)", rows["Shaft construction"]);
            Assert.Equal("UPN 140", rows["Column profile"]);
            Assert.Equal("UPN 100", rows["Beam profile"]);
            Assert.StartsWith("L50x50x5", rows["Bracing profile"]);
            Assert.Equal("2000 mm", rows["Car rail bracket spacing s"]);
            Assert.Equal("St 37", rows["Steel grade"]);
            Assert.StartsWith("PL 200x200x15, 4 bolts", rows["Base plate"]);
            Assert.StartsWith("0, 2160, 3000", rows["Beam levels, entrance face"]);
            Assert.StartsWith("0, 1500, 3000", rows["Beam levels, back face"]);
            Assert.Equal(rows["Beam levels, left face"], rows["Beam levels, right face"]);
            Assert.Contains(sheet.Steel.LargestGapMm.Value.ToString("0"), rows["Guide-rail bracket spacing (largest gap between beams, side and back faces)"]);
            string text = SheetText(sheet);
            Assert.Contains("Shaft structure", text);
            Assert.Contains("UPN 140", text);
            Assert.Contains("Beam levels, entrance face", text);
            // principle 6 and no structural check
            string lower = text.ToLowerInvariant();
            foreach (var bad in new[] { "preset", "assumed", "not from catalog", "review required", "preliminary", "to verify" })
                Assert.DoesNotContain(bad, lower);
            Assert.DoesNotContain("utilisation", lower);
            Assert.DoesNotContain("buckling of the column", lower);
        }

        [Fact]
        public void The_design_notes_have_no_line_about_a_gap_at_a_door_it_is_by_design()
        {
            foreach (var spacing in new double?[] { null, 1500, 1000 })
            {
                var steel = Project(true, spacing);
                var notes = DesignNotes.DesignNotes.Build(steel, TestData.Context(), Inputs());
                Assert.DoesNotContain(notes.Recommendations, r => r.Contains("door-head") || r.Contains("cannot be split"));
            }
            // a concrete project: nothing
            Assert.DoesNotContain(DesignNotes.DesignNotes.Build(Project(false), TestData.Context(), Inputs()).Recommendations, r => r.Contains("door-head"));
        }

        [Fact]
        public void A_floor_not_higher_than_the_door_is_reported()
        {
            var p = Project(true, 2000, 2100);
            var notes = DesignNotes.DesignNotes.Build(p, TestData.Context(), Inputs());
            Assert.Contains(notes.Recommendations, r => r.Contains("does not fit under"));
        }
    }
}
