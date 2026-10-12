using System;
using System.IO;
using Elevator.Core.Company;
using Elevator.Core.Serialization;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Sheet
{
    /// <summary>
    /// Company-level settings shared by all projects (never stored in a project file). Kept under %AppData%\ShaftMaster; the logo is a copy
    /// of the user's PNG/JPEG. Every value is optional. This is the storage adapter of the portable <see cref="CompanySettings"/> model:
    /// the documents resolve their company data from it with <see cref="DocumentData.Resolve"/> (a blank name follows the preset library).
    /// The file format is unchanged.
    /// </summary>
    public sealed class CompanyReportSettings
    {
        public string CompanyName;
        public string LogoFile; // file name inside SettingsDirectory

        // Overridable so tests never touch the real user profile.
        public static string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShaftMaster");

        static string SettingsPath
        {
            get
            {
                return Path.Combine(SettingsDirectory, "report-settings.json");
            }
        }

        /// <summary>Reads the company settings; defaults when they are missing or unreadable.</summary>
        public static CompanyReportSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new CompanyReportSettings();
                var s = Elevator.Core
                    .Serialization
                    .CoreJson
                    .CreateWithDefaultLimit()
                    .Deserialize<CompanyReportSettings>(File.ReadAllText(SettingsPath)) ?? new CompanyReportSettings();
                if (s.LogoFile != null
                    && (s.LogoFile != Path.GetFileName(s.LogoFile) || !File.Exists(Path.Combine(SettingsDirectory, s.LogoFile))))
                    s.LogoFile = null;
                return s;
            }
            catch (Exception) { return new CompanyReportSettings(); } // unreadable settings never block the application
        }

        /// <summary>Writes the company settings.</summary>
        public void Save()
        {
            Directory.CreateDirectory(SettingsDirectory);
            string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, CoreJson.CreateWithDefaultLimit().Serialize(this));
                if (File.Exists(SettingsPath))
                    File.Replace(temporary, SettingsPath, null);
                else
                    File.Move(temporary, SettingsPath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        /// <summary>The portable company model of these settings.</summary>
        public CompanySettings ToCompanySettings()
        {
            return new CompanySettings { CompanyName = CompanyName, LogoFile = LogoFile };
        }

        /// <summary>Takes the values of the portable company model (Save writes them).</summary>
        public void Apply(CompanySettings company)
        {
            CompanyName = company == null ? null : company.CompanyName;
            LogoFile = company == null ? null : company.LogoFile;
        }

        public string LogoPath
        {
            get
            {
                return LogoFile == null ? null : Path.Combine(SettingsDirectory, LogoFile);
            }
        }

        /// <summary>The stored logo, or null when none is set or the file cannot be read as PNG/JPEG.</summary>
        public DocxImage Logo()
        {
            try
            {
                return LogoFile != null && File.Exists(LogoPath) ? DocxImage.FromFile(LogoPath) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Validates the picked file as PNG/JPEG, then copies it into the settings folder.</summary>
        public void SetLogo(string source)
        {
            var image = DocxImage.FromFile(source);
            Directory.CreateDirectory(SettingsDirectory);
            string name = "company-logo-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "." + image.Extension;
            File.WriteAllBytes(Path.Combine(SettingsDirectory, name), image.Data);
            string old = LogoPath;
            LogoFile = name;
            if (old != null && File.Exists(old))
                File.Delete(old);
        }

        /// <summary>Deletes the copied logo.</summary>
        public void RemoveLogo()
        {
            string old = LogoPath;
            LogoFile = null;
            if (old != null && File.Exists(old))
                File.Delete(old);
        }
    }
}
