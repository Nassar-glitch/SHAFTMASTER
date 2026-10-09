using Elevator.Core;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD;
using Exception = System.Exception;

public sealed class CadIntegrationTests
{
    private static List<object> results=new List<object>();
    private static Dictionary<string,double> lastShape;
    private static int success;
    private static string LogPath {get{return Path.Combine(Configuration.Root,"tests","cad-test-results.json");}}
    private static string Source {get{return Path.Combine(Directory.GetParent(Configuration.Root).FullName,"legacy","ElevatorPlanGenerator","templates","plan codex.dwg");}}
    private static Dictionary<string,double> Shape(DrawingSnapshot s) {
        var v=new Dictionary<string,double>();foreach(var pair in s.Entities) {
            Curve c=pair.Value as Curve;if(c!=null){var a=c.StartPoint;var b=c.EndPoint;v[pair.Key+"ax"]=a.X;v[pair.Key+"ay"]=a.Y;v[pair.Key+"bx"]=b.X;v[pair.Key+"by"]=b.Y;}
            Dimension d=pair.Value as Dimension;if(d!=null)v[pair.Key+"dim"]=d.Measurement;
        }return v;
    }
    private static void Log(object value) {results.Add(value);Configuration.WriteJson(LogPath,results);}
    private static void Check(bool condition,string label) {if(!condition)throw new Exception(label);}
    private static void PointNear(Point3d actual,Point3d expected,string label) {if(actual.DistanceTo(expected)>0.02)throw new Exception(label+": point difference "+actual.DistanceTo(expected));}
    [CommandMethod("ELEVATORCHECK")]
    public void CheckCurrent() {
        try {
            if(Commands.LastError!=null||Commands.LastOutput==null)throw new Exception("Update did not succeed: "+Commands.LastError);
            var doc=Application.DocumentManager.MdiActiveDocument;
            using(var tr=doc.Database.TransactionManager.StartTransaction()) {
                var s=DrawingReader.Read(doc.Database,tr);DrawingUpdater.Validate(s,Commands.LastElevatorParameters,Commands.LastDimensions);
                Check(s.ModelEntityCount==521,"Model entity count changed");
                // Independent checks of all primary displayed dimensions.
                foreach(var p in new Dictionary<string,double>{{"Plan.Dimension.Part016",s.ElevatorParameters.SW},{"Plan.Dimension.Part026",s.ElevatorParameters.SD},{"Plan.Dimension.Part025",s.ElevatorParameters.SW-400},{"Plan.Dimension.Part020",s.ElevatorParameters.SD-400},{"Plan.Dimension.Part024",s.ElevatorParameters.SW-300},{"Plan.Dimension.Part003",Commands.LastElevatorParameters.CwDbg},{"Plan.Dimension.Part008",Commands.LastElevatorParameters.DoorWidth},{"Plan.Dimension.Part010",Commands.LastElevatorParameters.DoorWidth},{"Plan.Dimension.Part019",(s.ElevatorParameters.SD-400)/2+150}})
                    DrawingUpdater.AssertNear(s.Get<RotatedDimension>(p.Key).Measurement,p.Value,"Independent primary dimension "+p.Key);
                using(Database baseline=new Database(false,true)) {
                    baseline.ReadDwgFile(Source,FileOpenMode.OpenForReadAndAllShare,true,null);baseline.CloseInput(true);
                    using(var bt=baseline.TransactionManager.StartTransaction()) {
                        LegacyMigration.Ensure(baseline,bt);var original=DrawingReader.Read(baseline,bt);
                        // Build an independent source-to-result reference, also detecting cumulative drift.
                        var mapping=new PlanGeometryAdapter(original,Commands.LastElevatorParameters);
                        foreach(var pair in original.Entities) {
                            Binding bind=original.Bindings[pair.Key];Entity current=s.Entities[pair.Key];Entity before=pair.Value;
                            Check(current.Layer==before.Layer&&current.Linetype==before.Linetype&&current.LineWeight==before.LineWeight,"Style changed: "+pair.Key);
                            Check(current.Color.ColorIndex==before.Color.ColorIndex,"Color changed: "+pair.Key);
                            if(current is MText)Check(((MText)current).Contents==((MText)before).Contents,"Text changed: "+pair.Key);
                            if(bind.Role=="dimension") {Check(((Dimension)current).DimensionText==((Dimension)before).DimensionText,"Dimension format changed: "+pair.Key);continue;}
                            using(Entity expected=(Entity)before.Clone()) {
                                DrawingUpdater.ApplyShape(expected,bind.Role,mapping);
                                Polyline a=current as Polyline,b=expected as Polyline;
                                if(a!=null) {
                                    Check(a.NumberOfVertices==b.NumberOfVertices&&a.Closed==b.Closed,"Polyline topology changed: "+pair.Key);
                                    for(int i=0;i<a.NumberOfVertices;i++) {PointNear(s.Frame.Local(a.GetPoint3dAt(i)),original.Frame.Local(b.GetPoint3dAt(i)),"Polyline "+pair.Key);DrawingUpdater.AssertNear(a.GetBulgeAt(i),b.GetBulgeAt(i),"Bulge "+pair.Key);}
                                }
                                else if(current is Curve) {
                                    Curve ca=(Curve)current,cb=(Curve)expected;
                                    for(int i=0;i<=4;i++) PointNear(s.Frame.Local(ca.GetPointAtParameter(ca.StartParam+(ca.EndParam-ca.StartParam)*i/4)),original.Frame.Local(cb.GetPointAtParameter(cb.StartParam+(cb.EndParam-cb.StartParam)*i/4)),"Curve "+pair.Key);
                                }
                                if(current is Arc)DrawingUpdater.AssertNear(((Arc)current).Radius,((Arc)before).Radius,"Rigid radius "+pair.Key);
                                if(current is Hatch)DrawingUpdater.AssertNear(((Hatch)current).Area,((Hatch)before).Area,"Hatch area "+pair.Key);
                            }
                        }
                    }
                }
                lastShape=Shape(s);success++;
                Log(new {status="PASS",kind="current-drawing",test=success,layout=Commands.LastElevatorParameters,output=Commands.LastOutput});
            }
            // Reopen saved DWG independently and validate every dimension after file round trip.
            using(var saved=new Database(false,true)) {
                saved.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);saved.CloseInput(true);
                using(var t=saved.TransactionManager.StartTransaction()) {var ss=DrawingReader.Read(saved,t);DrawingUpdater.Validate(ss,Commands.LastElevatorParameters,Commands.LastDimensions);Check(ss.ModelEntityCount==521,"Saved entity count changed");}
            }
            Log(new {status="PASS",kind="saved-reopen",test=success});
            doc.Editor.WriteMessage("\nPASS: geometry, styles, rigid details, dimensions and saved DWG reopen.\n");
        } catch(Exception ex){Log(new {status="FAIL",error=ex.Message});Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nTEST FAILED: "+ex.Message+"\n");}
    }
    [CommandMethod("ELEVATORPOSETEST")]
    public void Pose() {
        var doc=Application.DocumentManager.MdiActiveDocument;
        using(var tr=doc.Database.TransactionManager.StartTransaction()) {
            var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            Matrix3d m=Matrix3d.Displacement(new Vector3d(23456,-78210,0))*Matrix3d.Rotation(0.37,Vector3d.ZAxis,Point3d.Origin);
            foreach(ObjectId id in model) ((Entity)tr.GetObject(id,OpenMode.ForWrite)).TransformBy(m);
            tr.Commit();
        }
        doc.Editor.Regen();
        Log(new {status="PASS",kind="test-only-pose-applied",angleRadians=0.37,translation=new[]{23456,-78210,0}});
    }
    [CommandMethod("ELEVATORCHECKREJECTED")]
    public void Rejected() {
        try {
            Check(Commands.LastError!=null&&Commands.LastOutput==null,"Invalid input was not rejected");
            var doc=Application.DocumentManager.MdiActiveDocument;
            using(var t=doc.Database.TransactionManager.StartTransaction()) {
                var current=Shape(DrawingReader.Read(doc.Database,t));
                foreach(var p in lastShape)if(Math.Abs(p.Value-current[p.Key])>1e-7)throw new Exception("Rejected update modified geometry: "+p.Key);
            }
            Log(new {status="PASS",kind="rejected-without-geometry-change",reason=Commands.LastError});
        } catch(Exception ex){Log(new {status="FAIL",error=ex.Message});}
    }
}
