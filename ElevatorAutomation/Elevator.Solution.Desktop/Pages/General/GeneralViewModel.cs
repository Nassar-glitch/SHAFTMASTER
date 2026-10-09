using Elevator.Core.Application;
using Elevator.Reporting.Sheet;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class GeneralViewModel:PageViewModel
 {
  readonly PresetFlow presets;
  string personsText="";
  public GeneralViewModel(PresetFlow presets):base("GENERAL"){this.presets=presets;}
  // "8 persons" next to the capacity (empty without a preset library or a capacity).
  public string PersonsText{get{return personsText;}private set{SetProperty(ref personsText,value);}}
  public void UpdatePersons(double? capacityKg){var n=presets.PersonsFor(capacityKg);PersonsText=n.HasValue?n.Value+(n.Value==1?" person":" persons"):"";}
  // Calculation-sheet title block fields of the project.
  public static readonly FieldSpec[] ReportFields=
  {
   new FieldSpec("Client","Report.ClientName"),new FieldSpec("Document number","Report.DocumentNumber"),new FieldSpec("Revision","Report.Revision"),
   new FieldSpec("Prepared by","Report.PreparedBy"),new FieldSpec("Checked by","Report.CheckedBy"),new FieldSpec("Approved by","Report.ApprovedBy"),
  };
  // Company name and logo are shared by all projects (Company / report settings).
  public static string CompanySummary(CompanyReportSettings company){return "Company (all projects): "+(company.CompanyName??"no name")+" · "+(company.LogoFile!=null?"logo set":"no logo");}
 }
}
