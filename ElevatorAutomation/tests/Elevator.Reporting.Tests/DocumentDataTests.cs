using System;
using System.IO;
using System.Linq;
using System.Threading;
using Elevator.Core.Company;
using Elevator.Core.Layouts;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Tests;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
using Elevator.Reporting.Sheet;
#if NETFRAMEWORK
using Elevator.Solution.Desktop.Common;
#endif
using Xunit;

namespace Elevator.Reporting.Tests
{
    // Cloud L2a: the calculation sheet, the Design Notes and the drawing title block show the one set of company / report data
    // (Elevator.Core.Company.DocumentData): a company name the user has not entered is left out everywhere, and the revision is the
    // same text on every document ("" when blank, NN for a number, a stored "A" as it is).
    public class DocumentDataTests
    {
        static readonly DesignContext Ctx = TestData.Context();
        static readonly PresetLibrary Lib = PresetLibrary.Load(Path.Combine(TestData.Bin, "presets.json"));
        static readonly DateTime Day = new DateTime(2026, 10, 12, 9, 30, 0);
        sealed class OnePixel : IFigureRasterizer
        {
            static readonly byte[] Png1 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
            public void Initialise() { }
            public byte[] Png(Figure f, double dpi) { return Png1; }
        }

        static ElevatorProject New()
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(TestData.Bin, "reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(Lib, Ctx, seed.Counterweight, seed.Rails.BracketSource);
        }

        // The sheet and the Design Notes rendered with a company name (as the application passes it from the company settings).
        static DocxDocument[] Render(ElevatorProject p, string company)
        {
            DocxDocument[] result = null; Exception error = null;
            // WPF measurement needs an STA thread on .NET Framework (same set-up as ReportContentTests).
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
                    var i = new CalculationSheetInputs { Presets = Lib, CompanyName = company, GeneratedLocal = Day };
                    result = new[] { CalculationSheet.Build(p, Ctx, i).Render(), DesignNotes.DesignNotes.Build(p, Ctx, i).Render() };
                }
                catch (Exception e) { error = e; }
            });
#if NETFRAMEWORK
            t.SetApartmentState(ApartmentState.STA);
#endif
            t.Start(); t.Join();
            if (error != null) throw new Exception("Rendering failed", error);
            return result;
        }

        [Fact]
        public void Every_document_shows_the_same_company_and_revision()
        {
            var p = New();
            p.Info.Name = "VILLA bx12";
            p.Report = new ProjectReportDetails { ClientName = " Delta ", Revision = "1", PreparedBy = " A. Engineer " };
            var t = TitleBlockData.Build(p, Lib, new CompanySettings { CompanyName = "Nile Lifts" }, null, null, null);
            Assert.Equal("01", t.Revision);
            foreach (var d in Render(p, "Nile Lifts"))
            {
                Assert.Equal(t.CompanyName, d.TitleBlock.Company);
                Assert.Contains(d.TitleBlock.DocumentRows, r => r.Key == "Rev." && r.Value == t.Revision);
                Assert.Contains(d.TitleBlock.ProjectRows, r => r.Key == "Client" && r.Value == t.Client);
                Assert.Equal("Rev 01", d.FooterRight);
            }
        }

        [Fact]
        public void A_company_name_not_entered_is_left_out_everywhere()
        {
            var p = New();
            foreach (var company in new[] { null, "", "   " })
            {
                foreach (var d in Render(p, company))
                    Assert.Null(d.TitleBlock.Company);
                Assert.Equal("", TitleBlockData.Build(p, Lib, new CompanySettings { CompanyName = company }, null, null, null).CompanyName);
            }
        }

        [Fact]
        public void A_stored_letter_revision_is_shown_as_it_is_and_a_blank_one_is_left_out()
        {
            var p = New();
            p.Report = new ProjectReportDetails { Revision = "A" };
            foreach (var d in Render(p, null))
            {
                Assert.Contains(d.TitleBlock.DocumentRows, r => r.Key == "Rev." && r.Value == "A");
                Assert.Equal("Rev A", d.FooterRight);
            }
            Assert.Equal("A", TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
            Assert.NotNull(DrawingRevision.Validate(p));
            Assert.Equal("A", p.Report.Revision);
            p.Report.Revision = null;
            foreach (var d in Render(p, null))
            {
                Assert.DoesNotContain(d.TitleBlock.DocumentRows, r => r.Key == "Rev.");
                Assert.Null(d.FooterRight);
            }
            Assert.Equal("", TitleBlockData.Build(p, Lib, null, null, null, null).Revision);
        }
    }
}
