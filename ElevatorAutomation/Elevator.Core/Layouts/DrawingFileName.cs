using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Elevator.Core.Company;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// The one base name of the DWG, the PDF and the project file (DESIGN-FLOW "Layouts and PDF"):
    /// COMPANY-TYPE-CW-W{SW}D{SD}-PROJECT-MODEL{NN}, for example ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00. The company and
    /// project names are kept as typed (spaces kept); only the characters Windows forbids in a file name (\ / : * ? " &lt; &gt; | and
    /// control characters) become "_", and trailing dots and spaces are trimmed.
    /// </summary>
    public static class DrawingFileName
    {
        /// <summary>The drawing file extension.</summary>
        public const string DwgExtension = ".dwg";
        /// <summary>The PDF file extension.</summary>
        public const string PdfExtension = ".pdf";
        /// <summary>The project file extension.</summary>
        public const string ProjectExtension = ".elevator.json";

        const string Forbidden = "\\/:*?\"<>|";

        /// <summary>A name part as typed, with the forbidden characters replaced by "_" and trailing dots and spaces trimmed.</summary>
        public static string Clean(string part)
        {
            if (part == null)
                return "";
            var b = new StringBuilder(part.Length);
            foreach (char c in part)
                b.Append(Forbidden.IndexOf(c) >= 0 || Char.IsControl(c) ? '_' : c);
            return b.ToString().TrimEnd('.', ' ');
        }

        /// <summary>The system type code of a project; null for a system that has no code yet.</summary>
        public static ElevatorSystemType? SystemTypeOf(ElevatorProject project)
        {
            if (project == null || project.Elevator == null)
                return null;
            return project.Elevator.SystemId == "traction-geared" ? ElevatorSystemType.GB : (ElevatorSystemType?)null;
        }

        /// <summary>
        /// The counterweight code of a project. Today always RCW: the plan geometry of every current system places the counterweight at
        /// the rear (DesignRules). SCWL / SCWR wait for the side-counterweight layout.
        /// </summary>
        public static CounterweightPositionCode CounterweightOf(ElevatorProject project)
        {
            return CounterweightPositionCode.RCW;
        }

        static string Millimetres(double mm, string name)
        {
            if (Double.IsNaN(mm) || Double.IsInfinity(mm) || mm <= 0)
                throw new ArgumentException(name + " must be a positive number of millimetres.");
            return Math.Round(mm, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>The base name (no extension).</summary>
        public static string BaseName(
            string company,
            ElevatorSystemType type,
            CounterweightPositionCode counterweight,
            double shaftWidthMm,
            double shaftDepthMm,
            string projectName,
            int revision)
        {
            string c = Clean(company), p = Clean(projectName);
            if (c.Trim().Length == 0)
                throw new ArgumentException("The company name is needed for the file name.");
            if (p.Trim().Length == 0)
                throw new ArgumentException("The project name is needed for the file name.");
            return c
                + "-" + type.ToString()
                + "-" + counterweight.ToString()
                + "-W" + Millimetres(shaftWidthMm, "SW") + "D" + Millimetres(shaftDepthMm, "SD")
                + "-" + p
                + "-MODEL" + DrawingRevision.Text(revision);
        }

        /// <summary>
        /// The base name of a project: the company (<see cref="DocumentData"/>: company settings, else the preset library), the system
        /// type, the counterweight code, SW and SD from the shaft, the project name and the revision NN.
        /// </summary>
        public static string BaseName(ElevatorProject project, PresetLibrary library, CompanySettings company)
        {
            return BaseName(project, library, company, CounterweightOf(project));
        }

        /// <summary>The base name of a project with a given counterweight code.</summary>
        public static string BaseName(
            ElevatorProject project,
            PresetLibrary library,
            CompanySettings company,
            CounterweightPositionCode counterweight)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            var type = SystemTypeOf(project);
            if (!type.HasValue)
                throw new ArgumentException("The elevator system has no file-name type code.");
            var doc = DocumentData.Resolve(project, library, company);
            if (!doc.RevisionNumber.HasValue)
                throw new ArgumentException(doc.RevisionIssue.Message);
            return BaseName(
                doc.CompanyName,
                type.Value,
                counterweight,
                project.Shaft == null ? 0 : project.Shaft.WidthMm,
                project.Shaft == null ? 0 : project.Shaft.DepthMm,
                project.Info == null ? null : project.Info.Name,
                doc.RevisionNumber.Value);
        }

        /// <summary>The DWG, PDF and project file names of one base name.</summary>
        public static DrawingFileNames Files(string baseName)
        {
            if (String.IsNullOrEmpty(baseName))
                throw new ArgumentException("The base name is empty.");
            return new DrawingFileNames
            {
                BaseName = baseName,
                Dwg = baseName + DwgExtension,
                Pdf = baseName + PdfExtension,
                Project = baseName + ProjectExtension
            };
        }
    }

    /// <summary>The file names that share one base name.</summary>
    public sealed class DrawingFileNames
    {
        public string BaseName;
        public string Dwg;
        public string Pdf;
        public string Project;
    }
}
