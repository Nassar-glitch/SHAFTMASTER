using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class ShaftViewModel:PageViewModel
 {
  public ShaftViewModel():base("SHAFT"){}
  public static readonly FieldSpec[] Fields={new FieldSpec("Shaft width · SW (mm)","Shaft.WidthMm"),new FieldSpec("Shaft depth · SD (mm)","Shaft.DepthMm"),new FieldSpec("Pit depth (mm)","Shaft.PitDepthMm"),new FieldSpec("Overhead (mm)","Shaft.OverheadMm")};
 }
}
