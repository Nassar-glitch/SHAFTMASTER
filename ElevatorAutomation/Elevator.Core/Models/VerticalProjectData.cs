using System;
using System.Collections.Generic;
namespace Elevator.Core.Vertical
{
    // Experimental schema, deliberately independent of the validated ProjectInput schema.
    public sealed class VerticalProjectData
    {
        public int SchemaVersion=1;
        public string NamingConvention="BG", FirstLevel="B";
        public List<VerticalFloor> Floors=new List<VerticalFloor>();
        public double PitDepthMm=1000, OverheadMm=4200, MachineRoomClearHeightMm=2200;
        public double CarInternalHeightMm=2200, CarOverallHeightMm=2400;
        public double DoorClearHeightMm=2000, DoorOpeningHeightMm=2160, LandingSlabGraphicalThicknessMm=250;
        public double CounterweightOverallHeightMm=3000, RatedSpeedMps=1;
        public double CarEnvelopeBottomBelowCarFFLMm=100;
        public string CarEnvelopeOffsetSource="PROVISIONAL_V1_DRAWING_DEFAULT";
        public double? CWBottomAbovePitFloorAtHighestServedLandingMm;
        public double ProvisionalCWCalibrationMm=300;
        public string CWAbsolutePositionStatus {get{return CWBottomAbovePitFloorAtHighestServedLandingMm.HasValue?"PROJECT_REFERENCE_SUPPLIED":"PROVISIONAL_REFERENCE_INPUT_REQUIRED";}}
        public string CarReferenceFloorId;
        public double LevelingOffsetMm;
        public double RequestedBracketMaxSpacingMm; // Mandatory: no default.
        public string BracketSpacingSource="ProjectInputPendingFutureCalculation";
        public string SuspensionTopology="1:1";
        public string PitDatumDefinition="Lowest modeled normal-floor FFL (Z=0) to finished pit floor";
        public string OverheadDatumDefinition="Highest served FFL to underside of MR floor slab";
        public string FloorDatumDefinition="FFL = graphical slab top; R = coordinated MR slab top";
        public string FixtureLabel;
    }
    public sealed class VerticalFloor
    {
        public string FloorId,Name;
        public bool Served=true, IsRoof;
        public double FflZ;
    }
    public sealed class VerticalSource
    {
        public ElevatorParameters Plan;
        public MachineRoomSectionParameters Section;
        public double FrontWallThicknessMm, RearWallThicknessMm, RoomFrontX, RoomRearX;
        public double[] CarRailProjectedEdges, CwRailProjectedEdges;
        public double[] CarBracketProjectedEdges, CwBracketProjectedEdges;
        public double LandingDoorBackX, LandingDoorFrontX;
        public double CarDoorBackX, CarDoorFrontX;
        public double CarRearOuterX;
        public double? GovernorProjectedAxisX;
        public string CodeDiameterRatioStatus, GovernorStatus;
    }
    public sealed class VerticalPrimitive
    {
        public string Id, Kind, Layer, Text, Provenance;
        public double[] Points;
        public double TextHeight=40;
    }
    public sealed class BracketLayout
    {
        public List<double> Elevations=new List<double>(), ConsecutiveSpacings=new List<double>();
        public List<string> Observations=new List<string>();
        public string SpacingStatus="PASS", FixingStatus="NOT_YET_EVALUABLE", RailEndAdequacyStatus="NOT_YET_EVALUABLE", RailJointDetailStatus="NOT_YET_IMPLEMENTED";
    }
    public sealed class VerticalGeometry
    {
        public List<VerticalPrimitive> Entities=new List<VerticalPrimitive>();
        public List<string> ReviewItems=new List<string>();
        public List<VerticalFloor> Floors;
        public BracketLayout Brackets;
        public double PitFloorZ, HighestServedZ, LowestServedZ, TravelMm, MRSlabUndersideZ, RoofZ, CeilingZ;
        public double CarFloorZ, CarBottomZ, CarTopZ, CwBottomZ, CwTopZ;
        public string CWAbsolutePositionStatus, LayoutStatus="PASS", CodeDiameterRatioStatus;
        public void Add(string id,string kind,string layer,string provenance,string text,params double[] points)
        {Entities.Add(new VerticalPrimitive{Id="VerticalExperiment."+id,Kind=kind,Layer=layer,Provenance=provenance,Text=text,Points=points});}
        public void Line(string id,string layer,double x,double z,double x2,double z2){Add(id,"Line",layer,"PROJECT_DERIVED",null,x,z,x2,z2);}
        public void Box(string id,string layer,double x,double z,double x2,double z2){Add(id,"Box",layer,"PROJECT_DERIVED",null,x,z,x2,z2);}
        public void Note(string id,string text,double x,double z){Add(id,"Text","Annotation","DRAWING_PRESENTATION",text,x,z);}
    }
}
