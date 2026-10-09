using System;
using System.Linq;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Core.Serialization;
namespace Elevator.Solution.Bridge
{
    public static class StepInputFactory
    {
        public static StepModelParameters Create(ElevatorProject project,DesignContext context,StepModelParameters reference,out ElevatorDesignResult design)
        {
            design=new ElevatorDesignEngine().Calculate(project,context,DrawingViews.All);
            if(!design.CanGenerate)throw new ArgumentException(String.Join("\n",design.Validation.Where(v=>v.BlocksGeneration).Select(v=>v.Message)));
            if(design.SectionAA==null||design.Plan==null||design.Plan.MachineRoomSection==null)throw new ArgumentException("The coordinated four-view design could not be calculated.");
            var p=Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<StepModelParameters>(Wire.Json(reference));
            var plan=design.Plan;var section=plan.MachineRoomSection;var aa=design.SectionAA;var mr=plan.MachineRoom;
            if(!section.HasCalculatedPath)throw new ArgumentException("The preliminary 3D workflow requires the reviewed two-sheave arrangement. Direct rope routing or TZ above 1100 mm needs a reviewed 3D arrangement before export.");
            p.SW=plan.SW;p.SD=plan.SD;p.CarWidth=plan.CarWidth;p.CarDepth=plan.CarDepth;p.CarFront=plan.CarFront;p.CarRow=plan.CarRow;p.CarDBG=plan.CarDbg;
            p.CwDBG=plan.CwDbg;p.CwWidth=plan.CwWidth;p.CwDepth=plan.CwDepth;p.CwRow=plan.CwRow;p.DoorWidth=plan.DoorWidth;
            p.Pit=-aa.PitZ;p.Travel=aa.Underside-project.Shaft.OverheadMm.Value;p.Overhead=project.Shaft.OverheadMm.Value;p.RoofSlab=aa.R-aa.Underside;p.RoomClearHeight=aa.Ceiling-aa.R;
            p.CarFloorZ=aa.CarZ;p.CwBottom=aa.CWBottom;p.CarInternalHeight=design.CarInternalHeightMm;p.CwHeight=design.CounterweightHeightMm;p.LandingSlab=design.LandingSlabThicknessMm;
            p.DoorClearHeight=design.DoorClearHeightMm;p.DoorConstructionHeight=design.DoorConstructionHeightMm;
            p.FloorElevations=aa.Floors.Where(f=>!f.IsRoof).Select(f=>f.FflZ).ToArray();p.ServedFloors=aa.Floors.Where(f=>!f.IsRoof).Select(f=>f.Served).ToArray();
            p.RoomLeft=mr.Extensions.Left;p.RoomRight=mr.Extensions.Right;p.RoomFront=mr.Extensions.Front;p.RoomRear=mr.Extensions.Rear;
            p.ConcreteHeight=section.ConcreteHeight;p.SteelBottom=section.SteelBottomZ;p.SteelDepth=section.Beam.NominalDepthMm;p.BeamDesignation=section.Beam.SectionDesignation;
            p.FrontSupportCenterY=-section.FrontSupportX;p.RearSupportCenterY=-section.RearSupportX;
            p.TractionY=-section.TractionX;p.TractionZAboveRoof=section.TractionZ;p.TractionDiameter=520;
            p.DeflectorY=-section.DeflectorX;p.DeflectorZAboveRoof=section.DeflectorZ;p.DeflectorDiameter=section.DeflectorPitchMm;p.DeflectorVisible=section.DeflectorVisible;
            p.HangerBottomAboveDeflectorCenter=context.SectionReference.HangerBottomAboveDeflectorCenter;
            // Retain the reviewed preliminary component dimensions and local reference offsets.
            // Equipment translates with the car-guide datum; governor X stays on its shaft-left datum, as in the MR updater.
            double dx=(plan.SW-reference.SW)/2,dy=plan.CarRow-reference.CarRow,dz=section.TractionZ-reference.TractionZAboveRoof;
            p.Beam1X=reference.Beam1X+dx;p.Beam2X=reference.Beam2X+dx;p.SupportLeftX=reference.SupportLeftX+dx;
            p.MachineBaseX=reference.MachineBaseX+dx;p.MachineBaseY=reference.MachineBaseY+dy;p.MachineBaseTop=reference.MachineBaseTop+dz;
            p.MachineGearboxX=reference.MachineGearboxX+dx;p.MachineMotorX=reference.MachineMotorX+dx;p.MachineMotorY=reference.MachineMotorY+dy;p.MachineMotorZ=reference.MachineMotorZ+dz;
            p.GovernorY=plan.CarRow+mr.Governor.OffsetFromCarGuideAxisMm.Value+p.GovernorDiameter/2;
            if(p.FloorElevations.Any(z=>z>aa.Underside))throw new ArgumentException("A floor is above the shaft roof datum.");
            return p;
        }
    }
}
