using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace ElevatorPlanGenerator
{
    public sealed class Commands
    {
        public static Layout LastLayout;
        public static string LastOutput;
        public static string LastError;
        public static Dictionary<string,double> LastDimensions;
        [CommandMethod("ELEVATORUPDATE",CommandFlags.Modal)]
        public void Update()
        {
            Document doc=Application.DocumentManager.MdiActiveDocument;Editor ed=doc.Editor;LastError=null;LastOutput=null;
            try {
                if(doc.Database.TileMode==false) throw new InvalidOperationException("Switch to the Model tab before running ELEVATORUPDATE.");
                var manifest=Configuration.Read<BindingFile>("template-bindings.json");var profile=Configuration.Read<EquipmentProfile>("equipment-profile.json");
                double oldSW,oldSD;
                using(Transaction tr=doc.Database.TransactionManager.StartTransaction()) {var s=DrawingReader.Read(doc.Database,tr,manifest);oldSW=s.Layout.SW;oldSD=s.Layout.SD;}
                var opt=new PromptDoubleOptions("\nShaft Width SW (mm)") {AllowNegative=false,AllowZero=false,AllowNone=true,UseDefaultValue=true,DefaultValue=oldSW};
                PromptDoubleResult sw=ed.GetDouble(opt);if(sw.Status!=PromptStatus.OK) return;
                opt=new PromptDoubleOptions("\nShaft Depth SD (mm)") {AllowNegative=false,AllowZero=false,AllowNone=true,UseDefaultValue=true,DefaultValue=oldSD};
                PromptDoubleResult sd=ed.GetDouble(opt);if(sd.Status!=PromptStatus.OK) return;
                string outputDir=Path.Combine(Configuration.Root,"output");Directory.CreateDirectory(outputDir);
                string name="Elevator_SW"+sw.Value.ToString("0.###",CultureInfo.InvariantCulture)+"_SD"+sd.Value.ToString("0.###",CultureInfo.InvariantCulture)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".dwg";
                string output=Path.Combine(outputDir,name);Layout target;Dictionary<string,double> expected,gaps;
                using(Transaction tr=doc.Database.TransactionManager.StartTransaction()) {
                    DrawingSnapshot snapshot=DrawingReader.Read(doc.Database,tr,manifest);
                    target=DesignRules.Calculate(sw.Value,sd.Value,profile,snapshot.Layout.CwDepth);
                    var calculator=new GeometryCalculator(snapshot,target);
                    gaps=DrawingUpdater.Preflight(snapshot,calculator);
                    expected=DrawingUpdater.Apply(doc.Database,tr,snapshot,calculator);
                    DrawingUpdater.Validate(DrawingReader.Read(doc.Database,tr,manifest),target,expected);
                    tr.Commit();
                }
                ed.Regen();
                using(Transaction tr=doc.Database.TransactionManager.StartTransaction()) DrawingUpdater.Validate(DrawingReader.Read(doc.Database,tr,manifest),target,expected);
                doc.Database.SaveAs(output,DwgVersion.AC1032);
                LastLayout=target;LastDimensions=expected;LastOutput=output;
                Configuration.WriteJson(Path.ChangeExtension(output,".json"),new {command="ELEVATORUPDATE",profile=profile.Version,input=new {SW=sw.Value,SD=sd.Value},calculated=target,clearances=gaps,dimensionChecks=expected,output=output});
                ed.WriteMessage("\nELEVATORUPDATE complete. Car: "+target.CarWidth+" x "+target.CarDepth+"; Car DBG: "+target.CarDbg+"; CW DBG: "+target.CwDbg+"; Door: "+target.DoorWidth+"; TZ: "+target.TZ+" mm.\nSaved separate DWG: "+output+"\n");
            } catch(System.Exception ex) {LastError=ex.Message;ed.WriteMessage("\nELEVATORUPDATE stopped: "+ex.Message+"\n");}
        }
    }
}
