// All dimensions in this source and Parameters.json are millimetres. SOLIDWORKS API geometry uses metres.
public class BodyRecord
{
    public string Name;
    public string Kind;
    public double[] BoundsMm;
    public double VolumeMm3;
    public int FaultCount;
}
