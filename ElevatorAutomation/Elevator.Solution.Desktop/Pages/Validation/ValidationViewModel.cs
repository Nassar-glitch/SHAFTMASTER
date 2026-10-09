using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class ValidationViewModel:PageViewModel
 {
  public ValidationViewModel():base("VALIDATION"){}
  // The three engineering statuses (Name, status word in Id).
  public static Choice[] Statuses(ElevatorDesignResult design)
  {return new[]{new Choice{Name="Layout",Id=DisplayText.StatusText(design.LayoutStatus)},new Choice{Name="Wrap angle",Id=DisplayText.StatusText(design.WrapAngleStatus)},new Choice{Name="D/d",Id=DisplayText.StatusText(design.CodeDiameterRatioStatus)}};}
  public static List<ValidationRow> Rows(ElevatorDesignResult design)
  {return design.Validation.Select(v=>new ValidationRow{Status=DisplayText.StatusText(v.Status),Field=v.Field,Message=DisplayText.MessageText(v.Message),Blocks=v.BlocksGeneration?"Yes":"No"}).ToList();}
  public static readonly string[] Columns={"Status","Field","Message","Blocks"};
 }
 // A row of the validation grid.
 sealed class ValidationRow{public string Status{get;set;}public string Field{get;set;}public string Message{get;set;}public string Blocks{get;set;}}
}
