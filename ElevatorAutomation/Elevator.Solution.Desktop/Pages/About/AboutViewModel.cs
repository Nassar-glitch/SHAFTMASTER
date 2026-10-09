using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class AboutViewModel:PageViewModel
 {
  public AboutViewModel():base("ABOUT"){}
  public const string Product="ShaftMaster — Elevator Design Solutions";
  public string VersionText{get{return "Version "+typeof(Program).Assembly.GetName().Version.ToString(3);}}
  public const string Requirements="Windows x64 and .NET Framework 4.8.\nDWG generation: AutoCAD 2023 and the existing reviewed drawing templates.\nSTEP export: installed, licensed SOLIDWORKS with a configured English-named default part template; verified with SOLIDWORKS 2022. Keep the packaged application files together.";
  public const string Workflow="Geared Traction · 1:1 suspension · Elevator Plan, Machine Room Plan, Section A-A and Section B-B. ";
 }
}
