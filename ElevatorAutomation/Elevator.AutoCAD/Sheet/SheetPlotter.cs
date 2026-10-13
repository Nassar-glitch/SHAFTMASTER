using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Elevator.Core.Layouts;

namespace Elevator.AutoCAD.Sheet
{
    /// <summary>
    /// Plots the sheet layout to PDF in the Core Console with PlotSettingsValidator and PlotEngine (no -PLOT script): device
    /// "DWG To PDF.pc3", the full-bleed A0 media, landscape, plot area layout, 1:1, plot style table monochrome.ctb, BACKGROUNDPLOT 0.
    /// A device, media or style table that is missing is a clear error.
    /// </summary>
    public static class SheetPlotter
    {
        public const string Device = "DWG To PDF.pc3";
        public const string StyleSheet = "monochrome.ctb";

        /// <summary>Plots the layout of the database to the PDF file. Returns the canonical media name used.</summary>
        public static string Plot(Database db, string layoutName, string pdfPath)
        {
            if (String.IsNullOrWhiteSpace(pdfPath))
                throw new ArgumentException("The PDF path is missing.");
            var doc = Application.DocumentManager.MdiActiveDocument;
            var validator = PlotSettingsValidator.Current;
            if (!Contains(validator.GetPlotDeviceList(), Device))
                throw new InvalidOperationException("The plot device \"" + Device + "\" is not available in this AutoCAD installation.");
            if (!Contains(validator.GetPlotStyleSheetList(), StyleSheet))
                throw new InvalidOperationException("The plot style table \"" + StyleSheet + "\" is not available in this AutoCAD installation.");
            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                throw new InvalidOperationException("AutoCAD is already plotting.");
            Application.SetSystemVariable("BACKGROUNDPLOT", 0);
            try
            {
                // SHX text of the sheet is stored in the PDF as searchable text (comments) as well as drawn
                Application.SetSystemVariable("PDFSHX", 1);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
            }
            var layouts = LayoutManager.Current;
            if (!LayoutExists(db, layoutName))
                throw new InvalidOperationException("The drawing has no layout " + layoutName + ".");
            layouts.CurrentLayout = layoutName;
            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
            string mediaUsed = null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layout = (Layout)tr.GetObject(layouts.GetLayoutId(layoutName), OpenMode.ForRead);
                var settings = new PlotSettings(layout.ModelType);
                settings.CopyFrom(layout);
                validator.SetPlotConfigurationName(settings, Device, null);
                validator.RefreshLists(settings);
                string media = null;
                foreach (string m in validator.GetCanonicalMediaNameList(settings))
                {
                    if (m.IndexOf("full_bleed_A0", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("841", StringComparison.Ordinal) >= 0)
                    {
                        media = m;
                        break;
                    }
                }
                if (media == null)
                    throw new InvalidOperationException("The paper size \"ISO full bleed A0\" is not available for " + Device + ".");
                mediaUsed = media;
                validator.SetCanonicalMediaName(settings, media);
                validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                validator.SetPlotRotation(settings, PlotRotation.Degrees090);
                validator.SetUseStandardScale(settings, false);
                validator.SetCustomPrintScale(settings, new CustomScale(1, 1));
                validator.SetCurrentStyleSheet(settings, StyleSheet);
                var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
                var infoValidator = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
                infoValidator.Validate(info);
                using (var engine = PlotFactory.CreatePublishEngine())
                {
                    engine.BeginPlot(null, null);
                    engine.BeginDocument(info, doc.Name, null, 1, true, pdfPath);
                    var page = new PlotPageInfo();
                    engine.BeginPage(page, info, true, null);
                    engine.BeginGenerateGraphics(null);
                    engine.EndGenerateGraphics(null);
                    engine.EndPage(null);
                    engine.EndDocument(null);
                    engine.EndPlot(null);
                }
                tr.Abort();
            }
            if (!File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                throw new IOException("AutoCAD did not write the PDF file.");
            return mediaUsed;
        }

        static bool Contains(System.Collections.Specialized.StringCollection list, string name)
        {
            foreach (string s in list)
                if (String.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static bool LayoutExists(Database db, string name)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                bool found = dict.Contains(name);
                tr.Commit();
                return found;
            }
        }
    }
}
