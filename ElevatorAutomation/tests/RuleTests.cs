using System;
using System.IO;
using System.Web.Script.Serialization;
using Elevator.Core;
public static class RuleTests
{
    private static int count;
    private static void Equal(double actual,double expected,string label) {count++;if(Math.Abs(actual-expected)>1e-7)throw new Exception(label+": "+actual+" != "+expected);}
    public static int Main(string[] args) {
        var p=new JavaScriptSerializer().Deserialize<EquipmentProfile>(File.ReadAllText(args[0]));
        var a=DesignRules.Calculate(1450,1600,p,100);
        Equal(a.CarDbg,1150,"Car DBG");Equal(a.CarWidth,1050,"Car width");Equal(a.CarDepth,1200,"Car depth");Equal(a.TZ,750,"TZ");Equal(a.CarRow,725,"Car row");Equal(a.CwRow,1475,"CW row");Equal(a.CwDbg,1040,"CW DBG");Equal(a.DoorWidth,900,"door");Equal(a.CwWidth,1000,"CW frame");
        foreach(double[] test in new[]{new double[]{1150,740,600},new double[]{1250,840,700},new double[]{1300,840,700},new double[]{1310,900,700},new double[]{1350,940,800},new double[]{1449.9,940,800},new double[]{1450,1040,900},new double[]{1550,1040,1000},new double[]{2000,1040,1000}}) {
            var n=DesignRules.Calculate(test[0],1800,p,100);Equal(n.CwDbg,test[1],"CW selection at "+test[0]);Equal(n.DoorWidth,test[2],"Door selection at "+test[0]);
            Equal(n.CarRow,n.CarFront+n.CarDepth/2,"Guide row at cabin midpoint");
        }
        foreach(double bad in new[]{0.0,-1.0,1000.0,1100.0,Double.NaN,Double.PositiveInfinity}) {
            bool rejected=false;try {DesignRules.Calculate(bad,1600,p,100);}catch(ArgumentException){rejected=true;}
            count++;if(!rejected)throw new Exception("Invalid width accepted: "+bad);
        }
        bool depthRejected=false;try {DesignRules.Calculate(1450,400,p,100);}catch(ArgumentException){depthRejected=true;}if(!depthRejected)throw new Exception("Invalid depth accepted.");count++;
        Console.WriteLine("PASS: "+count+" design-rule assertions.");
        var input=new ProjectInput{ProjectName="North Tower",PlanTitle="Custom plan"};
        var values=PlanAnnotationRules.Values(input,a,new System.Collections.Generic.Dictionary<string,double>{{"cabinDoorReturnMm",75}});
        string formatted=PlanAnnotationRules.Render("{{ProjectName}} | {{PlanTitle}} | {{SW}}x{{SD}} | {{CarWidth}}x{{CarDepth}} | {{CarDBG}}/{{CWDBG}} | {{DoorWidth}} | {{TZ}} | {{Clearance.cabinDoorReturnMm}}",values,v=>v);
        if(formatted!="North Tower | Custom plan | 1450x1600 | 1050x1200 | 1150/1040 | 900 | 750 | 75")throw new Exception("Derived annotation values are wrong.");
        foreach(string token in new[]{"{{Missing}}","{{Capacity}}","{{Stops}}"}) {
            bool rejected=false;try{PlanAnnotationRules.Render(token,values,v=>v);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Unset annotation accepted: "+token);
        }
        input.PlanTitle=null;values=PlanAnnotationRules.Values(input,a,new System.Collections.Generic.Dictionary<string,double>());
        if(values["PlanTitle"]!="SHAFT AND CAR")throw new Exception("Default title changed.");
        input.Capacity=630;input.Stops=5;values=PlanAnnotationRules.Values(input,a,new System.Collections.Generic.Dictionary<string,double>());
        if(PlanAnnotationRules.Render("{{Capacity}}/{{Stops}}",values,v=>v)!="630/5")throw new Exception("Optional metadata wrong.");
        Console.WriteLine("PASS: 6 annotation-rule checks (including combined derived values).");return 0;
    }
}
