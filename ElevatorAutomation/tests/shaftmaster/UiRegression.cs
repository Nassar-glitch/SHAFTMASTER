using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Project;using Elevator.Solution.Bridge;using Elevator.Solution.Desktop;
// In-process tests of the compiled packaged WPF application, not OS input automation.
public static class UiRegression
{
 static MainWindow window;static string evidence;static List<object> checks=new List<object>();static int exit;
 static FieldInfo Field(string name){return typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
 static T Get<T>(string name){return (T)Field(name).GetValue(window);}
 static void Set(string name,object value){Field(name).SetValue(window,value);}
 static object Call(string name,params object[] args){return typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,args);}
 static void Check(bool pass,string name){checks.Add(new{Name=name,Status=pass?"PASS":"FAIL"});if(!pass)throw new Exception(name);}
 static object Ordered(object value){var dictionary=value as Dictionary<string,object>;if(dictionary!=null){var sorted=new SortedDictionary<string,object>(StringComparer.Ordinal);foreach(var pair in dictionary)sorted[pair.Key]=Ordered(pair.Value);return sorted;}var array=value as object[];if(array!=null)return array.Select(Ordered).ToArray();return value;}
 static bool SameValues(string a,string b){var json=new System.Web.Script.Serialization.JavaScriptSerializer();return json.Serialize(Ordered(json.DeserializeObject(a)))==json.Serialize(Ordered(json.DeserializeObject(b)));}
 static IEnumerable<T> Children<T>(DependencyObject node)where T:DependencyObject{if(node is T)yield return (T)node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var item in Children<T>(VisualTreeHelper.GetChild(node,i)))yield return item;}
 [STAThread]public static int Main(string[] args)
 {
  evidence=Path.GetFullPath(args[0]);Directory.CreateDirectory(evidence);var app=new Application();window=new MainWindow(new string[0]);Set("smoke",true);
  window.Loaded+=async(s,e)=>{try{await Run();}catch(Exception error){exit=1;checks.Add(new{Status="FAIL",Error=error.ToString()});}finally{Wire.Write(Path.Combine(evidence,"ui-regression.json"),new{Status=exit==0?"PASS":"FAIL",Application=typeof(MainWindow).Assembly.Location,Checks=checks,Display=SystemParameters.WorkArea.ToString(),Dpi=VisualTreeHelper.GetDpi(window).PixelsPerDip,Note="Viewport matrix uses WPF logical sizes matching screen/scaling combinations; Windows scaling settings were not changed."});Set("busy",false);window.Close();}};
  app.Run(window);return exit;
 }
 static async Task Run()
 {
  var seed=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reviewed-example.elevator.json")));var session=new ProjectSession(seed);Set("session",session);string before=ProjectJson.Serialize(seed);
  Check(window.Title=="ShaftMaster — Elevator Design Solutions"&&window.Icon!=null&&window.ResizeMode==ResizeMode.CanResize&&window.WindowStyle==WindowStyle.SingleBorderWindow,"Native window chrome, title and bundled icon");
  foreach(string page in Get<string[]>("pages")){Call("ShowPage",page);window.UpdateLayout();Call("Commit");Call("Screenshot",Path.Combine(evidence,page.Replace(" & ","-")+".png"));}
  Check(ProjectJson.Serialize(session.Project)==before,"All ten views and input commits preserve every serialized project value");
  string copy=Path.Combine(evidence,"unchanged-copy.elevator.json");File.WriteAllText(Path.Combine(evidence,"before.json"),before);session.Save(copy);session.Open(copy);File.WriteAllText(Path.Combine(evidence,"after.json"),ProjectJson.Serialize(session.Project));Check(SameValues(ProjectJson.Serialize(session.Project),before)&&!session.Dirty,"Existing project save/reopen retains exact values and clears dirty state");
  Call("ShowPage","GENERAL");var editors=Get<Dictionary<string,TextBox>>("editors");editors["Info.Name"].Text="Unsaved UI test";Check(session.Dirty&&Get<TextBlock>("projectName").Text=="Unsaved UI test"&&Get<TextBlock>("dirtyLabel").Visibility==Visibility.Visible,"Live project name and separate unsaved indicator");session.Open(copy);
  Call("ShowPage","GENERAL");Check(Children<TextBlock>(Get<StackPanel>("content")).Any(t=>t.Text=="Geared Traction")&&Children<TextBlock>(Get<StackPanel>("content")).Any(t=>t.Text=="1:1"),"Friendly values do not change internal fields");
  Call("Calculate");Check(Get<ElevatorDesignResult>("design").CanGenerate,"Reviewed project validation remains generatable");
  foreach(int width in new[]{1366,1920})foreach(double scale in new[]{1.0,1.25,1.5})
  {
   var work=new Rect(0,0,width/scale,(width==1366?728:1040)/scale);var fitted=WindowLayout.Fit(new Rect(-2400,-1300,1220,820),work);Check(work.Contains(fitted),"Off-screen bounds clamped: "+width+" at "+scale);
   window.Width=fitted.Width;window.Height=fitted.Height;await Task.Delay(70);
   foreach(string page in Get<string[]>("pages"))
   {
    Call("ShowPage",page);window.UpdateLayout();var toolbar=Get<WrapPanel>("toolbar");var main=Get<DockPanel>("mainPanel");
    foreach(var button in Children<Button>(toolbar)){var p=button.TranslatePoint(new Point(0,0),main);Check(p.X>=-1&&p.X+button.ActualWidth<=main.ActualWidth+1&&p.Y>=-1&&p.Y+button.ActualHeight<=main.ActualHeight+1,"Toolbar reachable: "+page+" "+width+"/"+scale+" "+button.Content);}
    Check(Get<ScrollViewer>("pageScroll").ActualHeight>40,"Page scroll viewport available: "+page+" "+width+"/"+scale);
   }
   Call("ShowPage","GENERAL");window.UpdateLayout();Call("Screenshot",Path.Combine(evidence,"GENERAL-"+width+"-"+(scale*100)+".png"));
  }
  var negativeWork=new Rect(-1920,0,1920,1040);Check(negativeWork.Contains(WindowLayout.Fit(new Rect(2000,2000,1400,1100),negativeWork)),"Monitor with negative origin clamps restored bounds");
  window.Width=720;window.Height=420;Call("ShowPage","FLOORS");window.UpdateLayout();Check(Get<ScrollViewer>("pageScroll").ActualHeight>40,"Minimum window retains scrollable form");Call("Screenshot",Path.Combine(evidence,"FLOORS-MINIMUM.png"));
  Call("SetBusy",true);Check(!Get<DockPanel>("mainPanel").IsEnabled&&!Get<DockPanel>("navigationPanel").IsEnabled,"Busy operation locks duplicate submissions and input changes");Call("SetBusy",false);Check(Get<DockPanel>("mainPanel").IsEnabled&&Get<DockPanel>("navigationPanel").IsEnabled,"Busy reset restores enabled state");
  session.Open(copy);string originalRoot=Get<string>("root");Set("root",Path.Combine(evidence,"missing-template-test"));Set("outputFolder",evidence);Set("outputName","MustNotPublish.dwg");Call("ShowPage","DRAWINGS");bool failed=false;
  try{await (Task)Call("Generate");}catch(Exception error){File.WriteAllText(Path.Combine(evidence,"expected-background-failure.txt"),error.ToString());failed=error.Message.Contains("ExistingPlan_Master.dwg");}finally{Set("root",originalRoot);}
  Check(failed&&!Get<bool>("busy")&&Get<DockPanel>("mainPanel").IsEnabled&&Get<DockPanel>("navigationPanel").IsEnabled&&!File.Exists(Path.Combine(evidence,"MustNotPublish.dwg")),"Real background generation failure restores UI and publishes no drawing");
  Call("SetOutputLocation",Path.Combine(evidence,"example.step"));Check(Get<Button>("outputFolderButton").Visibility==Visibility.Visible,"Successful output exposes actual output-folder action");Call("SetStatus","STEP EXPORTED SUCCESSFULLY · "+Path.Combine(evidence,"example.step"));Check(!Get<TextBlock>("status").Text.Contains(evidence)&&Get<string>("detailedStatus").Contains(evidence),"Concise footer keeps full path in operation details");
  var original=File.ReadAllBytes(Path.Combine(evidence,"..","..","..","Elevator.Solution.Desktop","Assets","ShaftMasterLogo.png"));using(var embedded=typeof(MainWindow).Assembly.GetManifestResourceStream("ShaftMaster.Assets.ShaftMasterLogo.png")){var bytes=new MemoryStream();embedded.CopyTo(bytes);Check(original.SequenceEqual(bytes.ToArray()),"Approved logo embedded byte-for-byte");}
 }
}
