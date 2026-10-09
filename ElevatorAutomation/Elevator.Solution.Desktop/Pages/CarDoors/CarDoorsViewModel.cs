using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class CarDoorsViewModel:PageViewModel
 {
  public CarDoorsViewModel():base("CAR & DOORS"){}
  // Served floors only.
  public static List<Choice> DisplayFloors(ElevatorProject project){return project.Building.Floors.Where(f=>f.IsServed).Select(f=>new Choice{Id=f.FloorId,Name=f.Name}).ToList();}
 }
}
