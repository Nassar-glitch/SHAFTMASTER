using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionBB;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Exception=System.Exception;
public sealed class SectionTests
{
    static List<object> results=new List<object>();static int sequence;static string report="section-results.json";
    [CommandMethod("SECTIONTESTREOPEN")]
    public void Reopened(){report="section-reopen-results.json";sequence=100;results.Clear();}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Log(string name,Action action){try{action();results.Add(new{status="PASS",test=name});}catch(Exception ex){results.Add(new{status="FAIL",test=name,error=ex.ToString()});}Configuration.WriteJson(Path.Combine(Configuration.Root,"tests",report),results);}
    [CommandMethod("SECTIONTESTPREPARE")]
    public void Prepare()
    {
        Log("Prepare and place coordinated semantic section",()=>{
            string master=Path.Combine(Configuration.Root,"Templates","MachineRoomSection_Master.dwg");
            using(var db=new Database(false,true)){db.ReadDwgFile(Path.Combine(Configuration.Root,"tests","section","reference-copy.dwg"),FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);using(var tr=db.TransactionManager.StartTransaction()){MachineRoomSectionDrawing.Prepare(db,tr);Check(MachineRoomSectionDrawing.Read(db,tr).Entities.Count==709,"Source bindings and generated dependencies");tr.Commit();}db.SaveAs(master,DwgVersion.AC1032);}
            var doc=Application.DocumentManager.MdiActiveDocument;ShaftFrame frame;
            using(var tr=doc.Database.TransactionManager.StartTransaction()){frame=DrawingReader.Read(doc.Database,tr).Frame;var input=ProjectDataStorage.ReadProject(doc.Database,tr);input.Capacity=479.9;input.SuspensionRopeDiameterMm=8;input.NumberOfSuspensionRopes=6;ProjectDataStorage.Write(doc.Database,tr,"Project",input);tr.Commit();}
            MachineRoomDrawing.Import(doc.Database,Path.Combine(Configuration.Root,"Templates","MachineRoom_Master.dwg"),frame.World(new Point3d(4500,0,0)),frame.XAxis);
            MachineRoomSectionDrawing.Import(doc.Database,master,frame.World(new Point3d(9000,0,0)),frame.XAxis);
        });
    }
    [CommandMethod("SECTIONTESTCHECK")]
    public void Verify()
    {
        int n=++sequence;Log("Three views saved and reopened "+n,()=>{
            Check(Commands.LastOutput!=null&&Commands.LastError==null,Commands.LastError??"Missing output");
            using(var db=new Database(false,true)){db.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);using(var tr=db.TransactionManager.StartTransaction())
            {
                var design=Commands.LastElevatorParameters;var plan=DrawingReader.Read(db,tr);var mr=MachineRoomDrawing.Read(db,tr);var section=MachineRoomSectionDrawing.Read(db,tr);
                DrawingUpdater.Validate(plan,design,Commands.LastDimensions);MachineRoomUpdater.Validate(mr,design);var audit=MachineRoomSectionGeometryAudit.Run(section,design,mr);
                Check(section.Entities.Count==709,"Unexpected duplicated/lost section entity");Check(PlanCoverage.Read(db,tr,plan).UnboundEntities==0,"Section entities reported unbound");
                var stored=ProjectDataStorage.Read<MachineRoomSectionParameters>(db,tr,"MachineRoomSectionDesign");Check(stored!=null&&stored.Beam.SectionDesignation==design.MachineRoomSection.Beam.SectionDesignation,"Section result persistence");
                var geometry=new List<object>();
                foreach(var b in section.Manifest.Entities)
                {
                    var e=section.Entities[b.Key];if(!e.Visible||b.Role=="frame")continue;
                    var pts=new List<LocalPoint>();var poly=e as Polyline;var curve=e as Curve;var hatch=e as Hatch;var dim=e as Dimension;var text=e as MText;
                    if(poly!=null){pts.AddRange(MachineRoomDependencies.Points(e,section.Frame));if(poly.Closed&&pts.Count>0)pts.Add(pts[0]);}
                    else if(curve!=null){for(int i=0;i<=64;i++){var p=section.Frame.Local(curve.GetPointAtParameter(i==64?curve.EndParam:curve.StartParam+(curve.EndParam-curve.StartParam)*i/64));pts.Add(new LocalPoint(p.X,p.Y,p.Z));}}
                    string content=null;LocalPoint? position=null;
                    if(dim!=null){var p=section.Frame.Local(dim.TextPosition);position=new LocalPoint(p.X,p.Y,0);content=(dim is LineAngularDimension2?(dim.Measurement*180/Math.PI).ToString("0.###")+" deg":dim.Measurement.ToString("0.###"));var linear=dim as RotatedDimension;if(linear!=null){foreach(var point in new[]{linear.XLine1Point,linear.DimLinePoint,linear.XLine2Point}){var q=section.Frame.Local(point);pts.Add(new LocalPoint(q.X,q.Y,0));}}}
                    if(text!=null){var q=section.Frame.Local(text.Location);position=new LocalPoint(q.X,q.Y,0);content=text.Contents;}
                    if(hatch!=null&&(b.Role=="concrete"||b.Role=="slab")){var map=new MachineRoomSectionGeometry(design);foreach(var q in new[]{new LocalPoint(b.Bounds[0],b.Bounds[1],0),new LocalPoint(b.Bounds[2],b.Bounds[1],0),new LocalPoint(b.Bounds[2],b.Bounds[3],0),new LocalPoint(b.Bounds[0],b.Bounds[3],0),new LocalPoint(b.Bounds[0],b.Bounds[1],0)})pts.Add(map.Map(q,b.Role));}
                    geometry.Add(new {b.Key,b.Role,Type=b.Type,Points=pts,Text=content,TextPosition=position,Hatch=hatch==null?null:hatch.PatternName});
                }
                Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","section","case-"+n+".json"),new {DWG=Commands.LastOutput,design.SW,design.SD,design.TZ,Design=design.MachineRoomSection,Audit=audit,Geometry=geometry});
            }}
        });
    }
    [CommandMethod("SECTIONTESTDATA")]
    public void Data()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;var ed=doc.Editor;
        double capacity=ed.GetDouble("\nTest capacity: ").Value,rope=ed.GetDouble("\nTest rope: ").Value;
        int count=ed.GetInteger("\nTest rope count: ").Value;double clearance=ed.GetDouble("\nTest clearance (-1 unknown): ").Value;
        using(var tr=doc.Database.TransactionManager.StartTransaction()){var input=ProjectDataStorage.ReadProject(doc.Database,tr);input.Capacity=capacity;input.SuspensionRopeDiameterMm=rope;input.NumberOfSuspensionRopes=count;input.MinimumDeflectorSlabClearanceMm=clearance<0?(double?)null:clearance;ProjectDataStorage.Write(doc.Database,tr,"Project",input);tr.Commit();}
    }
    [CommandMethod("SECTIONTESTFAULTS")]
    public void Faults()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        foreach(string role in new[]{"traction","deflector","rope","generatedRope","steel","concrete","slab","stand","machine","dimension","status","hanger"})
        {
            string selected=role;Log("Native fault detected: "+role,()=>{using(var tr=db.TransactionManager.StartTransaction()){
                var section=MachineRoomSectionDrawing.Read(db,tr);Check(Commands.LastElevatorParameters!=null,"No valid baseline for fault test");MachineRoomSectionGeometryAudit.Run(section,Commands.LastElevatorParameters,MachineRoomDrawing.Read(db,tr));var b=section.Manifest.Entities.First(v=>v.Role==selected&&section.Entities[v.Key].Visible);
                var e=section.Entities[b.Key];e.UpgradeOpen();e.TransformBy(Matrix3d.Displacement(section.Frame.XAxis*7));
                bool caught=false;try{MachineRoomSectionGeometryAudit.Run(section,Commands.LastElevatorParameters,MachineRoomDrawing.Read(db,tr));}catch{caught=true;}
                Check(caught,"Fault escaped audit: "+selected);
            }});
        }
        foreach(bool duplicate in new[]{false,true})Log(duplicate?"Duplicate section identifier rejected":"Missing section identifier rejected",()=>{using(var tr=db.TransactionManager.StartTransaction()){
            var s=MachineRoomSectionDrawing.Read(db,tr);var e=s.Entities["MachineRoomSection.TractionSheave.PitchCircle"];e.UpgradeOpen();
            if(duplicate){var model=(BlockTableRecord)tr.GetObject(e.OwnerId,OpenMode.ForWrite);var clone=(Entity)e.Clone();model.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);}else ComponentBinding.Tag(e,"Removed.Section.Pitch","traction");
            bool caught=false;try{MachineRoomSectionDrawing.Read(db,tr);}catch{caught=true;}Check(caught,"Semantic identifier fault escaped reader");
        }});
    }
    [CommandMethod("SECTIONTESTLIMIT")]
    public void Limit()
    {
        Log("Concrete above 1000 rejects without changing three-view geometry",()=>{
            var db=Application.DocumentManager.MdiActiveDocument.Database;var previous=Commands.LastElevatorParameters;var dimensions=Commands.LastDimensions;double? original;
            using(var tr=db.TransactionManager.StartTransaction()){var input=ProjectDataStorage.ReadProject(db,tr);original=input.MinimumDeflectorSlabClearanceMm;input.MinimumDeflectorSlabClearanceMm=1500;ProjectDataStorage.Write(db,tr,"Project",input);tr.Commit();}
            try{new UpdateAllCommand().Execute();Check(Commands.LastError!=null&&Commands.LastError.Contains("REDESIGN REQUIRED")&&Commands.LastOutput==null,"Out-of-range concrete accepted");
                using(var tr=db.TransactionManager.StartTransaction()){DrawingUpdater.Validate(DrawingReader.Read(db,tr),previous,dimensions);MachineRoomUpdater.Validate(MachineRoomDrawing.Read(db,tr),previous);MachineRoomSectionGeometryAudit.Run(MachineRoomSectionDrawing.Read(db,tr),previous,MachineRoomDrawing.Read(db,tr));}
            }finally{using(var tr=db.TransactionManager.StartTransaction()){var input=ProjectDataStorage.ReadProject(db,tr);input.MinimumDeflectorSlabClearanceMm=original;ProjectDataStorage.Write(db,tr,"Project",input);tr.Commit();}}
        });
    }
}
