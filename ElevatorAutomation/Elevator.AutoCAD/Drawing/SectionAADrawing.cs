using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using Autodesk.AutoCAD.DatabaseServices;using Autodesk.AutoCAD.Geometry;using Elevator.Core.Serialization;
using Elevator.Core.Geometry;
namespace Elevator.AutoCAD {
 public sealed class AASeed {public string SourceSha256;public List<AAPart> Entities;}
 public sealed class AAPart {public string Handle,Role,Key;public double[] Bounds;}
 public sealed class AAManifest {public int SchemaVersion=1;public List<string> Keys;public string InputJson;public double CarAttachmentZ=2300,CWAttachmentZ=3000;}
 public sealed class AAAudit {public string Status="PASS",LayoutStatus,CWCalibrationStatus,RailEndAdequacy="NOT_YET_EVALUABLE",BracketHardwareStatus="NOT_IMPLEMENTED - STATION DATA ONLY",CodeDiameterRatioStatus,WrapAngleStatus,SlabClearanceStatus,HangerStatus="MECHANICAL REVIEW REQUIRED";public int EntityCount,FloorCount,DoorCount,Dimensions,BracketStations;public double Travel,Pit,OH,CarEnvelope,CWEnvelope,MaxBracketSpacing;}
 public static class SectionAADrawing {
  public const string Prefix="SectionAA.",Library="SECTION_AA_NATIVE_PARTS",DataKey="SectionAAProject",ManifestKey="SectionAAManifest";
  public static string Output {get{return Path.Combine(Configuration.Root,"output","section-aa");}}
  public static string Master {get{return Path.Combine(Configuration.Root,"Templates","SectionAA_Master.dwg");}}
  public static string Json(object o){return Elevator.Core.Serialization.CoreJson.Create().Serialize(o);}
  static ObjectId Model(Database db,Transaction tr){return ((BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead))[BlockTableRecord.ModelSpace];}
  public static string[] Tag(Entity e){using(var x=e.GetXDataForApplication(DrawingReader.RegApp)){return x==null?null:x.AsArray().Skip(1).Select(v=>Convert.ToString(v.Value)).ToArray();}}
  public static Dictionary<string,Entity> Find(Database db,Transaction tr){var r=new Dictionary<string,Entity>();foreach(ObjectId id in (BlockTableRecord)tr.GetObject(Model(db,tr),OpenMode.ForRead)){var e=(Entity)tr.GetObject(id,OpenMode.ForRead);var t=Tag(e);if(t==null||!t[0].StartsWith(Prefix))continue;if(r.ContainsKey(t[0]))throw new InvalidOperationException("Duplicate A-A identity "+t[0]);r.Add(t[0],e);}return r;}
  public static ShaftFrame Frame(Dictionary<string,Entity> e){var l=(Line)e[Prefix+"Frame.Axis"];var x=(l.EndPoint-l.StartPoint).GetNormal();return new ShaftFrame{Origin=l.StartPoint,XAxis=x,YAxis=Vector3d.ZAxis.CrossProduct(x),ZAxis=Vector3d.ZAxis};}
  static void Add(Database db,Transaction tr,BlockTableRecord m,Entity e,string key,string role){m.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);ComponentBinding.Tag(e,key,role);}
  static void Map(Entity e,Func<Point3d,Point3d> f){var l=e as Line;if(l!=null){var a=f(l.StartPoint);var b=f(l.EndPoint);l.StartPoint=a;l.EndPoint=b;return;}var p=e as Polyline;if(p!=null){for(int i=0;i<p.NumberOfVertices;i++){var q=f(p.GetPoint3dAt(i));p.SetPointAt(i,new Point2d(q.X,q.Y));}p.Elevation=0;return;}throw new InvalidOperationException("Unsupported flexible native member "+e.GetRXClass().DxfName);}
  static double Interpolate(double v,double a,double b,double c,double d){return v<=a?v+c-a:v>=b?v+d-b:c+(v-a)*(d-c)/(b-a);}
  public static void PrepareLibrary(Database target,string source){
   var seed=Configuration.Read<AASeed>("section-aa-seed.json");using(var fs=File.Open(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","")!=seed.SourceSha256)throw new InvalidOperationException("Unreviewed A-A source hash.");
   using(var src=new Database(false,true)){src.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,null);src.CloseInput(true);using(var tr=target.TransactionManager.StartTransaction()){
    var bt=(BlockTable)tr.GetObject(target.BlockTableId,OpenMode.ForRead);if(bt.Has(Library))throw new InvalidOperationException("Native library already present.");bt.UpgradeOpen();var lib=new BlockTableRecord{Name=Library};bt.Add(lib);tr.AddNewlyCreatedDBObject(lib,true);ComponentBinding.Register(target,tr);
    var ids=new ObjectIdCollection();foreach(var b in seed.Entities)ids.Add(src.GetObjectId(false,new Handle(Convert.ToInt64(b.Handle,16)),0));var map=new IdMapping();src.WblockCloneObjects(ids,lib.ObjectId,map,DuplicateRecordCloning.MangleName,false);
    foreach(var b in seed.Entities){var id=src.GetObjectId(false,new Handle(Convert.ToInt64(b.Handle,16)),0);var e=(Entity)tr.GetObject(map[id].Value,OpenMode.ForWrite);string role=b.Role;var bounds=b.Bounds;double cy=(bounds[1]+bounds[3])/2;
     // Source coordinates are used only in this hash-gated normalization, never for runtime identity.
     Func<Point3d,Point3d> local=q=>new Point3d(q.X-19957.897749633543,q.Y-4644.288108124732,0);
     if(role.StartsWith("Floor.")){
      e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,-10644.288108124732,0)));
      if(role=="Floor.Slab.Bottom")Map(e,q=>new Point3d(q.X,-250,0));if(role=="Floor.Slab.Top")Map(e,q=>new Point3d(q.X,0,0));
      if(role=="Floor.Slab.Break")Map(e,q=>new Point3d(q.X,Interpolate(q.Y,-200,0,-250,0),0));
      if(role=="Floor.Door.Vertical"){var l=(Line)e;l.StartPoint=new Point3d(60,0,0);l.EndPoint=new Point3d(60,2160,0);}
      if(role=="Floor.Door.ClearHead"){var l=(Line)e;l.StartPoint=new Point3d(0,2000,0);l.EndPoint=new Point3d(60,2000,0);}
      if(role=="Floor.Door.FullHead"){var l=(Line)e;l.StartPoint=new Point3d(0,2160,0);l.EndPoint=new Point3d(200,2160,0);}
     }else if(role=="Car"){
      double min=4349.576625634523,max=8223.514976425868;bool sling=bounds[0]>19050&&bounds[2]<19400;double shift;
      Func<double,double> cabin=y=>y<=4644.288108124732?Interpolate(y,4480.01094784381,4644.288108124732,-100,0):Interpolate(y,4644.288108124732,7094.752528057128,0,2200);
      if(bounds[3]<4484){e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,-100-min,0)));}
      else if(sling){shift=cy>6800?2300-max:-100-min;if(bounds[3]-bounds[1]>700&&(e is Line||e is Polyline))Map(e,q=>new Point3d(q.X-19957.897749633543,q.Y<6000?q.Y-min-100:q.Y-max+2300,0));else e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,shift,0)));}
      else if(e is Line||e is Polyline){Map(e,q=>{double z=cabin(q.Y);if(q.Y>=7137.7)z=2300;return new Point3d(q.X-19957.897749633543,z,0);});}
      else {shift=cabin(cy)-cy;e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,shift,0)));}
      var flatten=e as Line;if(flatten!=null){flatten.StartPoint=new Point3d(flatten.StartPoint.X,flatten.StartPoint.Y,0);flatten.EndPoint=new Point3d(flatten.EndPoint.X,flatten.EndPoint.Y,0);}
     }else if(role=="CW"){
      double low=16991.416430697354,high=19747.299106947947,delta=3000-(high-low);bool span=bounds[3]-bounds[1]>1000&&(e is Line||e is Polyline);
      if(span)Map(e,q=>new Point3d(q.X-19957.897749633543,q.Y-low+(q.Y>(low+high)/2?delta:0),0));
      else e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,-low+(cy>(low+high)/2?delta:0),0)));
     }else if(role=="Governor")e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,-21044.29919945264,0)));
     else if(role=="Hook")e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,-23544.29919945264,0)));
     else {e.TransformBy(Matrix3d.Displacement(new Vector3d(-19957.897749633543,role=="Title"?-4644.288108124732:-3644.288108124732,0)));}
     var hatch=e as Hatch;if(hatch!=null)hatch.Associative=false;
     ComponentBinding.Tag(e,"AASeed."+b.Key,role);
    }tr.Commit();}}
  }
  public static void ImportLibrary(Database db){using(var src=new Database(false,true)){src.ReadDwgFile(Master,FileOpenMode.OpenForReadAndAllShare,true,null);src.CloseInput(true);using(var st=src.TransactionManager.StartTransaction())using(var tr=db.TransactionManager.StartTransaction()){var sb=(BlockTable)st.GetObject(src.BlockTableId,OpenMode.ForRead);var tb=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);if(!tb.Has(Library)){var ids=new ObjectIdCollection(new[]{sb[Library]});src.WblockCloneObjects(ids,db.BlockTableId,new IdMapping(),DuplicateRecordCloning.Ignore,false);}tr.Commit();}}}
  static void Layer(Database db,Transaction tr,string name,bool plot){var lt=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);if(lt.Has(name))return;lt.UpgradeOpen();var l=new LayerTableRecord{Name=name,IsPlottable=plot};lt.Add(l);tr.AddNewlyCreatedDBObject(l,true);}
  public static AAAudit Rebuild(Database db,SectionAAProject data,Point3d? insertion){using(var tr=db.TransactionManager.StartTransaction()){SectionSnapshot bb;var source=VerticalExperimentDrawing.ReadSource(db,tr,out bb);var g=SectionAACalculator.Calculate(data,source.Section.Reference.SourceSlabThickness);var old=Find(db,tr);var f=old.Count>0?Frame(old):new ShaftFrame{Origin=insertion??Point3d.Origin,XAxis=Vector3d.XAxis,YAxis=Vector3d.YAxis,ZAxis=Vector3d.ZAxis};if(old.Count==0&&!insertion.HasValue)throw new InvalidOperationException("Place A-A first.");
   foreach(var e in old.Values){e.UpgradeOpen();e.Erase();}ComponentBinding.Register(db,tr);Layer(db,tr,"AA_Rail",true);Layer(db,tr,"AA_Rope",true);Layer(db,tr,"AA_Datum",false);Layer(db,tr,"AA_Architecture",true);
   var model=(BlockTableRecord)tr.GetObject(Model(db,tr),OpenMode.ForWrite);var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);if(!bt.Has(Library))throw new InvalidOperationException("Clean native master library missing.");var lib=(BlockTableRecord)tr.GetObject(bt[Library],OpenMode.ForRead);
   var pose=Matrix3d.AlignCoordinateSystem(Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis,f.Origin,f.XAxis,f.YAxis,f.ZAxis);
   Action<Entity,string,string> add=(e,id,role)=>{e.TransformBy(pose);Add(db,tr,model,e,Prefix+id,role);};
   Action<string,double,double,double,double,string> line=(id,x,y,x2,y2,layer)=>{var l=new Line(new Point3d(x,y,0),new Point3d(x2,y2,0));l.SetDatabaseDefaults(db);l.Layer=layer;add(l,id,"Line");};
   var axis=new Line(Point3d.Origin,new Point3d(1,0,0)){Visible=false};axis.SetDatabaseDefaults(db);axis.Layer="AA_Datum";add(axis,"Frame.Axis","frame");
   foreach(ObjectId id in lib){var original=(Entity)tr.GetObject(id,OpenMode.ForRead);var tag=Tag(original);string key=tag[0].Substring(7),role=tag[1];
    if(role.StartsWith("Floor.")){foreach(var floor in g.Floors.Where(v=>!v.IsRoof)){if(!floor.Served&&role.StartsWith("Floor.Door"))continue;var e=(Entity)original.Clone();var text=e as MText;if(text!=null){if(role=="Floor.Label")text.Contents=floor.Served?floor.Name:"NO STOP";text.Width=Math.Max(text.Width,text.Contents.Length>3?1600:600);if(role=="Floor.FLLabel")text.Contents=floor.Name+" FL";}e.TransformBy(Matrix3d.Displacement(new Vector3d(0,floor.FflZ,0)));add(e,"Floor."+floor.FloorId+"."+role.Substring(6)+"."+key,role);}continue;}
    var clone=(Entity)original.Clone();double dx=0,dz=0;
    if(role=="Car"){
     var ex=original.GeometricExtents;double mid=(ex.MinPoint.X+ex.MaxPoint.X)/2;
     Func<double,double> offset=x=>x< -1100?-source.Plan.CarRear+1404.594390164566:x> -350?-source.Plan.CarFront+123.872547790641:-source.Plan.CarRow+741.644616181759;
     if(ex.MaxPoint.X-ex.MinPoint.X>500&&(clone is Line||clone is Polyline)){Map(clone,q=>new Point3d(q.X+offset(q.X),q.Y,q.Z));dx=0;}else dx=offset(mid);dz=g.CarZ;
    }
    else if(role=="CW"){dx=-source.Plan.CwRow-(-1700.17465069557);dz=g.CWBottom;}
    else if(role=="Governor"){dx=(source.GovernorProjectedAxisX??-source.Plan.CarRow)-(-1099.48049017503);dz=g.R;}
    else if(role=="Hook")dz=g.Ceiling;
    else if(role=="Title")dz=g.PitZ+1000;
    else {dz=g.PitZ;if(role.Contains("CarBuffer"))dx=-source.Plan.CarRow+741.644616181759;else if(role.Contains("CWBuffer"))dx=-source.Plan.CwRow+1700.17465069557;else if(role.Contains("Tension"))dx=(source.GovernorProjectedAxisX??-source.Plan.CarRow)+1099.48049017503;}
    if(role=="ReferenceOnly.PitSupportNotes"){
     var ex=original.GeometricExtents;var hatch=clone as Hatch;
     if(hatch!=null){while(hatch.NumberOfLoops>0)hatch.RemoveLoopAt(0);var vertices=new Point2dCollection(new[]{new Point2d(-source.Plan.SD-200,ex.MinPoint.Y),new Point2d(200,ex.MinPoint.Y),new Point2d(200,ex.MaxPoint.Y),new Point2d(-source.Plan.SD-200,ex.MaxPoint.Y)});hatch.AppendLoop(HatchLoopTypes.External,vertices,new DoubleCollection(new[]{0.0,0,0,0}));hatch.EvaluateHatch(true);}
     else dx=(ex.MinPoint.X+ex.MaxPoint.X)/2< -1200?-source.Plan.CwRow+1700.17465069557:-source.Plan.CarRow+741.644616181759;
    }
    clone.TransformBy(Matrix3d.Displacement(new Vector3d(dx,dz,0)));add(clone,(role=="CW"?"CW":role=="Car"?"Car":role=="Governor"?"MR.Governor":role=="Hook"?"MR.Hook":role)+"."+key,role);
   }
   double left=-source.Plan.SD,wall=200;var normal=g.Floors.Where(v=>!v.IsRoof).ToArray();var levels=new List<double>{g.PitZ};levels.AddRange(normal.Select(v=>v.FflZ));levels.Add(g.Underside);
   for(int i=0;i<levels.Count-1;i++){line("Shaft.Left.Inner."+i,left,levels[i],left,levels[i+1],"AA_Architecture");line("Shaft.Left.Outer."+i,left-wall,levels[i],left-wall,levels[i+1],"AA_Architecture");line("Shaft.Right.Inner."+i,0,levels[i],0,levels[i+1],"AA_Architecture");}
   foreach(var floor in normal){int i=Array.IndexOf(normal,floor);double top=i+1<normal.Length?normal[i+1].FflZ-250:g.Underside;line("Shaft.Right.Outer."+floor.FloorId,200,floor.FflZ,200,top,"AA_Architecture");}
   line("Shaft.PitOuter",200,g.PitZ-200,200,-250,"AA_Architecture");
   // Native pit floor spans are replaced by datum-driven boundaries; pit equipment retains detail.
   foreach(var p in Find(db,tr).Where(p=>p.Key.StartsWith(Prefix+"Pit.Floor.")).ToArray()){p.Value.UpgradeOpen();p.Value.Erase();}
   line("Pit.Floor.Top",left,g.PitZ,0,g.PitZ,"AA_Architecture");line("Pit.Floor.Bottom",left-wall,g.PitZ-200,200,g.PitZ-200,"AA_Architecture");
   line("MR.Wall.Inner",left,g.R,left,g.Ceiling,"AA_Architecture");line("MR.Wall.Outer",left-wall,g.R,left-wall,g.Ceiling+200,"AA_Architecture");line("MR.Ceiling",left,g.Ceiling,source.RoomFrontX+1800,g.Ceiling,"AA_Architecture");line("MR.Roof",left-wall,g.Ceiling+200,source.RoomFrontX+1800,g.Ceiling+200,"AA_Architecture");
   // Consume validated B-B native geometry, not a second reeving calculation or schematic machine.
   var bbpose=Matrix3d.AlignCoordinateSystem(bb.Frame.Origin,bb.Frame.XAxis,bb.Frame.YAxis,bb.Frame.ZAxis,f.World(new Point3d(0,g.R,0)),f.XAxis,f.YAxis,f.ZAxis);
   foreach(var b in bb.Manifest.Entities){if(b.Role=="frame"||b.Role=="status"||b.Role=="dimension"||b.Role=="spanNote"||(b.Role=="rope"&&!b.Key.EndsWith("CommonTangent")))continue;var e=(Entity)bb.Entities[b.Key].Clone();var tx=e as MText;if(tx!=null&&tx.Contents.Contains("SEC AT")){e.Dispose();continue;}PlaceCoordinatedNote(e,bb.Frame);e.TransformBy(bbpose);Add(db,tr,model,e,Prefix+"MR.Coordinated."+b.Key,b.Role);}
   line("CarRail.Projected", -source.Plan.CarRow,g.PitZ,-source.Plan.CarRow,g.Underside,"AA_Datum");line("CWRail.Projected",-source.Plan.CwRow,g.PitZ,-source.Plan.CwRow,g.Underside,"AA_Datum");
   for(int i=0;i<2;i++){line("CarRail.Edge"+i,source.CarRailProjectedEdges[i],g.PitZ,source.CarRailProjectedEdges[i],g.Underside,"AA_Rail");line("CWRail.Edge"+i,source.CwRailProjectedEdges[i],g.PitZ,source.CwRailProjectedEdges[i],g.Underside,"AA_Rail");}
   // Coincident projected left/right rails share one drawn axis; profile data remains semantic.
   if(source.Section.HasCalculatedPath){line("Rope.CarDrop",source.Section.CarDropX,g.CarZ+2300,source.Section.CarDropX,g.R+source.Section.TractionZ,"AA_Rope");line("Rope.CWDrop",source.Section.CwDropX,g.CWBottom+3000,source.Section.CwDropX,g.R+source.Section.DeflectorZ,"AA_Rope");}
   foreach(string rail in new[]{"Car","CW"})for(int i=0;i<g.Brackets.Elevations.Count;i++){double x=rail=="Car"?-source.Plan.CarRow:-source.Plan.CwRow;line("BracketStation."+rail+"."+i,x-15,g.Brackets.Elevations[i],x+15,g.Brackets.Elevations[i],"AA_Datum");}
   var styles=(DimStyleTable)tr.GetObject(db.DimStyleTableId,OpenMode.ForRead);ObjectId style=styles.Cast<ObjectId>().FirstOrDefault(id=>((DimStyleTableRecord)tr.GetObject(id,OpenMode.ForRead)).Name.EndsWith("Copy of ISO-25"));if(style.IsNull)style=db.Dimstyle;
   Action<string,double,double,double,string> dim=(id,a,b,x,text)=>{var d=new RotatedDimension(Math.PI/2,new Point3d(200,a,0),new Point3d(200,b,0),new Point3d(x,(a+b)/2,0),text,style);d.SetDatabaseDefaults(db);d.DimensionStyle=style;add(d,"Dimension."+id,"dimension");d.RecomputeDimensionBlock(true);};
   for(int i=1;i<normal.Length;i++)dim("Floor."+normal[i-1].FloorId,normal[i-1].FflZ,normal[i].FflZ,3400,"");dim("Pit",g.PitZ,0,4100,"");dim("OH",normal.Last(v=>v.Served).FflZ,g.Underside,4100,"O.H. <>");dim("Travel",normal.First(v=>v.Served).FflZ,normal.Last(v=>v.Served).FflZ,4100,"CAR TRAVEL <>");dim("ShaftTotal",g.PitZ,g.Underside,left-1400,"SHAFT TOTAL HEIGHT <>");
   line("MR.LandingSlab.Top",200,g.R,1630,g.R,"AA_Architecture");line("MR.LandingSlab.Bottom",200,g.Underside,1630,g.Underside,"AA_Architecture");
   var roomLabel=new MText{Location=new Point3d(left+300,g.Ceiling-250,0),TextHeight=200,Contents="M/R"};roomLabel.SetDatabaseDefaults(db);add(roomLabel,"MR.Room.Label","annotation");
   var rlabel=new MText{Location=new Point3d(1450,g.R+240,0),TextHeight=200,Contents="R FL"};rlabel.SetDatabaseDefaults(db);add(rlabel,"MR.R.Label","annotation");
   data.Floors=g.Floors;ProjectDataStorage.Write(db,tr,DataKey,data);ProjectDataStorage.Write(db,tr,"SectionAAStationData",g.Brackets);ProjectDataStorage.Write(db,tr,"SectionAAProfiles",new{Car="T70/65/9",CW="T50/50/5",RailEndAdequacy="NOT_YET_EVALUABLE",Projection="Left/right coincide in A-A projection; not artificially separated"});ProjectDataStorage.Write(db,tr,ManifestKey,new AAManifest{Keys=Find(db,tr).Keys.OrderBy(k=>k).ToList(),InputJson=Json(data)});
   var result=Audit(db,tr);ProjectDataStorage.Write(db,tr,"SectionAAAudit",result);tr.Commit();return result;
  }}
  // A-A has its own governor projection: move only the two inherited support notes clear of it.
  static void PlaceCoordinatedNote(Entity e,ShaftFrame sourceFrame){var t=e as MText;if(t==null)return;double z;if(t.Contents.Contains("TRACTION MACHINE -ROPE FALL"))z=850;else if(t.Contents.Contains("STAND AND MACHINE BEAM SUPPORT"))z=740;else return;t.Location=sourceFrame.World(new Point3d(700,z,0));}
  public static AAAudit Audit(Database db,Transaction tr){var d=ProjectDataStorage.Read<SectionAAProject>(db,tr,DataKey);var m=ProjectDataStorage.Read<AAManifest>(db,tr,ManifestKey);if(d==null||m==null||m.InputJson!=Json(d))throw new InvalidOperationException("A-A missing or stored data changed; update required.");var all=Find(db,tr);if(!all.Keys.OrderBy(k=>k).SequenceEqual(m.Keys))throw new InvalidOperationException("A-A missing/orphan entities.");SectionSnapshot bb;var s=VerticalExperimentDrawing.ReadSource(db,tr,out bb);var g=SectionAACalculator.Calculate(d,s.Section.Reference.SourceSlabThickness);var f=Frame(all);
   Action<double,double,string> near=(a,b,n)=>{if(Math.Abs(a-b)>.03)throw new InvalidOperationException("A-A mismatch "+n+": "+a+" / "+b);};
   foreach(var fl in g.Floors.Where(v=>!v.IsRoof)){string p=Prefix+"Floor."+fl.FloorId+".";var door=all.Where(v=>v.Key.StartsWith(p+"Door.")).ToArray();if(door.Length!=(fl.Served?3:0))throw new InvalidOperationException("Door ownership count");foreach(var v in door){var l=(Line)v.Value;double y=f.Local(l.EndPoint).Y-fl.FflZ;near(y,v.Key.Contains("ClearHead")?2000:2160,v.Key);}var top=(Polyline)all.Single(v=>v.Key.StartsWith(p+"Slab.Top")).Value;var bottom=(Polyline)all.Single(v=>v.Key.StartsWith(p+"Slab.Bottom")).Value;near(f.Local(top.GetPoint3dAt(0)).Y,fl.FflZ,"FFL");near(f.Local(bottom.GetPoint3dAt(0)).Y,fl.FflZ-250,"Slab");}
   foreach(var pair in new Dictionary<string,double>{{"Pit",d.Pit},{"OH",d.OH},{"Travel",g.Travel},{"ShaftTotal",g.Underside-g.PitZ}})near(((Dimension)all[Prefix+"Dimension."+pair.Key]).Measurement,pair.Value,pair.Key);
   foreach(var rail in new[]{"CarRail.Projected","CWRail.Projected"}){var l=(Line)all[Prefix+rail];near(f.Local(l.StartPoint).Y,g.PitZ,rail);near(f.Local(l.EndPoint).Y,g.Underside,rail);near(f.Local(l.StartPoint).X,rail.StartsWith("CarRail")?-s.Plan.CarRow:-s.Plan.CwRow,rail+" horizontal projection");}
   near(f.Local(((Line)all[Prefix+"MR.Ceiling"]).StartPoint).Y-g.R,2200,"MR clear");
   foreach(var fl in normalForAudit(g)){var dim=(RotatedDimension)all[Prefix+"Dimension.Floor."+fl.Item1.FloorId];near(dim.Measurement,fl.Item2.FflZ-fl.Item1.FflZ,"floor interval");}
   foreach(var entry in all.Where(v=>v.Key.StartsWith(Prefix+"MR.Coordinated."))){string sourceKey=entry.Key.Substring((Prefix+"MR.Coordinated.").Length);using(var expected=(Entity)bb.Entities[sourceKey].Clone()){var pose=Matrix3d.AlignCoordinateSystem(bb.Frame.Origin,bb.Frame.XAxis,bb.Frame.YAxis,bb.Frame.ZAxis,f.World(new Point3d(0,g.R,0)),f.XAxis,f.YAxis,f.ZAxis);PlaceCoordinatedNote(expected,bb.Frame);expected.TransformBy(pose);VerticalSectionGeometryAudit.Compare(entry.Value,expected,f,entry.Key);}}
   var car=all.Where(p=>p.Key.StartsWith(Prefix+"Car.")).SelectMany(p=>MachineRoomDependencies.Points(p.Value,f)).ToArray();var cw=all.Where(p=>p.Key.StartsWith(Prefix+"CW.")).SelectMany(p=>MachineRoomDependencies.Points(p.Value,f)).ToArray();
   near(car.Max(p=>p.Y)-car.Min(p=>p.Y),2400,"Car envelope");near(cw.Max(p=>p.Y)-cw.Min(p=>p.Y),3000,"CW envelope");
   return new AAAudit{EntityCount=all.Count,FloorCount=g.Floors.Count,DoorCount=g.Floors.Count(v=>v.Served),Dimensions=all.Count(p=>p.Value is Dimension),BracketStations=g.Brackets.Elevations.Count,MaxBracketSpacing=g.Brackets.ConsecutiveSpacings.Max(),Pit=d.Pit,OH=d.OH,Travel=g.Travel,CarEnvelope=car.Max(p=>p.Y)-car.Min(p=>p.Y),CWEnvelope=cw.Max(p=>p.Y)-cw.Min(p=>p.Y),LayoutStatus=g.LayoutStatus,CWCalibrationStatus=d.CWCalibrationStatus,CodeDiameterRatioStatus=s.CodeDiameterRatioStatus,WrapAngleStatus=s.Section.Arrangement.Reeving==null?"NOT YET EVALUABLE":s.Section.Arrangement.Reeving.WrapAngleStatus,SlabClearanceStatus=s.Section.Arrangement.DeflectorSlabClearanceStatus};
  }
  static IEnumerable<Tuple<Elevator.Core.Geometry.VerticalFloor,Elevator.Core.Geometry.VerticalFloor>> normalForAudit(SectionAAGeometry g){var floors=g.Floors.Where(v=>!v.IsRoof).ToArray();for(int i=1;i<floors.Length;i++)yield return Tuple.Create(floors[i-1],floors[i]);}
  public static void ExportMaster(Database db){using(var tr=db.TransactionManager.StartTransaction())using(var dst=new Database(true,true)){var ids=new ObjectIdCollection(Find(db,tr).Values.Select(e=>e.ObjectId).ToArray());using(var dt=dst.TransactionManager.StartTransaction()){var bt=(BlockTable)dt.GetObject(dst.BlockTableId,OpenMode.ForRead);db.WblockCloneObjects(ids,bt[BlockTableRecord.ModelSpace],new IdMapping(),DuplicateRecordCloning.Ignore,false);var sb=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);db.WblockCloneObjects(new ObjectIdCollection(new[]{sb[Library]}),dst.BlockTableId,new IdMapping(),DuplicateRecordCloning.Ignore,false);dst.Insunits=UnitsValue.Millimeters;ProjectDataStorage.Write(dst,dt,DataKey,ProjectDataStorage.Read<SectionAAProject>(db,tr,DataKey));ProjectDataStorage.Write(dst,dt,ManifestKey,ProjectDataStorage.Read<AAManifest>(db,tr,ManifestKey));dt.Commit();}dst.SaveAs(Master,DwgVersion.AC1032);}}
 }
}

