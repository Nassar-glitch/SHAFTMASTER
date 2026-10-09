using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop
{
    public sealed class ProjectSession
    {
        public ElevatorProject Project;
        public string FilePath;
        public bool Dirty;
        public DrawingViews Views=DrawingViews.All;
        public bool Arrange=true;
        public ProjectSession(ElevatorProject project){Project=project;}
        public void Save(string path){path=Path.GetFullPath(path);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,ProjectJson.Serialize(Project));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}FilePath=path;Dirty=false;}
        public void Open(string path){var p=ProjectJson.Deserialize(File.ReadAllText(path));var errors=ProjectValidator.Validate(p,DrawingViews.Plan);if(errors.Any(e=>e.Message=="Required project group is missing."))throw new ArgumentException("Incomplete project structure.");Project=p;FilePath=path;Dirty=false;}
        public void MoveIntermediate(string id,int delta)
        {
            var floors=Project.Building.Floors;int index=floors.FindIndex(f=>f.FloorId==id),target=index+delta;
            if(index<=0||index>=floors.Count-1||target<=0||target>=floors.Count-1)throw new ArgumentException("Only intermediate floors can move; keep the lowest datum and final floor fixed.");
            var clone=ProjectJson.Clone(Project);var item=clone.Building.Floors[index];clone.Building.Floors.RemoveAt(index);clone.Building.Floors.Insert(target,item);
            ProjectFloors.Elevations(clone.Building); // retain interval with floor, validate before committing
            Project.Building=clone.Building;Dirty=true;
        }
    }
    public sealed class FloorRow:INotifyPropertyChanged
    {
        readonly ProjectSession session;public readonly FloorDefinition Floor;readonly Action changed;
        public FloorRow(ProjectSession s,FloorDefinition f,Action callback){session=s;Floor=f;changed=callback;}
        public string Name{get{return Floor.Name;}set{ProjectFloors.Rename(session.Project.Building,Floor.FloorId,value);Notify("Name");}}
        public string Height{get{return Floor.HeightToNextMm.HasValue?Floor.HeightToNextMm.Value.ToString("0.###",CultureInfo.InvariantCulture):"";}set{
            double parsed;double? height=null;if(!String.IsNullOrWhiteSpace(value)){if(!Double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)||!ProjectFloors.FinitePositive(parsed))throw new ArgumentException("Height must be a finite positive number in mm.");height=parsed;}
            if(session.Project.Building.Floors.Last()==Floor&&height.HasValue)throw new ArgumentException("The final floor has no interval above it.");
            Floor.HeightToNextMm=height;Notify("Height");}}
        public bool Served{get{return Floor.IsServed;}set{Floor.IsServed=value;Notify("Served");}}
        public string Elevation{get{try{return ProjectFloors.Elevations(session.Project.Building).Single(f=>f.FloorId==Floor.FloorId).ElevationMm.ToString("0.###");}catch(ArgumentException){return "";}}}
        void Notify(string name){session.Dirty=true;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));changed();}
        public void Refresh(){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Elevation"));}
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
