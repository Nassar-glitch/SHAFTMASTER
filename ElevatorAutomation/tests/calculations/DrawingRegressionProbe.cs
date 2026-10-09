using System;
using System.IO;
using System.Web.Script.Serialization;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
public static class DrawingRegressionProbe
{
    public static void Main(string[] args)
    {
        try
        {
            var json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};var source=json.Deserialize<ElevatorParameters>(File.ReadAllText(args[0]));
            var project=ProjectJson.Deserialize(File.ReadAllText(args[1]));var equipment=json.Deserialize<EquipmentProfile>(File.ReadAllText(args[2]));
            var snapshots=new System.Collections.Generic.List<object>();
            foreach(var size in new[]{new[]{1450.0,1600},new[]{1650.0,1800},new[]{1850.0,2000}})
            {
                project.Shaft.WidthMm=size[0];project.Shaft.DepthMm=size[1];
                var context=new DesignContext{Equipment=equipment,CounterweightDepthMm=source.CwDepth,RoomExtensions=source.MachineRoom.Extensions,SectionReference=source.MachineRoomSection.Reference,FrontSupportDepthMm=-source.MachineRoomSection.FrontSupportX,RearSupportOffsetMm=-source.MachineRoomSection.RearSupportX-source.SD,RetainedDeflectorPitchMm=250,RetainedDeflectorVisible=true};
                // Same probe compiles against release Core and new Core. New evidence is set
                // only where supported; both snapshots exclude optional calculation results.
                var field=typeof(DesignContext).GetField("ChassisEvidence");
                if(field!=null&&args.Length>4)field.SetValue(context,json.Deserialize(File.ReadAllText(args[4]),field.FieldType));
                var result=new ElevatorDesignEngine().Calculate(project,context,DrawingViews.All);
                if(!result.CanGenerate)throw new Exception("Existing four-view case blocked.");
                snapshots.Add(new {SW=size[0],SD=size[1],Plan=result.Plan,SectionAA=result.SectionAA,Floors=result.Floors,Validation=result.Validation,Layout=result.LayoutStatus,Wrap=result.WrapAngleStatus,Ratio=result.CodeDiameterRatioStatus});
            }
            File.WriteAllText(args[3],json.Serialize(snapshots));Console.WriteLine("PASS: three four-view geometry snapshots.");
        }
        catch(Exception e){Console.Error.WriteLine(e.ToString());Environment.ExitCode=1;}
    }
}
