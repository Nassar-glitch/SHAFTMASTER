using System;
using System.IO;
using Elevator.Core.Layouts;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>One Generate Drawings run with a sheet: the three files and where they go.</summary>
    public sealed class OutputSetRequest
    {
        public ElevatorProject Project;
        public GenerationOptions Options;
        public SheetRequest Sheet;
        /// <summary>The output folder the user chose.</summary>
        public string Folder;
        /// <summary>The three file names (one base name).</summary>
        public DrawingFileNames Names;
        /// <summary>True when the user decided to replace existing files of the set; false: an existing file stops the run.</summary>
        public bool Overwrite;
        public Action<string> Progress;
        /// <summary>Writes the project file (the desktop saves its session there); null: the project JSON is written.</summary>
        public Action<string> SaveProject;
    }

    /// <summary>What a run wrote.</summary>
    public sealed class OutputSetResult
    {
        public string Dwg;
        public string Pdf;
        public string ProjectFile;
        public GenerationResponse Generation;
        /// <summary>Why the PDF is missing (the DWG and the project file are still written); null when it was written.</summary>
        public string PdfError;
        /// <summary>Why the project file is missing; null when it was written.</summary>
        public string ProjectError;

        public bool Success
        {
            get { return Generation != null && Generation.Success && PdfError == null && ProjectError == null; }
        }
    }

    /// <summary>
    /// The output set of one run: the DWG with the sheet, the PDF of that sheet and the project file, one base name, in the folder the
    /// user chose. Never writes into the source drawing or the template; an existing file is replaced only when the caller says the
    /// user decided so. An error of the PDF step does not lose the DWG or the project file.
    /// </summary>
    public static class OutputSetWriter
    {
        public static OutputSetResult Write(GenerationClient client, OutputSetRequest request)
        {
            if (request == null || request.Project == null || request.Sheet == null || request.Names == null)
                throw new ArgumentException("The output set needs a project, a sheet and the file names.");
            string folder = Path.GetFullPath(request.Folder);
            var result = new OutputSetResult
            {
                Dwg = Path.Combine(folder, request.Names.Dwg),
                Pdf = Path.Combine(folder, request.Names.Pdf),
                ProjectFile = Path.Combine(folder, request.Names.Project)
            };
            bool exists = File.Exists(result.Dwg) || File.Exists(result.Pdf) || File.Exists(result.ProjectFile);
            if (exists && !request.Overwrite)
                throw new IOException("Output files already exist; the run needs the decision to replace them or to use the next revision.");
            Action<string> progress = request.Progress ?? (m => { });
            Directory.CreateDirectory(folder);
            var generation = new GenerationRequest
            {
                ProjectJson = ProjectJson.Serialize(request.Project),
                Options = new GenerationOptions { Views = request.Options.Views, ArrangeViews = request.Options.ArrangeViews },
                Sheet = request.Sheet
            };
            var consent = OutputConsent.Capture(result.Dwg, request.Overwrite);
            result.Generation = client.Run(generation, consent, progress);
            if (!result.Generation.Success)
                return result;
            try
            {
                if (request.SaveProject != null)
                    request.SaveProject(result.ProjectFile);
                else
                    File.WriteAllText(result.ProjectFile, generation.ProjectJson);
            }
            catch (Exception e)
            {
                result.ProjectError = e.Message;
            }
            progress("Plotting PDF…");
            try
            {
                var sheet = result.Generation.Sheet;
                client.PlotPdf(
                    result.Dwg,
                    result.Pdf,
                    sheet == null ? SheetLayout.Name : sheet.LayoutName,
                    sheet,
                    sheet != null && sheet.LogoPlaced ? request.Sheet.LogoFile : null,
                    request.Sheet.LogoWidthPx,
                    request.Sheet.LogoHeightPx);
            }
            catch (Exception e)
            {
                result.PdfError = e.Message;
            }
            return result;
        }
    }
}
