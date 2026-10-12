using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;
namespace Elevator.Solution.Desktop
{
 // Toolbar commands of the shell: new / open / save, validation, drawing generation, STEP export and the --smoke run.
 sealed class ProjectCommands
 {
  readonly MainWindow shell;
  public GenerationResponse Last;
  public StepExportResult LastStep;
  public ProjectCommands(MainWindow shell){this.shell=shell;}
  ProjectSession Session{get{return shell.Session;}}
  Pages.DrawingsViewModel Output{get{return shell.Drawings.ViewModel;}}
  public ElevatorProject NewProject(){return ProjectSession.NewProject(Path.Combine(shell.Bin,"reviewed-example.elevator.json"),shell.Presets.Library,shell.Context);}
  public void New()
  {
   if(ConfirmDiscard()){shell.Session=new ProjectSession(NewProject());shell.ResetCalculations();shell.Live.Design=null;Last=null;LastStep=null;shell.SetOutputLocation(null);shell.ShowPage("GENERAL");shell.SetStatus("New project ready.");}
  }
  public bool ConfirmDiscard(){return !Session.Dirty||MessageBox.Show(shell,"Discard unsaved project changes?","Unsaved project",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
  public void Open(){if(!ConfirmDiscard())return;var dialog=new OpenFileDialog{Filter="Elevator project|*.elevator.json|JSON|*.json"};if(dialog.ShowDialog(shell)==true){Session.Open(dialog.FileName);shell.ResetCalculations();shell.Live.Design=null;Last=null;LastStep=null;shell.SetOutputLocation(null);shell.ShowPage("GENERAL");shell.SetStatus("Project opened · "+dialog.FileName);}}
  public void Save(bool saveAs)
  {
   shell.Commit();
   var draftErrors=shell.Live.RelevantDraftErrors(Session.Project);
   if(draftErrors.Count>0)throw new ArgumentException("Correct invalid calculation text before saving: "+String.Join(", ",draftErrors.Keys)+". Blank means missing; drawing generation remains available.");
   if(shell.Live.CarGuideForceDraftError!=null)throw new ArgumentException(shell.Live.CarGuideForceDraftError+" Correct h or intentionally leave it blank before saving.");
   string path=Session.FilePath;if(saveAs||String.IsNullOrEmpty(path)){var dialog=new SaveFileDialog{Filter="Elevator project|*.elevator.json",DefaultExt=".elevator.json",FileName="Project.elevator.json"};if(dialog.ShowDialog(shell)!=true)return;path=dialog.FileName;}Session.Save(path);shell.SetStatus("Project saved · "+path);
  }
  // Validation of the current inputs (the Validation page shows it).
  public void Calculate()
  {
   var design=shell.Live.Validate(Session);
   shell.SetStatus(design.CanGenerate?"Validation completed.":"Validation found blocking inputs; see the rows below.");
  }
  public void ValidateAndShow(){shell.Commit();Calculate();shell.ShowPage("VALIDATION");}
  public async void StartGeneration(){try{await Generate();}catch(Exception e){shell.SetStatus(e.Message);if(!shell.Smoke)MessageBox.Show(shell,e.Message,"Generation failed",MessageBoxButton.OK,MessageBoxImage.Warning);}}
  public async void StartStepExport()
  {
   if(shell.Busy)return;
   try
   {
    shell.Commit();var client=new StepExportClient(shell.Bin);client.CheckDependencies();
    ElevatorDesignResult checkedDesign;StepInputFactory.Create(Session.Project,shell.Context,Wire.Read<StepModelParameters>(Path.Combine(shell.Bin,"preliminary-3d-reference.json")),out checkedDesign);
    var dialog=new SaveFileDialog{Title="Export preliminary elevator STEP",Filter="STEP model (*.step;*.stp)|*.step;*.stp",DefaultExt=".step",AddExtension=true,OverwritePrompt=true,FileName="Elevator Assembly.step",InitialDirectory=Directory.Exists(Output.OutputFolder)?Output.OutputFolder:null};
    if(dialog.ShowDialog(shell)!=true)return;
    shell.SetBusy(true);shell.SetStatus("Starting STEP export…");
    LastStep=await client.ExportAsync(Session.Project,dialog.FileName,true,message=>shell.Dispatcher.BeginInvoke(new Action(()=>shell.SetStatus(message))));
    shell.SetOutputLocation(LastStep.OutputPath);shell.ShowPage("DRAWINGS");
    string messageText="STEP exported and reopened successfully.\n"+LastStep.OutputPath+"\n"+LastStep.Verification.ReopenedSolidCount+" solids · Units: mm · Solid faults: "+LastStep.Verification.FaultCount+"\nEditable source: "+LastStep.SourceFolder+"\nLayout: "+LastStep.LayoutStatus+" · Wrap: "+LastStep.WrapAngleStatus+" · D/d: "+LastStep.CodeDiameterRatioStatus;
    if(LastStep.Verification.Intersections.Length>0)messageText+="\n"+LastStep.Verification.Intersections.Length+" component intersections listed in verification.json.";
    shell.SetStatus("STEP EXPORTED SUCCESSFULLY · "+LastStep.ElapsedSeconds.ToString("0.0")+" s · "+LastStep.OutputPath);
    MessageBox.Show(shell,messageText,"STEP export complete",MessageBoxButton.OK,LastStep.Verification.Intersections.Length>0?MessageBoxImage.Warning:MessageBoxImage.Information);
   }
   catch(Exception e){shell.SetStatus(e.Message);MessageBox.Show(shell,e.Message,"STEP export failed",MessageBoxButton.OK,MessageBoxImage.Warning);}
   finally{shell.SetBusy(false);}
  }
  // The A0 sheet of the drawing (L2b): the title block from the project, the company settings and the live results, and the logo.
  Elevator.Core.Layouts.SheetRequest Sheet()
  {
   var company=Elevator.Reporting.Sheet.CompanyReportSettings.Load();var title=Elevator.Core.Layouts.TitleBlockData.Build(Session.Project,shell.Presets.Library,company.ToCompanySettings(),shell.Live,Elevator.Reporting.Docx.ReportClock.Now());
   var logo=company.Logo();
   return new Elevator.Core.Layouts.SheetRequest{Attributes=title.Attributes(),DoorType=Session.Project.Doors==null?null:Session.Project.Doors.Type,
    LogoFile=logo==null?null:company.LogoPath,LogoWidthPx=logo==null?0:logo.PixelWidth,LogoHeightPx=logo==null?0:logo.PixelHeight};
  }
  public async Task Generate()
  {
   if(shell.Busy)return;shell.Commit();Calculate();if(!shell.Live.Design.CanGenerate){shell.ShowPage("VALIDATION");throw new ArgumentException("Correct blocking validation issues before generation.");}
   string path=Path.GetFullPath(Path.Combine(Output.OutputFolder,Output.OutputName));bool confirmed=false;
   if(File.Exists(path)){if(shell.Smoke)throw new IOException("Smoke output already exists; choose a fresh test folder.");confirmed=MessageBox.Show(shell,"Replace this existing drawing after successful generation and verification?\n"+path,"Confirm replacement",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes;if(!confirmed)return;}
   var consent=OutputConsent.Capture(path,confirmed);shell.SetBusy(true);
   try{
    var client=new GenerationClient(shell.Root,shell.Bin,@"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
    Last=await client.GenerateAsync(Session.Project,new GenerationOptions{Views=Session.Views,ArrangeViews=Session.Arrange},consent,message=>shell.Dispatcher.BeginInvoke(new Action(()=>shell.SetStatus(message))),Sheet());
    if(!Last.Success)throw new InvalidOperationException(Last.Error??"Host generation failed.");shell.SetOutputLocation(Last.OutputPath);
    var design=shell.Live.Design;
    design.Validation=Last.Validation;design.LayoutStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),Last.LayoutStatus);design.WrapAngleStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),Last.WrapAngleStatus);design.CodeDiameterRatioStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),Last.CodeDiameterRatioStatus);shell.SetStatus("DRAWING GENERATED SUCCESSFULLY · "+Last.ElapsedSeconds.ToString("0.0")+" s");shell.ShowPage("DRAWINGS");
   }finally{shell.SetBusy(false);}
  }
  // --smoke: the packaged window end to end (screenshots of every page, two native generations).
  public async Task Smoke()
  {
   string evidence=Path.Combine(Output.OutputFolder,"ui-e2e-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(evidence);
   try
   {
    shell.Session=new ProjectSession(ProjectJson.Deserialize(File.ReadAllText(Path.Combine(shell.Bin,"reviewed-example.elevator.json"))));Session.Project.Info.Name="Desktop V0.1 — 1450 × 1600";
    foreach(var f in Session.Project.Building.Floors.Take(Session.Project.Building.Floors.Count-1))f.HeightToNextMm=3000;
    shell.ShowPage("GENERAL");shell.Form.Editors["Info.Name"].Text="Desktop V0.1 — native four-view test";shell.Commit();
    shell.ShowPage("SHAFT");shell.Form.Editors["Shaft.WidthMm"].Text="1450";shell.Form.Editors["Shaft.DepthMm"].Text="1600";shell.Commit();
    Session.Save(Path.Combine(evidence,"baseline.elevator.json"));Session.Open(Session.FilePath);Calculate();
    foreach(string page in shell.PageKeys){shell.ShowPage(page);await Task.Delay(120);shell.Screenshot(Path.Combine(evidence,page.Replace(" & ","-")+".png"));}
    if(shell.Args.Contains("--screenshots-only")){File.WriteAllText(Path.Combine(evidence,"screenshots-result.json"),"{\"Status\":\"PASS\",\"Pages\":10}");return;}
    Output.OutputFolder=evidence;Output.OutputName="AllFour_1450_1600.dwg";shell.ShowPage("DRAWINGS");await Generate();shell.Screenshot(Path.Combine(evidence,"GENERATED.png"));
    if(!Last.Success||Last.ViewsGenerated.Length!=4)throw new Exception("All-four generation failed.");
    Wire.Write(Path.Combine(evidence,"baseline-result.json"),Last);
    var rows=shell.Floors.ViewModel.Rows;
    shell.ShowPage("FLOORS");rows[0].Height="3100";rows[1].Height="2800";rows[2].Served=false;rows[3].Name="Upper";shell.Commit();
    Session.Save(Path.Combine(evidence,"unequal-no-stop.elevator.json"));Output.OutputName="Unequal_NO_STOP.dwg";shell.ShowPage("DRAWINGS");await Generate();Wire.Write(Path.Combine(evidence,"unequal-result.json"),Last);
    shell.ShowPage("FLOORS");shell.Screenshot(Path.Combine(evidence,"FLOORS-UNEQUAL.png"));
    File.WriteAllText(Path.Combine(evidence,"ui-e2e-result.json"),Wire.Json(new{Status="PASS",Application=Assembly.GetExecutingAssembly().Location,Workflow="Actual WPF window; editable controls and floor rows; ProjectJson save/open; desktop Generate; disposable native host; fresh process verify; publish",Baseline="AllFour_1450_1600.dwg",Unequal="Unequal_NO_STOP.dwg"}));
    shell.ExitCode=0;
   }
   catch(Exception e){shell.ExitCode=1;File.WriteAllText(Path.Combine(evidence,"ui-e2e-result.json"),Wire.Json(new{Status="FAIL",Error=e.ToString()}));}
   finally{shell.SetBusy(false);shell.Close();}
  }
 }
}
