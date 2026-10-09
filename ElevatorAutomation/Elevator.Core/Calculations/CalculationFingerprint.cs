using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Elevator.Core.Serialization;
namespace Elevator.Core.Calculations
{
    internal static class CalculationFingerprint
    {
        internal static object ChassisInputs(ChassisCalculationInputs inputs)
        {
            if(inputs==null)return null;
            return typeof(ChassisCalculationInputs).GetFields().Where(f=>f.Name!="SafetyPit"&&f.Name!="CarGuideForces"&&f.Name!="RopeTraction").ToDictionary(f=>f.Name,f=>f.GetValue(inputs));
        }
        internal static string Hash(object value)
        {
            var serializer=CoreJson.Create();
            var canonical=new StringBuilder();
            try{Canonical(serializer.DeserializeObject(serializer.Serialize(value)),serializer,canonical);}
            catch(ArgumentException){return null;}
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-","");
        }
        static void Canonical(object value,IJsonSerializer serializer,StringBuilder output)
        {
            var map=value as IDictionary<string,object>;
            if(map!=null){output.Append('{');bool first=true;foreach(var key in map.Keys.OrderBy(k=>k,StringComparer.Ordinal)){if(!first)output.Append(',');first=false;output.Append(serializer.Serialize(key)).Append(':');Canonical(map[key],serializer,output);}output.Append('}');return;}
            var list=value as IEnumerable;
            if(list!=null&&!(value is string)){output.Append('[');bool first=true;foreach(var item in list){if(!first)output.Append(',');first=false;Canonical(item,serializer,output);}output.Append(']');return;}
            output.Append(serializer.Serialize(value));
        }
    }
}
