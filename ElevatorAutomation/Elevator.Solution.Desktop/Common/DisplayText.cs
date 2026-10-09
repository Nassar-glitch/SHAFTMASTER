using System;
using System.Globalization;
using Elevator.Core;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop.Common
{
 // Wording of values, statuses and messages on screen (moved unchanged from MainWindow).
 static class DisplayText
 {
  // Short words for engine status codes; the UI and the status bar never show UPPER_SNAKE_CASE codes.
  public static string StatusWord(string code)
  {
   switch(code)
   {
    case null:return "";
    case "CALCULATED":case "CALCULATED_UNDER_ASSUMPTIONS":case "CALCULATED_UNDER_NOTE_ASSUMPTIONS":return "Calculated";
    case "MISSING_INPUT":return "Input missing";
    case "INVALID_INPUT":return "Invalid input";
    case "UPLIFT_REVIEW_REQUIRED":return "Uplift at a bearing";
    case "UNSUPPORTED_CONFIGURATION":return "Not supported";
    case "STALE_GEOMETRY":return "Geometry changed";
    case "INVALID_GEOMETRY":return "Invalid geometry";
    case "INVALID_GEOMETRY_OR_LOAD":return "Invalid geometry or load";
    case "NOT_APPLICABLE":return "Not applicable";
    case "NOT_YET_EVALUABLE":return "Not evaluated";
    case "REVIEW_REQUIRED":return "Check";
   }
   var t=code.Replace('_',' ').ToLowerInvariant();return t.Length==0?t:Char.ToUpperInvariant(t[0])+t.Substring(1);
  }
  public static string Plain(string text)
  {
   if(text==null)return null;
   return System.Text.RegularExpressions.Regex.Replace(text,@"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b",m=>StatusWord(m.Value));
  }
  // Validation page wording: short status words; rule messages are kept, with the status codes spelled plainly.
  public static string StatusText(ValidationStatus s){return s==ValidationStatus.PASS?"OK":s==ValidationStatus.WARNING?"Warning":s==ValidationStatus.FAIL?"Not OK":s==ValidationStatus.NOT_YET_EVALUABLE?"Not evaluated":"Check";}
  public static string MessageText(string m){return m==null?null:Plain(System.Text.RegularExpressions.Regex.Replace(m.Replace("NOT YET EVALUABLE","not evaluated").Replace("NOT_YET_EVALUABLE","not evaluated").Replace("NOT PROVIDED","not entered").Replace("Not provided","Missing").Replace("Vertical upper-to-lower car guide-shoe spacing h (mm)","Guide-shoe spacing h (mm)"),@"\s*\((?:project )?rule [A-Z]{2}-[A-Z0-9-]+\)",""));}
  public static string BearingLabel(string id){return id=="P1"?"right / rear":id=="P2"?"right / front":id=="P3"?"left / rear":"left / front";}
  public static string N(double v){return v.ToString("0.###",CultureInfo.InvariantCulture);}public static string N(double? v){return v.HasValue?N(v.Value):"UNAVAILABLE";}
  public static string PointText(LocalPoint p){return "("+N(p.X)+", "+N(p.Y)+", "+N(p.Z)+")";}
  public static string DisplayValue(string path,object value)
  {
   if(path=="Elevator.SystemId"&&Convert.ToString(value)=="traction-geared")return "Geared Traction";
   if(path=="Suspension.RopingRatio"&&value!=null&&Convert.ToDouble(value)==1)return "1:1";
   return value==null?"":Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
  }
  public static string ManagedText(object v){return v==null?"":v is double?((double)v).ToString("0.###",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture);}
  public static string PageLabel(string page){return page=="CAR & DOORS"?"Car & Doors":System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(page.ToLowerInvariant());}
 }
}
