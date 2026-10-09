using System;
using System.Windows.Threading;
namespace Elevator.Solution.Desktop
{
 // Live calculation on the UI thread (WPF timer). Every input change restarts a 300 ms timer; when it fires, the presets
 // are applied and every outdated result is recalculated (LiveCalculation), so the results are always current. Invalid
 // entries keep their field message and the dependent result shows nothing.
 sealed class LiveUpdates
 {
  readonly MainWindow shell;DispatcherTimer timer;DateTime lastCurrencyCheck=DateTime.MinValue;
  public LiveUpdates(MainWindow shell){this.shell=shell;}
  bool CalculationsVisible{get{return shell.ActivePage=="CALCULATIONS";}}
  public void Schedule()
  {
   if(shell.Live.Running)return;
   if(timer==null){timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};timer.Tick+=(s,e)=>{try{Run();}catch(Exception ex){shell.SetStatus(ex.Message);}};}
   timer.Stop();timer.Start();
  }
  public void Run()
  {
   if(timer!=null)timer.Stop();
   var session=shell.Session;
   if(shell.Live.Running||shell.Busy||session==null||session.Project==null)return;
   shell.Live.Recalculate(session.Project,()=>shell.PresetEditing.Apply());
   if(CalculationsVisible)shell.Calculations.RenderAll();
  }
  // Status timer: results whose inputs changed elsewhere (a follower, a fingerprint) are dropped and recalculated.
  public void CheckCurrency()
  {
   if(DateTime.UtcNow-lastCurrencyCheck<TimeSpan.FromSeconds(2))return;lastCurrencyCheck=DateTime.UtcNow;
   var project=shell.Session.Project;
   if(shell.Live.SafetyPitStale(project))InvalidateSafetyPit();
   CheckCarGuideForceCurrency();
   if(shell.Live.ChassisStale(project))InvalidateCalculation();
  }
  public void InvalidateCalculation(){shell.Live.Chassis.Invalidate();InvalidateSafetyPit();CheckCarGuideForceCurrency();if(CalculationsVisible)shell.Calculations.RenderResults();Schedule();}
  public void InvalidateSafetyPit(){shell.Live.SafetyPit=null;if(CalculationsVisible)shell.Calculations.SafetyPit.Render();Schedule();}
  public void InvalidateRopeTraction(){shell.Live.RopeTraction=null;if(CalculationsVisible)shell.Calculations.RopeTraction.Render();Schedule();}
  public void InvalidateCarGuideForces(){shell.Live.CarGuideForces=null;if(CalculationsVisible)shell.Calculations.CarGuideForces.Render();Schedule();}
  public void CheckCarGuideForceCurrency(){if(shell.Live.CarGuideForcesStale(shell.Session.Project))InvalidateCarGuideForces();}
 }
}
