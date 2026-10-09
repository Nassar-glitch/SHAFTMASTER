using System;
using System.IO;
using System.Web.Script.Serialization;
using Elevator.Core.Geometry;
using Elevator.Core.Project;
using Elevator.Core.Serialization;

namespace Elevator.Solution.Bridge
{
    /// <summary>The user's consent to write or replace an output file.</summary>
    public sealed class OutputConsent
    {
        public string Path;
        public string PreviousHash;
        public bool ReplaceExisting;

        /// <summary>Checks the output path and records whether an existing file may be replaced.</summary>
        public static OutputConsent Capture(string path, bool confirmed)
        {
            path = System.IO.Path.GetFullPath(path);
            if (System.IO.Path.GetExtension(path).ToLowerInvariant() != ".dwg")
                throw new ArgumentException("Output must have a .dwg extension.");
            bool exists = File.Exists(path);
            if (exists && !confirmed)
                throw new IOException("Existing output requires explicit replacement confirmation.");
            return new OutputConsent
            {
                Path = path,
                PreviousHash = exists ? Wire.HashFile(path) : null,
                ReplaceExisting = exists && confirmed
            };
        }

        /// <summary>Moves the staged file to the output path.</summary>
        public void Publish(string staged)
        {
            string folder = System.IO.Path.GetDirectoryName(Path);
            Directory.CreateDirectory(folder);
            // A same-directory staging file enables atomic rename/replace; never save directly to the final drawing.
            string temporary = System.IO.Path.Combine(folder, ".elevator-" + Guid.NewGuid().ToString("N") + ".dwg");
            try
            {
                File.Copy(staged, temporary, false);
                if (ReplaceExisting)
                {
                    if (!File.Exists(Path) || Wire.HashFile(Path) != PreviousHash)
                        throw new IOException("Output changed since confirmation; replacement cancelled.");
                    File.Replace(temporary, Path, null);
                }
                else
                    File.Move(temporary, Path); // fails if another writer created the output
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
