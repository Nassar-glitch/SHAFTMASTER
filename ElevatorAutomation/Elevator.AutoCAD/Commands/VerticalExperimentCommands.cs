using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.Core.Shaft;
using Elevator.Core.Serialization;
using Elevator.Core.Geometry;
namespace Elevator.AutoCAD
{
    public sealed class VerticalExperimentCommands
    {
        static void Cancel(PromptStatus status){if(status!=PromptStatus.OK&&status!=PromptStatus.None)throw new OperationCanceledException("Input cancelled; existing experiment retained.");}
        static double Number(Editor ed,string label,double? current,bool zero)
        {
            var p=new PromptDoubleOptions("\n"+label+": "){AllowNegative=false,AllowZero=zero,AllowNone=current.HasValue,UseDefaultValue=current.HasValue,DefaultValue=current??0};var r=ed.GetDouble(p);Cancel(r.Status);return r.Status==PromptStatus.OK?r.Value:current.Value;
        }
        static int Integer(Editor ed,string label,int? value,int min,int max){var r=ed.GetInteger(new PromptIntegerOptions("\n"+label+": "){LowerLimit=min,UpperLimit=max,AllowNone=value.HasValue,UseDefaultValue=value.HasValue,DefaultValue=value??0});Cancel(r.Status);return r.Status==PromptStatus.OK?r.Value:value.Value;}
        static string Choice(Editor ed,string label,string[] values,string value)
        {var p=new PromptKeywordOptions("\n"+label+": "){AllowNone=value!=null};foreach(string v in values)p.Keywords.Add(v);if(value!=null)p.Keywords.Default=value;var r=ed.GetKeywords(p);Cancel(r.Status);return r.Status==PromptStatus.None?value:r.StringResult;}
        static string Text(Editor ed,string label,string value)
        {var r=ed.GetString(new PromptStringOptions("\n"+label+(value==null?"": " <"+value+">")+": "){AllowSpaces=true});Cancel(r.Status);return String.IsNullOrWhiteSpace(r.StringResult)?value:r.StringResult.Trim();}
        static void Brackets(Editor ed,VerticalProjectData d)
        {
            string value=Choice(ed,"Guide rail bracket MAX spacing (project input; structural calculation pending)",new[]{"3000","2000","1500","1000","Custom"},null);
            d.RequestedBracketMaxSpacingMm=value=="Custom"?Number(ed,"Custom maximum spacing mm",null,false):Double.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
        }
        static void CarReference(Editor ed,VerticalProjectData d)
        {
            var served=d.Floors.Where(f=>f.Served&&!f.IsRoof).ToArray();var current=served.FirstOrDefault(f=>f.FloorId==d.CarReferenceFloorId);var ground=served.FirstOrDefault(f=>f.Name=="G");
            string name=Text(ed,"Displayed car landing ["+String.Join(" / ",served.Select(f=>f.Name).ToArray())+"]",current!=null?current.Name:ground!=null?ground.Name:null);
            var selected=served.SingleOrDefault(v=>v.Name==name);if(selected==null)throw new ArgumentException("Choose a served floor name.");d.CarReferenceFloorId=selected.FloorId;d.LevelingOffsetMm=0;
        }
        static void Setup(Editor ed,VerticalProjectData d)
        {
            int n=Integer(ed,"Number of modeled levels INCLUDING final structural R",d.Floors.Count>0?(int?)d.Floors.Count:null,3,100);
            d.NamingConvention=Choice(ed,"Floor naming",new[]{"BG","Numeric"},d.NamingConvention);d.FirstLevel=Text(ed,"First normal level (BG: B/G/positive integer; Numeric: integer)",d.NamingConvention=="BG"?"B":"-1");
            string mode=Choice(ed,"Floor heights BETWEEN NORMAL LEVELS (R will be derived from OH)",new[]{"Common","Individual"},"Common");var heights=new double[n-2];
            if(mode=="Common"){double h=Number(ed,"Common FFL-to-FFL height mm",null,false);for(int i=0;i<heights.Length;i++)heights[i]=h;}
            else for(int i=0;i<heights.Length;i++)heights[i]=Number(ed,"FFL interval "+(i+1)+" -> "+(i+2)+" mm",null,false);
            d.Floors=FloorStackGenerator.Create(n,d.NamingConvention,d.FirstLevel,heights,d.Floors);
            ed.WriteMessage("\nLevels: "+String.Join(", ",d.Floors.Select(f=>f.Name).ToArray()));
            foreach(var f in d.Floors)f.Served=!f.IsRoof;
            if(Choice(ed,"Any NO STOP normal floors?",new[]{"No","Yes"},"No")=="Yes")
            {
                string text=Text(ed,"NO STOP names separated by spaces",null);if(String.IsNullOrWhiteSpace(text))throw new ArgumentException("Supply NO STOP names.");
                foreach(string name in text.Split(new[]{' ',',',';'},StringSplitOptions.RemoveEmptyEntries)){var floor=d.Floors.SingleOrDefault(f=>f.Name==name&&!f.IsRoof);if(floor==null)throw new ArgumentException("Unknown normal level: "+name);floor.Served=false;}
            }
            d.PitDepthMm=Number(ed,"Pit from lowest modeled FFL to finished pit floor mm",d.PitDepthMm,false);
            d.OverheadMm=Number(ed,"OH from highest served FFL to underside of MR slab mm",d.OverheadMm,false);CarReference(ed,d);Brackets(ed,d);
        }
        public static VerticalProjectData Edit(Document doc,VerticalProjectData data)
        {
            var d=data==null?new VerticalProjectData():Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<VerticalProjectData>(VerticalExperimentDrawing.Json(data));var ed=doc.Editor;
            if(d.Floors.Count==0)Setup(ed,d);
            else
            {
                string part=Choice(ed,"Edit experimental vertical data",new[]{"Floors","PitOH","Car","CW","Brackets","Room","Doors"},"Floors");
                if(part=="Floors")Setup(ed,d);
                if(part=="PitOH"){d.PitDepthMm=Number(ed,"Pit mm",d.PitDepthMm,false);d.OverheadMm=Number(ed,"OH mm",d.OverheadMm,false);}
                if(part=="Car"){CarReference(ed,d);d.CarEnvelopeBottomBelowCarFFLMm=Number(ed,"Envelope bottom below car FFL mm (envelope reference, not member thickness)",d.CarEnvelopeBottomBelowCarFFLMm,true);d.CarEnvelopeOffsetSource="PROJECT_INPUT";d.CarOverallHeightMm=Number(ed,"Overall car envelope mm",d.CarOverallHeightMm,false);d.CarInternalHeightMm=Number(ed,"Internal car height mm",d.CarInternalHeightMm,false);}
                if(part=="CW")
                {
                    d.CounterweightOverallHeightMm=Number(ed,"CW overall height mm",d.CounterweightOverallHeightMm,false);
                    var reference=ed.GetDouble(new PromptDoubleOptions("\nCW bottom above finished pit floor with car level at HIGHEST SERVED landing mm (Enter leaves current reference/status unchanged): "){AllowNone=true,AllowZero=true,AllowNegative=false});
                    Cancel(reference.Status);if(reference.Status==PromptStatus.OK)d.CWBottomAbovePitFloorAtHighestServedLandingMm=reference.Value;
                }
                if(part=="Brackets")Brackets(ed,d);
                if(part=="Room")d.MachineRoomClearHeightMm=Number(ed,"MR floor to ceiling underside clear height mm",d.MachineRoomClearHeightMm,false);
                if(part=="Doors"){d.DoorClearHeightMm=Number(ed,"Door clear height mm",d.DoorClearHeightMm,false);d.DoorOpeningHeightMm=Number(ed,"Full frame/opening height mm",d.DoorOpeningHeightMm,false);}
            }
            using(var tr=doc.Database.TransactionManager.StartTransaction()){SectionSnapshot section;var source=VerticalExperimentDrawing.ReadSource(doc.Database,tr,out section);var g=VerticalSectionCalculator.Calculate(d,source);d.Floors=g.Floors;}return d;
        }
        [CommandMethod("ELEVATORVERTICALEXPERIMENT",CommandFlags.Modal)]
        public void Generate()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;
            try
            {
                VerticalProjectData data;bool installed;
                using(var tr=doc.Database.TransactionManager.StartTransaction()){data=ProjectDataStorage.Read<VerticalProjectData>(doc.Database,tr,VerticalExperimentDrawing.DataKey);installed=VerticalExperimentDrawing.Find(doc.Database,tr).Count>0;}
                if(data==null)data=Edit(doc,null);Point3d? point=null;
                if(!installed){var r=doc.Editor.GetPoint("\nPlace full-section entrance / lowest modeled FFL datum in EMPTY drawing space: ");Cancel(r.Status);point=r.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem);}
                var audit=VerticalExperimentDrawing.Generate(doc.Database,data,point);doc.Editor.Regen();Directory.CreateDirectory(VerticalExperimentDrawing.OutputDirectory);
                string stem=Path.Combine(VerticalExperimentDrawing.OutputDirectory,"Vertical_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));doc.Database.SaveAs(stem+".dwg",DwgVersion.AC1032);Configuration.WriteJson(stem+".audit.json",audit);
                doc.Editor.WriteMessage("\nExperimental section generated. Geometry audit "+audit.Status+"; "+audit.CWAbsolutePositionStatus+".\nSaved "+stem+".dwg\n");
            }
            catch(System.Exception ex){doc.Editor.WriteMessage("\nVertical experiment stopped: "+ex.Message+"\n");}
        }
        [CommandMethod("ELEVATORVERTICALDATA",CommandFlags.Modal)]
        public void Data()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;try{VerticalProjectData data;using(var tr=doc.Database.TransactionManager.StartTransaction())data=ProjectDataStorage.Read<VerticalProjectData>(doc.Database,tr,VerticalExperimentDrawing.DataKey);data=Edit(doc,data);using(var tr=doc.Database.TransactionManager.StartTransaction()){ProjectDataStorage.Write(doc.Database,tr,VerticalExperimentDrawing.DataKey,data);tr.Commit();}doc.Editor.WriteMessage("\nInputs saved. Run ELEVATORVERTICALEXPERIMENT to regenerate; UPDATEALL does not update this experiment.\n");}catch(System.Exception ex){doc.Editor.WriteMessage("\nVertical data unchanged: "+ex.Message+"\n");}
        }
        [CommandMethod("ELEVATORVERTICALAUDIT",CommandFlags.Modal)]
        public void Audit()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;try{using(var tr=doc.Database.TransactionManager.StartTransaction()){var a=VerticalSectionGeometryAudit.Run(doc.Database,tr);doc.Editor.WriteMessage("\nREAD-ONLY AUDIT: "+VerticalExperimentDrawing.Json(a)+"\n");}}catch(System.Exception ex){doc.Editor.WriteMessage("\nVertical audit FAILED: "+ex.Message+"\n");}
        }
    }
}

