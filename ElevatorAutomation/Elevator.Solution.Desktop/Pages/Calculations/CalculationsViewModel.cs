using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class CalculationsViewModel:PageViewModel
 {
  public CalculationsViewModel():base("CALCULATIONS"){}
  // Basic and Advanced; an unknown saved mode is listed so that it stays visible.
  public static List<Choice> ModeChoices(string mode)
  {
   var choices=new List<Choice>{new Choice{Id="Basic",Name="Simplified load distribution"},new Choice{Id="Advanced",Name="Advanced · detailed rope / member actions"}};
   if(!choices.Any(v=>v.Id==mode))choices.Add(new Choice{Id=mode,Name="Unsupported saved mode: "+mode});
   return choices;
  }
  // Basic mode: plain names of the inputs in result messages.
  public static string BasicFieldLabel(string field)
  {
   switch(field){
    case "Calculations.CarMassKg":return "Empty car mass";
    case "Calculations.CounterweightMassKg":return "Counterweight mass";
    case "Calculations.MachineMassKg":return "Machine mass";
    case "Calculations.CarSideAdditionalSuspendedMassKg":return "Effective car-side additions";
    case "Calculations.CounterweightSideAdditionalSuspendedMassKg":return "Effective CW-side additions";
    case "Calculations.ChassisMassKg":return "Chassis assembly mass";
    case "Calculations.HangerAssemblyMassKg":return "Hanger assembly mass";
    case "Elevator.CapacityKg":return "Rated capacity";
    case "Suspension.RopingRatio":return "Suspension ratio";
    case "Machine.TractionSheavePitchDiameterMm":return "Traction-sheave pitch diameter";
    case "Basic.Geometry":case "Geometry":return "Drawing geometry";
    case "Bearings":return "Bearing reactions";
    default:return "Project input";
   }
  }
  // Basic P1–P4 lever arms (mm, 0–1 decimals): header row, then front → rear, front row, rear row.
  public static string[][] DistanceRows(SimplifiedDistances d)
  {
   System.Func<double,string> mm=v=>v.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
   return new[]{new[]{"Distances (mm)","Start → load","Load → end","Span"},
    new[]{"Front row → rear row",mm(d.LoadFromFrontMm),mm(d.LoadToRearMm),mm(d.LongitudinalSpanMm)},
    new[]{"Front row P4 → P2",mm(d.FrontLoadFromLeftMm),mm(d.FrontLoadToRightMm),mm(d.FrontTransverseSpanMm)},
    new[]{"Rear row P3 → P1",mm(d.RearLoadFromLeftMm),mm(d.RearLoadToRightMm),mm(d.RearTransverseSpanMm)}};
  }
 }
}
