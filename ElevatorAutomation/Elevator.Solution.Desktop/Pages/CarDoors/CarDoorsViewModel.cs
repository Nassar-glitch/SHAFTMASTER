using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Doors;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class CarDoorsViewModel:PageViewModel
 {
  public CarDoorsViewModel():base("CAR & DOORS"){}
  // Served floors only.
  public static List<Choice> DisplayFloors(ElevatorProject project){return project.Building.Floors.Where(f=>f.IsServed).Select(f=>new Choice{Id=f.FloorId,Name=f.Name}).ToList();}
  public static List<Choice> Types(){return new List<Choice>{new Choice{Id=DoorTypes.Manual,Name="Manual"},new Choice{Id=DoorTypes.Automatic2S,Name="Automatic 2S · side opening"},new Choice{Id=DoorTypes.Automatic2C,Name="Automatic 2C · centre opening"}};}
  public static List<Choice> Sides(){return new List<Choice>{new Choice{Id=DoorTypes.Left,Name="Left"},new Choice{Id=DoorTypes.Right,Name="Right"}};}
  public static List<Choice> Handings(){return new List<Choice>{new Choice{Id=DoorTypes.Auto,Name="Auto"},new Choice{Id=DoorTypes.Left,Name="Left"},new Choice{Id=DoorTypes.Right,Name="Right"}};}
  // The clearances of the Advanced dimensions expander that a type uses, in order.
  public static string[] ClearanceKeys(string type)
  {
   if(type==DoorTypes.Automatic2S)return new[]{DoorSettings.CarZoneKey,DoorSettings.SillGapKey,DoorSettings.LandingZoneKey,DoorSettings.FixedReturnKey,DoorSettings.StackReturnKey,DoorSettings.MinFixedReturnKey,DoorSettings.MinStackReturnKey,DoorSettings.LandingJambKey,DoorSettings.LandingJambMinKey,DoorSettings.LandingPocketKey,DoorSettings.WallClearanceKey};
   return new[]{DoorSettings.CarZoneKey,DoorSettings.SillGapKey,DoorSettings.LandingZoneKey,DoorSettings.FixedReturnKey,DoorSettings.MinFixedReturnKey,DoorSettings.LandingPocketKey,DoorSettings.WallClearanceKey};
  }
  public static string Label(string type,string key)
  {
   if(type==DoorTypes.Automatic2C)
   {
    if(key==DoorSettings.FixedReturnKey)return "Car return, each side, preferred (mm)";
    if(key==DoorSettings.MinFixedReturnKey)return "Car return, each side, minimum (mm)";
    if(key==DoorSettings.LandingPocketKey)return "Landing pocket, each side (mm)";
   }
   return DoorFlow.Label(key);
  }
  public static string Format(double? v){return v.HasValue?Math.Round(v.Value,2).ToString("0.##",CultureInfo.InvariantCulture):"";}
  // The layout of the project's door on the current plan; null for a Manual door or when the inputs give no plan.
  public static DoorLayout Layout(ProjectSession session,LiveCalculation live,DesignContext context)
  {
   DoorLayout layout;Offset(session,live,context,out layout);return layout;
  }
  // The layout and the Doors Offset (range, typed value, out of range) of the project's automatic door; both null for Manual.
  public static DoorOffset Offset(ProjectSession session,LiveCalculation live,DesignContext context,out DoorLayout layout)
  {
   layout=null;
   if(!DoorTypes.IsAutomatic(DoorFlow.TypeOf(session.Project)))return null;
   var current=live.Design;
   if(current==null||current.Plan==null){try{current=new ElevatorDesignEngine().Calculate(session.Project,context,DrawingViews.Plan);}catch(ArgumentException){current=null;}}
   if(current==null||current.Plan==null)return null;
   var catalog=context==null?null:context.Doors;
   var door=DoorInput.From(session.Project,catalog);if(door==null)return null;
   layout=DoorRules.Select(door,current.Plan);
   return DoorOffset.From(door,layout,current.Plan);
  }
 }
}
