using System;
using System.IO;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
namespace Elevator.Reporting.Sheet
{
 // Company-level calculation-sheet settings shared by all projects (never stored in a project file).
 // Kept under %AppData%\ShaftMaster; the logo is a copy of the user's PNG/JPEG. Every value is optional.
 public sealed class CompanyReportSettings
 {
  public string CompanyName;
  public string LogoFile; // file name inside SettingsDirectory
  // Overridable so tests never touch the real user profile.
  public static string SettingsDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ShaftMaster");
  static string SettingsPath{get{return Path.Combine(SettingsDirectory,"report-settings.json");}}
  public static CompanyReportSettings Load()
  {
   try
   {
    if(!File.Exists(SettingsPath))return new CompanyReportSettings();
    var s=Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<CompanyReportSettings>(File.ReadAllText(SettingsPath))??new CompanyReportSettings();
    if(s.LogoFile!=null&&(s.LogoFile!=Path.GetFileName(s.LogoFile)||!File.Exists(Path.Combine(SettingsDirectory,s.LogoFile))))s.LogoFile=null;
    return s;
   }
   catch(Exception){return new CompanyReportSettings();} // unreadable settings never block the application
  }
  public void Save()
  {
   Directory.CreateDirectory(SettingsDirectory);
   string temporary=SettingsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(temporary,Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Serialize(this));if(File.Exists(SettingsPath))File.Replace(temporary,SettingsPath,null);else File.Move(temporary,SettingsPath);}
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
  public string LogoPath{get{return LogoFile==null?null:Path.Combine(SettingsDirectory,LogoFile);}}
  // The stored logo, or null when none is set or the file cannot be read as PNG/JPEG.
  public DocxImage Logo()
  {
   try{return LogoFile!=null&&File.Exists(LogoPath)?DocxImage.FromFile(LogoPath):null;}catch(Exception){return null;}
  }
  // Validates the picked file as PNG/JPEG, then copies it into the settings folder.
  public void SetLogo(string source)
  {
   var image=DocxImage.FromFile(source);
   Directory.CreateDirectory(SettingsDirectory);
   string name="company-logo-"+Guid.NewGuid().ToString("N").Substring(0,8)+"."+image.Extension;
   File.WriteAllBytes(Path.Combine(SettingsDirectory,name),image.Data);
   string old=LogoPath;LogoFile=name;
   if(old!=null&&File.Exists(old))File.Delete(old);
  }
  public void RemoveLogo(){string old=LogoPath;LogoFile=null;if(old!=null&&File.Exists(old))File.Delete(old);}
 }
}
