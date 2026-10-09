using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core;
namespace Elevator.AutoCAD
{
    public sealed class MachineRoomAuditResult
    {
        public string Status="PASS";
        public int CheckedEntities,CheckedReferencePoints,CheckedSpanConnections;
        public string EngineeringAdequacy="NOT EVALUATED - geometric continuity is not structural, bearing-capacity, rope-path or clearance approval.";
        public string[] Unevaluated={"Manufacturer machine/deflector mounting relationship and minimum working gaps", "Detailed suspension rope paths and cutout envelopes", "Unspecified minimum bearing/structural adequacy", "General interference of unknown equipment and annotation text collision"};
    }
    public static class MachineRoomGeometryAudit
    {
        private static void Near(double actual,double expected,string message){DrawingUpdater.AssertNear(actual,expected,"Geometry audit: "+message);}
        public static MachineRoomAuditResult Run(MachineRoomSnapshot s,ElevatorParameters design)
        {
            if(s.Manifest.DependencyVersion!=1)throw new InvalidOperationException("Machine-room dependency audit requires reviewed bindings; run UPDATEALL to migrate the reference.");
            var result=new MachineRoomAuditResult();
            var support=new Dictionary<string,MachineRoomBinding>();
            foreach(string name in new[]{"P1","P2","P3","P4"})
            {
                var candidates=s.Manifest.Entities.Where(b=>b.Key.StartsWith("MachineRoom.Support."+name+".")&&s.Entities[b.Key] is Polyline).ToArray();
                support[name]=candidates.OrderByDescending(b=>{var p=b.Dependency.Offsets;return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));}).First();
            }
            foreach(var b in s.Manifest.Entities.Where(b=>b.Dependency!=null))
            {
                var d=b.Dependency;var actual=MachineRoomDependencies.Points(s.Entities[b.Key],s.Frame);
                if(d.CircleRadius.HasValue)Near(((Circle)s.Entities[b.Key]).Radius,d.CircleRadius.Value,b.Key+" native hole radius");
                if(actual.Length!=d.Offsets.Length||d.References.Length!=actual.Length)throw new InvalidOperationException("Geometry audit: changed chassis topology "+b.Key);
                for(int i=0;i<actual.Length;i++)
                {
                    var expected=ChassisDependencies.Target(d,i,design.Center,design.CarRow,design.SD,design.CwRow);
                    Near(actual[i].X,expected.X,b.Key+" reference X");Near(actual[i].Y,expected.Y,b.Key+" reference Y");Near(actual[i].Z,expected.Z,b.Key+" reference Z");result.CheckedReferencePoints++;
                }
                if(s.Entities[b.Key].Visible!=b.OriginalVisible)throw new InvalidOperationException("Geometry audit: visible chassis dependency lost "+b.Key);
                if(d.Behavior=="SpanBetweenReferences"||d.Behavior.StartsWith("Stretch"))
                {
                    if(actual.Max(p=>p.Y)-actual.Min(p=>p.Y)<=.02)throw new InvalidOperationException("Geometry audit: collapsed chassis span "+b.Key);
                    // Compare actual span ends with actual support-plate geometry, retaining source offsets.
                    for(int i=0;i<actual.Length;i++)if(d.References[i]!="Machine")
                    {
                        bool rear=d.References[i]=="RearBeam";
                        string name=rear?(d.Offsets[i].X<0?"P3":"P1"):(d.Offsets[i].X<0?"P4":"P2");
                        var plate=support[name];var p=MachineRoomDependencies.Points(s.Entities[plate.Key],s.Frame);
                        double observed=actual[i].Y-p.Average(v=>v.Y);
                        double required=d.Offsets[i].Y-plate.Dependency.Offsets.Average(v=>v.Y);
                        Near(observed,required,b.Key+" reaches "+name+" support row");result.CheckedSpanConnections++;
                    }
                }
                result.CheckedEntities++;
            }
            foreach(var pair in support)
            {
                var b=pair.Value;var p=MachineRoomDependencies.Points(s.Entities[b.Key],s.Frame);
                bool rear=b.Dependency.References[0]=="RearBeam";
                // Preserve source penetration relative to the actual shaft/beam datum. No invented minimum bearing.
                double datum=rear?s.SD:0;
                Near(p.Min(v=>v.Y)-datum,b.Dependency.Offsets.Min(v=>v.Y),pair.Key+" beam-side bearing boundary");
                Near(p.Max(v=>v.Y)-datum,b.Dependency.Offsets.Max(v=>v.Y),pair.Key+" beam-side bearing boundary");
                var structure=s.Manifest.Entities.Where(v=>rear?v.Role=="room":v.Role=="frontBeam").SelectMany(v=>MachineRoomDependencies.Points(s.Entities[v.Key],s.Frame)).ToArray();
                double bandMin=rear?s.SD:structure.Min(v=>v.Y),bandMax=rear?structure.Max(v=>v.Y):structure.Max(v=>v.Y);
                double observedOverlap=Math.Min(p.Max(v=>v.Y),bandMax)-Math.Max(p.Min(v=>v.Y),bandMin);
                double requiredReferenceOverlap=b.Dependency.Offsets.Max(v=>v.Y)-b.Dependency.Offsets.Min(v=>v.Y);
                if(observedOverlap+0.02<requiredReferenceOverlap)throw new InvalidOperationException("Geometry audit: lost reference bearing overlap at "+pair.Key);
                if(p.Min(v=>v.X)<structure.Min(v=>v.X)-.02||p.Max(v=>v.X)>structure.Max(v=>v.X)+.02)throw new InvalidOperationException("Geometry audit: support outside structural span "+pair.Key);
            }
            foreach(var b in s.Manifest.Entities.Where(b=>b.StructuralYOffsets!=null))
            {
                var points=MachineRoomDependencies.Points(s.Entities[b.Key],s.Frame);
                if(points.Length!=b.StructuralYOffsets.Length)throw new InvalidOperationException("Geometry audit: structural topology changed "+b.Key);
                for(int i=0;i<points.Length;i++)Near(points[i].Y,b.StructuralYOffsets[i]+(b.StructuralRear[i]?design.SD:0),b.Key+" structural datum");
            }
            var pitch=s.Get<Polyline>(MachineRoomDrawing.Pitch);
            var pitchPoints=MachineRoomDependencies.Points(pitch,s.Frame);
            Near(pitchPoints.Max(p=>p.Y),design.CwRow,"Deflector reaches CW reference");
            var mark=s.Entities["MachineRoom.DeflectorSheave.CenterMark"];
            Near(MachineRoomDependencies.Points(mark,s.Frame).Average(p=>p.Y),design.CwRow-s.PitchProjection/2,"Deflector center mark");
            foreach(var b in s.Manifest.Entities.Where(b=>s.Entities[b.Key] is RotatedDimension))
            {
                var dim=(RotatedDimension)s.Entities[b.Key];double displayed;
                if(double.TryParse(dim.DimensionText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out displayed))Near(displayed,dim.Measurement,b.Key+" numeric text override");
            }
            return result;
        }
    }
}
