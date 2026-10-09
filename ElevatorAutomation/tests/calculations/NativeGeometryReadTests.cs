using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.AutoCAD;
public static class NativeGeometryReadTests
{
    [CommandMethod("CALCPHASE1READONLY")]
    public static void ReadOnly()
    {
        string output=Path.GetDirectoryName(typeof(NativeGeometryReadTests).Assembly.Location);
        output=Directory.GetParent(output).FullName;
        var serializer=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};
        try
        {
            var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var mr=MachineRoomDrawing.Read(db,tr);var plan=DrawingReader.Read(db,tr);
                var section=MachineRoomSectionDrawing.Read(db,tr);
                var project=UnifiedProjectStorage.Read(db,tr);
                if(project==null)throw new InvalidOperationException("Stored canonical project unavailable.");
                var evidence=ChassisCalculationGeometryReader.Read(mr);
                var root=Directory.GetParent(Directory.GetParent(output).FullName).FullName;
                var context=new DesignContext{Equipment=serializer.Deserialize<EquipmentProfile>(File.ReadAllText(Path.Combine(root,"config","equipment-profile.json"))),CounterweightDepthMm=plan.ElevatorParameters.CwDepth,RoomExtensions=mr.Extensions,SectionReference=section.Manifest.Reference,FrontSupportDepthMm=(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P2")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P4"))/2,RearSupportOffsetMm=(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P1")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P3"))/2,RetainedDeflectorPitchMm=mr.PitchProjection,RetainedDeflectorVisible=mr.Entities[MachineRoomDrawing.Pitch].Visible,ChassisEvidence=evidence};
                var design=new ElevatorDesignEngine().Calculate(project,context,DrawingViews.All);
                if(!design.CanGenerate||design.ChassisGeometry.Issues.Count>0)throw new InvalidOperationException(String.Join(";",design.ChassisGeometry.Issues.Select(v=>v.Message)));
                if(design.ChassisGeometry.Bearings.Count!=4||design.ChassisGeometry.LongitudinalMembers.Count!=3)throw new InvalidOperationException("Native chassis topology differs.");
                if(design.ChassisGeometry.Provenance==null||!ChassisGeometryProvenance.Validate(project,design.ChassisGeometry,context).IsValid)throw new InvalidOperationException("Native generation source provenance invalid.");
                if(design.ChassisCalculation.Reactions!=null)throw new InvalidOperationException("Older project must not fabricate missing calculation inputs.");
                File.WriteAllText(Path.Combine(output,"native-readonly-result.json"),serializer.Serialize(new {Status="PASS",SW=project.Shaft.WidthMm,SD=project.Shaft.DepthMm,Evidence=evidence,Geometry=design.ChassisGeometry,Calculation=design.ChassisCalculation,TransactionCommitted=false}));
                // No commit, save, update commands or source-template edits.
            }
            doc.Editor.WriteMessage("\nPASS: native chassis geometry read; missing calculation inputs remain unavailable.\n");
        }
        catch(System.Exception e){File.WriteAllText(Path.Combine(output,"native-readonly-result.json"),serializer.Serialize(new {Status="FAIL",Error=e.ToString()}));}
    }
}
