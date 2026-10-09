using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Runs the SOLIDWORKS STEP worker for a project.</summary>
    public sealed class StepExportClient
    {
        readonly string bin;

        public StepExportClient(string applicationDirectory)
        {
            bin = applicationDirectory;
        }

        /// <summary>Checks that the export files and SOLIDWORKS are present.</summary>
        public void CheckDependencies()
        {
            foreach (var file in new[]
            {
                "Elevator.Solution.StepExport.exe",
                "SolidWorks.Interop.sldworks.dll",
                "SolidWorks.Interop.swconst.dll",
                "preliminary-3d-reference.json",
                "reviewed-context.json"
            })
            {
                if (!File.Exists(Path.Combine(bin, file)))
                {
                    throw new FileNotFoundException(
                                            "STEP export package is incomplete: " + file + ". Keep the application folder together.");
                }
            }
            if (Type.GetTypeFromProgID("SldWorks.Application") == null)
            {
                throw new InvalidOperationException(
                                    "STEP export requires installed, licensed SOLIDWORKS with a configured default part template. AutoCAD "
                                    + "DWG generation "
                                    + "remains available separately.");
            }
        }

        /// <summary>Exports the project to a STEP file in the background.</summary>
        public Task<StepExportResult> ExportAsync(
            ElevatorProject project,
            string outputPath,
            bool replaceConfirmed,
            Action<string> progress)
        {
            // Freeze current inputs before handing work to the worker. No source drawing is read or modified.
            var snapshot = ProjectJson.Serialize(project);
            outputPath = Path.GetFullPath(outputPath);
            string extension = Path.GetExtension(outputPath).ToLowerInvariant();
            if (extension != ".step" && extension != ".stp")
                throw new ArgumentException("Choose a .step or .stp filename.");
            bool existed = File.Exists(outputPath);
            if (existed && !replaceConfirmed)
                throw new IOException("STEP replacement was not confirmed.");
            string previous = existed ? Wire.HashFile(outputPath) : null;
            return Task.Run(() => Run(snapshot, outputPath, existed, previous, progress));
        }

        StepExportResult Run(string snapshot, string output, bool existed, string previous, Action<string> progress)
        {
            CheckDependencies();
            var watch = Stopwatch.StartNew();
            ElevatorDesignResult design;
            progress("Calculating 3D geometry from current project inputs…");
            var input = StepInputFactory.Create(
                ProjectJson.Deserialize(snapshot),
                Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json")),
                Wire.Read<StepModelParameters>(Path.Combine(bin, "preliminary-3d-reference.json")),
                out design);
            // Editable native source, inputs and verification accompany the STEP in a unique sibling folder.
            string folder = WriteSourceFolder(snapshot, output, design, input);
            ProcessStartInfo start = WorkerStartInfo(folder);
            try
            {
                RunWorker(progress, folder, start);
                var verification = Wire.Read<StepVerification>(Path.Combine(folder, "verification.json"));
                if (verification.SourceSolidCount < 1
                    || verification.SourceSolidCount != verification.ReopenedSolidCount
                    || verification.FaultCount != 0
                    || verification.Units != "mm")
                    throw new InvalidDataException("STEP verification failed; no final file was published.");
                progress("Publishing the verified STEP file…");
                Publish(Path.Combine(folder, "Elevator_Assembly.step"), output, existed, previous);
                File.WriteAllText(
                    Path.Combine(folder, "README.txt"),
                    "Preliminary elevator assembly; millimetres. X = shaft width, Y = shaft depth from entrance, Z = elevation. Editable "
                    + "SOLIDWORKS source: Elevator_Assembly.SLDPRT. Parameters.json records the generated dimensions. "
                    + "project.elevator.json records the exact project snapshot. Engineering statuses remain independent and this export "
                    + "is not manufacturing or code certification. Detailed hardware, ropes and manufacturer envelopes are deferred. See "
                    + "the packaged STEP-EXPORT.md for retained assumptions.\r\n");
                return new StepExportResult
                {
                    OutputPath = output,
                    SourceFolder = folder,
                    Verification = verification,
                    ElapsedSeconds = watch.Elapsed.TotalSeconds,
                    LayoutStatus = design.LayoutStatus.ToString(),
                    WrapAngleStatus = design.WrapAngleStatus.ToString(),
                    CodeDiameterRatioStatus = design.CodeDiameterRatioStatus.ToString()
                };
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(e.Message + "\nExport details: " + folder, e);
            }
        }

        private string WriteSourceFolder(string snapshot, string output, ElevatorDesignResult design, StepModelParameters input)
        {
            string folder = Path.Combine(
                            Path.GetDirectoryName(output),
                            Path.GetFileNameWithoutExtension(output) + "_source_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            Wire.Write(Path.Combine(folder, "Parameters.json"), input);
            File.WriteAllText(Path.Combine(folder, "project.elevator.json"), snapshot);
            Wire.Write(
                Path.Combine(folder, "engineering-statuses.json"),
                new { design.LayoutStatus, design.WrapAngleStatus, design.CodeDiameterRatioStatus, design.Validation });
            return folder;
        }

        private ProcessStartInfo WorkerStartInfo(string folder)
        {
            var start =
                            new ProcessStartInfo(Path.Combine(bin, "Elevator.Solution.StepExport.exe"), "\"" + folder + "\"")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                WorkingDirectory = bin,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                StandardOutputEncoding = System.Text.Encoding.UTF8
                            };
            return start;
        }

        private void RunWorker(Action<string> progress, string folder, ProcessStartInfo start)
        {
            using (var process = new Process { StartInfo = start })
            {
                var log = new System.Text.StringBuilder();
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (log)
                            log.AppendLine(e.Data);
                        progress(e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                var errors = process.StandardError.ReadToEndAsync();
                // The worker owns COM on an STA thread. The desktop remains responsive while awaiting this background task.
                process.WaitForExit();
                string error = errors.GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(folder, "export.log"), log.ToString() + error);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(String.IsNullOrWhiteSpace(error) ? "The STEP worker failed." : error.Trim());
            }
        }

        static void Publish(string staged, string path, bool existed, string previous)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".step-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(staged, temporary, false);
                if (existed)
                {
                    if (!File.Exists(path) || Wire.HashFile(path) != previous)
                    {
                        throw new IOException(
                                            "The destination changed during export; replacement cancelled.");
                    }
                    File.Replace(
                        temporary,
                        path,
                        null);
                }
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
