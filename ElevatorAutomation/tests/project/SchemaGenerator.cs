using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
using Elevator.Core.GuideRails;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
using Elevator.Core.Suspension;
using Elevator.Core.SafetyGear;
public static class SchemaGenerator
{
    static Dictionary<string,object> Schema(Type type,string name)
    {
        if(name=="SafetyPit"||name=="CarGuideForces"||name=="Report"||name=="RopeTraction"||name=="UserEditedFields")return new Dictionary<string,object>{{"anyOf",new object[]{Schema(type,""),new{type="null"}}}};
        var nullable=Nullable.GetUnderlyingType(type);
        if(nullable!=null)return new Dictionary<string,object>{{"anyOf",new object[]{Schema(nullable,name),new{type="null"}}}};
        if(type==typeof(string))return new Dictionary<string,object>{{"type",new[]{"string","null"}}};
        if(type==typeof(bool))return new Dictionary<string,object>{{"type","boolean"}};
        if(type.IsEnum)return new Dictionary<string,object>{{"type","integer"},{"enum",Enum.GetValues(type).Cast<object>().Select(Convert.ToInt32).ToArray()},{"description",String.Join(", ",Enum.GetNames(type))}};
        if(type==typeof(int)||type==typeof(double))
        {
            var result=new Dictionary<string,object>{{"type",type==typeof(int)?"integer":"number"}};
            if(name=="schemaVersion")result["const"]=1;
            if(new[]{"WidthMm","DepthMm","HeightToNextMm","CapacityKg","SpeedMetresPerSecond","PitDepthMm","OverheadMm","BracketMaximumSpacingMm","ViewMarginMm","TractionSheavePitchDiameterMm","GuideShoeSpacingMm"}.Contains(name))result["exclusiveMinimum"]=0;
            if(name=="RopeCount"){result["minimum"]=4;result["maximum"]=7;}
            if(name=="RopeDiameterMm")result["enum"]=new[]{6.5,8,10,11,12};
            if(name=="RopingRatio")result["enum"]=new[]{1};
            if(name=="MinimumDeflectorSlabClearanceMm")result["minimum"]=0;
            return result;
        }
        if(type==typeof(Dictionary<string,object>))return new Dictionary<string,object>{{"type","object"},{"additionalProperties",true}};
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>))return new Dictionary<string,object>{{"type","array"},{"items",Schema(type.GetGenericArguments()[0],name)}};
        var properties=new Dictionary<string,object>();foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))properties[field.Name]=Schema(field.FieldType,field.Name);
        var required=type==typeof(Elevator.Core.Calculations.ChassisCalculationInputs)||type==typeof(Elevator.Core.SafetyGear.SafetyPitInputs)||type==typeof(Elevator.Core.GuideRails.CarGuideForceInputs)||type==typeof(Elevator.Core.Suspension.RopeTractionInputs)||type==typeof(ProjectReportDetails)?new string[0]:properties.Keys.Where(k=>type!=typeof(ElevatorProject)||(k!="Calculations"&&k!="Report"&&k!="UserEditedFields"&&k!="FollowsPresets")).ToArray();
        return new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}};
    }
    public static void Main(string[] args)
    {
        var schema=Schema(typeof(ProjectDocument),"");schema["$schema"]="https://json-schema.org/draft/2020-12/schema";
        schema["title"]="Elevator Solution project document v1";
        schema["description"]="Lengths mm, angles deg, capacity kg, speed m/s. Null means NOT PROVIDED. JSON shape validation is followed by ProjectValidator and the reviewed Core solvers.";
        File.WriteAllText(args[0],new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue}.Serialize(schema));
    }
}
