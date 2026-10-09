using System.Globalization;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class SuspensionViewModel:PageViewModel
 {
  static readonly CultureInfo PresetInv=CultureInfo.InvariantCulture;
  public SuspensionViewModel():base("SUSPENSION"){}
  public static readonly FieldSpec[] Fields={new FieldSpec("Rope diameter (mm)","Suspension.RopeDiameterMm"),new FieldSpec("Number of ropes","Suspension.RopeCount"),new FieldSpec("Roping ratio","Suspension.RopingRatio",null,true)};
  // Editor checks of the rope / traction data (null: valid).
  public static string Positive(double v){return v>0?null:"Enter a positive value.";}
  public static string UndercutAngle(double v,string groove){if(v<0||v>105)return "β must be 0–105° (EN 81-50 5.11.2.3.1).";if(groove==GrooveTypes.VUnhardened&&v<=0)return "An unhardened V-groove needs an undercut (β > 0).";return null;}
  public static string GrooveAngle(double v,string groove){double min=groove==GrooveTypes.UndercutU?25:35;return v<min||v>=180?"γ must be at least "+min.ToString(PresetInv)+"° (EN 81-50 5.11.2.3.1).":null;}
  public static string BrakingRetardation(double v){return v>=TractionRules.MinimumBrakingRetardationMps2?null:"a must be at least 0.5 m/s² (EN 81-50 5.11.2.2.2).";}
  public static string EffectiveGroove(PresetFlow presets,ElevatorProject project){return PresetFields.GrooveType.Effective(presets.Context(project)) as string;}
 }
}
