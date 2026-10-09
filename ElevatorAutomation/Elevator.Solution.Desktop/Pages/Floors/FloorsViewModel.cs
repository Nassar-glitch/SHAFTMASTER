using System;
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Input;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class FloorsViewModel:PageViewModel
 {
  ProjectSession session;FloorRow selectedRow;string newFloorName="",newFloorInterval="";
  public FloorsViewModel():base("FLOORS")
  {
   InsertBeforeCommand=new RelayCommand(InsertBefore);
   RemoveCommand=new RelayCommand(Remove);
   MoveDownCommand=new RelayCommand(()=>Move(-1));
   MoveUpCommand=new RelayCommand(()=>Move(1));
  }
  // The rows of the floor grid (one list, refilled for each build of the page).
  public readonly List<FloorRow> Rows=new List<FloorRow>();
  public FloorRow SelectedRow{get{return selectedRow;}set{SetProperty(ref selectedRow,value);}}
  public string NewFloorName{get{return newFloorName;}set{SetProperty(ref newFloorName,value);}}
  // The interval of a new floor in mm (starts at the preset storey height).
  public string NewFloorInterval{get{return newFloorInterval;}set{SetProperty(ref newFloorInterval,value);}}
  // Floor actions on the selected row; each throws ArgumentException with the message for the user.
  public ICommand InsertBeforeCommand{get;private set;}
  public ICommand RemoveCommand{get;private set;}
  public ICommand MoveDownCommand{get;private set;}
  public ICommand MoveUpCommand{get;private set;}
  public void Load(ProjectSession session,PresetFlow presets,Action changed)
  {
   this.session=session;Rows.Clear();foreach(var f in session.Project.Building.Floors)Rows.Add(new FloorRow(session,f,changed));
   SelectedRow=null;NewFloorName="";NewFloorInterval=DisplayText.ManagedText(ProjectPresets.NewFloorHeight(presets.Library));
  }
  static FloorDefinition Selected(FloorRow row){if(row==null)throw new ArgumentException("Select an intermediate floor first.");return row.Floor;}
  void InsertBefore()
  {
   if(SelectedRow==null)throw new ArgumentException("Select a floor above the lowest datum.");double h=Double.Parse(NewFloorInterval,CultureInfo.InvariantCulture);ProjectFloors.InsertBefore(session.Project.Building,SelectedRow.Floor.FloorId,new FloorDefinition{Name=NewFloorName},h);session.Dirty=true;
  }
  void Remove(){var f=Selected(SelectedRow);ProjectFloors.Remove(session.Project.Building,f.FloorId);session.Dirty=true;}
  void Move(int delta){session.MoveIntermediate(Selected(SelectedRow).FloorId,delta);}
 }
}
