namespace Elevator.Core { public struct LocalPoint {
 public double X,Y,Z; public LocalPoint(double x,double y,double z=0) {X=x;Y=y;Z=z;}
 public static LocalPoint operator +(LocalPoint a,LocalPoint b) {return new LocalPoint(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
} }