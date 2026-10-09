using System;
using System.Globalization;
using Elevator.Core.Application;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop.Common
{
 // "Calculated geometry" of the Shaft and Car & Doors pages: car, DBG, TZ and door width of the current inputs.
 static class DerivedGeometry
 {
  // Null when the inputs give no plan (the page then shows nothing under the heading).
  public static string Text(ProjectSession session,LiveCalculation live,DesignContext context)
  {
   var current=live.Design;
   if(current==null||current.Plan==null){try{current=new ElevatorDesignEngine().Calculate(session.Project,context,session.Views);}catch(ArgumentException){current=null;}}
   if(current==null||current.Plan==null)return null;
   var p=current.Plan;return String.Format(CultureInfo.InvariantCulture,"Car: {0:0.###} × {1:0.###} mm    ·    Car DBG: {2:0.###} mm\nTZ: {3:0.###} mm    ·    CW DBG: {4:0.###} mm    ·    Door width: {5:0.###} mm",p.CarWidth,p.CarDepth,p.CarDbg,p.TZ,p.CwDbg,p.DoorWidth);
  }
 }
}
