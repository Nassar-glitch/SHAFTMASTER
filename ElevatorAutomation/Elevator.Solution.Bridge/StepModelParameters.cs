using System;
namespace Elevator.Solution.Bridge {
public sealed class StepModelParameters {
 public double SW=1450,SD=1600,CarWidth=1050,CarDepth=1200,CarFront=125,CarRow=725,CarDBG=1150,CwDBG=1040,CwWidth=1000,CwDepth=100,CwRow=1475,DoorWidth=900;
 public double Pit=1000,Travel=12000,Overhead=4200,RoofSlab=200,LandingSlab=250,RoomClearHeight=2200;
 public double CarFloorZ=0,CarInternalHeight=2200,CarFrameBottom=-100,CarFrameTop=2300,CwBottom=12347.128322572622,CwHeight=3000;
 public bool[] ServedFloors; public bool DeflectorVisible=true; public string BeamDesignation="UPN140";
 public double DoorClearHeight=2000,DoorConstructionHeight=2160,SupportLeftX=400,MachineBaseX=795,MachineBaseY=755,MachineBaseTop=760,MachineGearboxX=800,MachineMotorX=980,MachineMotorY=500,MachineMotorZ=967.796894885352;
 public double[] FloorElevations={0,3000,6000,9000,12000};
 public double WallSide=195,WallRear=200,WallFront=200,RoomLeft=525,RoomRight=525,RoomFront=900,RoomRear=0;
 public double ConcreteHeight=350,SteelBottom=531.5751416887069,SteelDepth=140,FrontSupportCenterY=-110.61188555485558,RearSupportCenterY=1710.6118855548557;
 public double TractionY=985,TractionZAboveRoof=1007.0914284303582,TractionDiameter=520,DeflectorY=1350,DeflectorZAboveRoof=406.57514168870694,DeflectorDiameter=250;
 public double PulleyWidth=100.070883886248,PanelThickness=25,FloorThickness=50,FrameMemberWidth=25,FrameMemberDepth=80,DoorThickness=20;
 public double BeamWidth=65,BeamWeb=7,BeamFlange=9,Beam1X=550,Beam2X=925,ConcreteWidth=775,ConcreteDepth=200;
 public double GovernorX=208.82,GovernorY=1000,GovernorZAboveRoof=350,GovernorDiameter=250,GovernorWidth=50;
 public double CeilingThickness=200,PitBaseThickness=200,LandingApronDepth=500,HangerWidth=16,HangerDepth=30;
 public double HangerBottomAboveDeflectorCenter=17.45307743718149;
}
}
