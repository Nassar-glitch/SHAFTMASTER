using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class RailsViewModel:PageViewModel
 {
  public RailsViewModel():base("RAILS"){}
  public static readonly FieldSpec[] Fields={new FieldSpec("Maximum bracket spacing (mm)","Rails.BracketMaximumSpacingMm")};
 }
}
