using System;
using System.Globalization;
using System.Collections.Generic;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class FloorsViewModel:PageViewModel
 {
  public FloorsViewModel():base("FLOORS"){}
  // The rows of the floor grid (one list, refilled for each build of the page).
  public readonly List<FloorRow> Rows=new List<FloorRow>();
  public void Load(ProjectSession session,Action changed){Rows.Clear();foreach(var f in session.Project.Building.Floors)Rows.Add(new FloorRow(session,f,changed));}
  // The interval a new floor gets (the preset storey height).
  public static string NewFloorInterval(PresetFlow presets){return DisplayText.ManagedText(ProjectPresets.NewFloorHeight(presets.Library));}
  public static FloorDefinition Selected(FloorRow row){if(row==null)throw new ArgumentException("Select an intermediate floor first.");return row.Floor;}
  public void InsertBefore(ProjectSession session,FloorRow selected,string name,string interval)
  {
   if(selected==null)throw new ArgumentException("Select a floor above the lowest datum.");double h=Double.Parse(interval,CultureInfo.InvariantCulture);ProjectFloors.InsertBefore(session.Project.Building,selected.Floor.FloorId,new FloorDefinition{Name=name},h);session.Dirty=true;
  }
  public void Remove(ProjectSession session,FloorRow row){var f=Selected(row);ProjectFloors.Remove(session.Project.Building,f.FloorId);session.Dirty=true;}
  public void Move(ProjectSession session,FloorRow row,int delta){session.MoveIntermediate(Selected(row).FloorId,delta);}
 }
}
