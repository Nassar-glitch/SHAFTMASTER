using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Runs the AutoCAD Core Console host for a request.</summary>
    public sealed class GenerationClient
    {
        public readonly string Root;
        public readonly string Bin;
        public readonly string ConsolePath;
        public string LastJobDirectory;

        public GenerationClient(string root, string bin, string consolePath)
        {
            Root = root;
            Bin = bin;
            ConsolePath = consolePath;
        }

        /// <summary>Generates the drawing of a project in the background.</summary>
        public Task<GenerationResponse> GenerateAsync(
            ElevatorProject project,
            GenerationOptions options,
            OutputConsent consent,
            Action<string> progress)
        {
            return GenerateAsync(project, options, consent, progress, null);
        }

        /// <summary>Generates the drawing of a project in the background and adds the sheet (layout, viewports, title block) when given.</summary>
        public Task<GenerationResponse> GenerateAsync(
            ElevatorProject project,
            GenerationOptions options,
            OutputConsent consent,
            Action<string> progress,
            Elevator.Core.Layouts.SheetRequest sheet)
        {
            // Snapshot synchronously before background work; further UI changes cannot modify an in-flight request.
            var request = new GenerationRequest
            {
                ProjectJson = ProjectJson.Serialize(project),
                Options = new GenerationOptions { Views = options.Views, ArrangeViews = options.ArrangeViews },
                Sheet = sheet
            };
            return Task.Run(() => Run(request, consent, progress));
        }

        /// <summary>Runs a generation request and checks the response.</summary>
        public GenerationResponse Run(GenerationRequest request, OutputConsent consent, Action<string> progress)
        {
            var timer = Stopwatch.StartNew();
            var failure = new GenerationResponse { RequestId = request.RequestId, InputHash = Wire.HashText(request.ProjectJson) };
            try
            {
                var project = Wire.Validate(request);
                var issues = ProjectValidator.Validate(project, request.Options.Views);
                if (issues.Any(i => i.BlocksGeneration))
                {
                    failure.Validation = issues;
                    throw new ArgumentException(String.Join("; ", issues.Where(i => i.BlocksGeneration).Select(i => i.Message)));
                }
                if (!File.Exists(ConsolePath))
                    throw new FileNotFoundException("AutoCAD 2023 Core Console is required.", ConsolePath);
                LastJobDirectory = Path.Combine(Root, "output", "solution-v01", "jobs", request.RequestId);
                Directory.CreateDirectory(LastJobDirectory);
                Wire.Write(Path.Combine(LastJobDirectory, "request.json"), request);
                File.WriteAllText(Path.Combine(LastJobDirectory, "project.elevator.json"), request.ProjectJson);
                string scratch = Path.Combine(LastJobDirectory, "disposable.dwg");
                File.Copy(Path.Combine(Root, "Templates", "ExistingPlan_Master.dwg"), scratch, false);
                progress("Generating in a disposable AutoCAD drawing…");
                RunHost(scratch, "generate", LastJobDirectory);
                var result = ReadResponse(request, "response.json");
                if (!result.Success)
                    return result;
                progress("Reopening and auditing the generated DWG in a fresh process…");
                string staged = Path.Combine(LastJobDirectory, "generated.dwg");
                RunHost(staged, "verify", LastJobDirectory);
                var verified = ReadResponse(request, "verified.json");
                if (!verified.Success)
                    return verified;
                progress("Publishing the verified drawing…");
                consent.Publish(staged);
                result.OutputPath = consent.Path;
                result.ElapsedSeconds = timer.Elapsed.TotalSeconds;
                Wire.Write(Path.Combine(LastJobDirectory, "published.json"), result);
                return result;
            }
            catch (Exception e)
            {
                failure.Error = e.ToString();
                failure.ElapsedSeconds = timer.Elapsed.TotalSeconds;
                return failure;
            }
        }

        GenerationResponse ReadResponse(GenerationRequest request, string filename)
        {
            var r = Wire.Read<GenerationResponse>(Path.Combine(LastJobDirectory, filename));
            if (r.ProtocolVersion != BridgeContract.Version
                || r.RequestId != request.RequestId
                || r.InputHash != Wire.HashText(request.ProjectJson))
                throw new InvalidDataException("Host response does not match the project snapshot.");
            return r;
        }

        /// <summary>
        /// Plots the layout of a drawing to PDF in the Core Console (a copy of the drawing is plotted, never the file itself), puts the
        /// logo onto the page when given, and writes the PDF to <paramref name="pdfPath"/> (replacing it).
        /// </summary>
        public PdfPlotResult PlotPdf(string dwg, string pdfPath, string layout, Elevator.Core.Layouts.SheetResult sheet, string logoFile, int logoWidthPx, int logoHeightPx)
        {
            if (!File.Exists(ConsolePath))
                throw new FileNotFoundException("AutoCAD 2023 Core Console is required.", ConsolePath);
            if (!File.Exists(dwg))
                throw new FileNotFoundException("The drawing to plot is missing.", dwg);
            string job = Path.Combine(Root, "output", "solution-v01", "jobs", "plot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            LastJobDirectory = job;
            string copy = Path.Combine(job, "plot.dwg"), pdf = Path.Combine(job, "sheet.pdf");
            File.Copy(dwg, copy, true);
            Wire.Write(Path.Combine(job, "plot-request.json"), new PdfPlotRequest { Layout = layout, Pdf = pdf });
            RunHost(copy, "plot", job, 300000);
            string answer = Path.Combine(job, "plot.json");
            if (!File.Exists(answer))
                throw new IOException("The plot host gave no answer; see " + Path.Combine(job, "plot.log") + ".");
            var result = Wire.Read<PdfPlotResult>(answer);
            if (!result.Success)
                throw new InvalidOperationException("Plotting the PDF failed: " + result.Error);
            if (!String.IsNullOrWhiteSpace(logoFile) && sheet != null)
                PdfLogoOverlay.Apply(pdf, logoFile, sheet.LogoAreaX, sheet.LogoAreaY, sheet.LogoAreaWidth, sheet.LogoAreaHeight, logoWidthPx, logoHeightPx);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pdfPath)));
            File.Copy(pdf, pdfPath, true);
            return result;
        }

        /// <summary>Starts the Core Console with the host script.</summary>
        public void RunHost(string drawing, string mode, string job)
        {
            RunHost(drawing, mode, job, 600000);
        }

        /// <summary>Starts the Core Console with the host script; a timeout kills it and the run fails (the log stays in the job folder).</summary>
        public void RunHost(string drawing, string mode, string job, int timeoutMs)
        {
            string plugin = Path.Combine(Bin, "Elevator.Solution.Host.dll").Replace('\\', '/');
            if (plugin.Contains("\"") || plugin.Contains("\n"))
                throw new ArgumentException("Invalid installation path.");
            string script = Path.Combine(job, mode + ".scr");
            // Only runtime bootstrap: the host calls the typed API; no elevator drawing commands are sent.
            File.WriteAllText(
                script,
                "FILEDIA\n0\nSECURELOAD\n0\n_.NETLOAD\n\"" + plugin + "\"\nSOLUTIONHOST\n" + job + "\n" + mode + "\n_.QUIT\n_N\n");
            var psi = new ProcessStartInfo(ConsolePath, "/i \"" + drawing + "\" /s \"" + script + "\" /l en-US")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = job
            };
            using (var process = Process.Start(psi))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    process.WaitForExit();
                    throw new TimeoutException("The AutoCAD host exceeded " + (timeoutMs / 60000) + " minutes; no output published.");
                }
                Task.WaitAll(stdout, stderr);
                File.WriteAllText(Path.Combine(job, mode + ".log"), stdout.Result + stderr.Result);
                if (process.ExitCode != 0)
                    throw new IOException("AutoCAD host exited with code " + process.ExitCode + "; see job log.");
            }
        }
    }
}
