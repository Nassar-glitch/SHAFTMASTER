using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
namespace Elevator.Solution.Desktop
{
    public sealed class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            var app=new Application();var window=new MainWindow(args);app.Run(window);return window.ExitCode;
        }
    }
    public sealed partial class MainWindow:Window
    {
        public int ExitCode;
        readonly string bin=AppDomain.CurrentDomain.BaseDirectory,root;
        readonly string[] pages={"GENERAL","SHAFT","FLOORS","CAR & DOORS","MACHINE","SUSPENSION","RAILS","CALCULATIONS","DRAWINGS","VALIDATION","ABOUT"};
        readonly Brush ink=new SolidColorBrush(Color.FromRgb(7,51,101)),accent=new SolidColorBrush(Color.FromRgb(0,133,218));
        ProjectSession session;DesignContext context;ElevatorDesignResult design;GenerationResponse last;StepExportResult lastStep;
        ProgressBar busyIndicator;
        StackPanel content;TextBlock status,title,subtitle;ListBox navigation;Grid shell;DataGrid floorGrid;
        readonly Dictionary<string,TextBox> editors=new Dictionary<string,TextBox>();
        readonly List<Action> committers=new List<Action>();readonly List<FloorRow> floorRows=new List<FloorRow>();
        string activePage="GENERAL",outputFolder,outputName="Elevator Project.dwg";bool busy,smoke;
        readonly string[] args;
        public MainWindow(string[] arguments)
        {
            // Report figures: WPF text measurement and PNG rendering (Elevator.Reporting has no WPF).
            if(FigureRender.Rasterizer==null)FigureRender.Rasterizer=new WpfFigureRasterizer();
            args=arguments;root=FindRoot(bin);
            outputFolder=Path.Combine(root,"output","solution-v01");Directory.CreateDirectory(outputFolder);
            Title="ShaftMaster — Elevator Design Solutions";WindowLayout.Initial(this);Icon=BrandAssets.Image("ShaftMasterMark.png");
            FontFamily=new FontFamily("Segoe UI");FontSize=14;Foreground=ink;Background=new SolidColorBrush(Color.FromRgb(242,245,248));
            ConfigureStyles();
            string contextPath=Path.Combine(bin,"reviewed-context.json");if(File.Exists(contextPath))context=Wire.Read<DesignContext>(contextPath);
            string chassisPath=Path.Combine(bin,"reviewed-chassis-evidence.json");if(context!=null&&File.Exists(chassisPath))context.ChassisEvidence=Wire.Read<Elevator.Core.Calculations.ChassisGeometryEvidence>(chassisPath);
            LoadPresets();
            session=new ProjectSession(NewProject());
            BuildShell();ShowPage("GENERAL");Closing+=(s,e)=>{if(busy){SetStatus("An operation is running. Close is available after it finishes; Minimize and Maximize remain available.");e.Cancel=true;return;}if(!smoke&&!ConfirmDiscard())e.Cancel=true;};
            InitializeChromeEvents();
            Loaded+=async(s,e)=>{WindowLayout.Clamp(this);if(args.Contains("--smoke")){smoke=true;await Smoke();}};
        }
        // The folder with config/ and Templates/: the parent of the binary folder for a release (bin-shaftmaster-vNN), an
        // ancestor for a build in artifacts/bin.
        static string FindRoot(string binary)
        {
            var parent=Directory.GetParent(binary.TrimEnd(Path.DirectorySeparatorChar));
            for(var d=parent;d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"config"))&&Directory.Exists(Path.Combine(d.FullName,"Templates")))return d.FullName;
            return parent.FullName;
        }
        ElevatorProject NewProject()
        {
            // A new project opens complete from config/presets.json (DESIGN-FLOW stage 2); every unlocked value follows the presets.
            // The audited counterweight reference pose and bracket source are kept from the reviewed example.
            var seed=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")));
            return ProjectPresets.CreateNew(presets,context,seed.Counterweight,seed.Rails.BracketSource);
        }
        Button Button(string text,Action action,bool primary=false)
        {
            var b=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,8,0),Background=primary?accent:Brushes.White,Foreground=primary?Brushes.White:ink,BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromRgb(193,206,216)),Cursor=System.Windows.Input.Cursors.Hand};
            b.Click+=(s,e)=>Guard(action);return b;
        }
        void Guard(Action action){try{action();}catch(Exception e){SetStatus(e.Message);if(!smoke)MessageBox.Show(this,e.Message,"ShaftMaster",MessageBoxButton.OK,MessageBoxImage.Warning);else throw;}}
        void BuildShell(){BuildShaftMasterShell();}
        void Note(string text){content.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,15),Foreground=Brushes.SlateGray});}
        void Heading(string text){content.Children.Add(new TextBlock{Text=text,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,15,0,12)});}
        object Value(string path,out object owner,out FieldInfo field)
        {
            var parts=path.Split('.');owner=session.Project;for(int i=0;i<parts.Length-1;i++)owner=owner.GetType().GetField(parts[i]).GetValue(owner);field=owner.GetType().GetField(parts.Last());return field.GetValue(owner);
        }
        void Field(string label,string path,string hint=null,bool readOnly=false)
        {
            object owner;FieldInfo field;object value=Value(path,out owner,out field);
            var panel=new StackPanel();var row=FormRow(label,panel);bool numeric=(Nullable.GetUnderlyingType(field.FieldType)??field.FieldType)!=typeof(string);if(numeric){panel.MaxWidth=250;panel.HorizontalAlignment=HorizontalAlignment.Left;}
            if(readOnly){panel.Children.Add(new TextBlock{Text=DisplayValue(path,value),Padding=new Thickness(10,7,10,7),Background=new SolidColorBrush(Color.FromRgb(228,235,241))});}
            else
            {
                Func<object,string> text=v=>{string t=v==null?"":Convert.ToString(v,CultureInfo.InvariantCulture);return (path=="Shaft.WidthMm"||path=="Shaft.DepthMm")&&t=="0"?"":t;};
                var box=new TextBox{Text=text(value),MinWidth=numeric?130:0};System.Windows.Automation.AutomationProperties.SetName(box,label);editors[path]=box;panel.Children.Add(box);
                var managed=Managed(path);bool programmatic=false;
                box.TextChanged+=(s,e)=>{if(programmatic)return;session.Dirty=true;if(path.StartsWith("Report.")){RefreshProjectStatus();return;} /* sheet metadata: no calculation or drawing input */ design=null;if(path=="Elevator.CapacityKg"||path=="Shaft.WidthMm"||path=="Shaft.DepthMm")InvalidateCarGuideForceDisplay();InvalidateCalculationDisplay();InvalidateRopeTractionDisplay();RefreshProjectStatus();
                    if(managed==null)return;
                    // A managed field takes a parsable value at once so that the fields following it update while unlocked.
                    Type t=Nullable.GetUnderlyingType(field.FieldType)??field.FieldType;double d;int n;
                    if(String.IsNullOrWhiteSpace(box.Text)){if(Nullable.GetUnderlyingType(field.FieldType)==null)return;field.SetValue(owner,null);}
                    else if(t==typeof(double)&&Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&!Double.IsNaN(d)&&!Double.IsInfinity(d))field.SetValue(owner,d);
                    else if(t==typeof(int)&&Int32.TryParse(box.Text,out n))field.SetValue(owner,n);
                    else return;
                    ManagedEdited(managed);};
                if(managed!=null){box.ContextMenu=ManagedResetMenu(managed);managedRefreshers[path]=()=>{if(managed.IsEdited(session.Project))return;programmatic=true;box.Text=text(field.GetValue(owner));programmatic=false;};}
                committers.Add(()=>{object parsed;Type type=Nullable.GetUnderlyingType(field.FieldType)??field.FieldType;
                    if(String.IsNullOrWhiteSpace(box.Text)){if(type==typeof(string)||Nullable.GetUnderlyingType(field.FieldType)!=null)parsed=null;else throw new ArgumentException(label+" is required.");}
                    else if(type==typeof(string))parsed=box.Text;
                    else if(type==typeof(double)){double v;if(!Double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||Double.IsNaN(v)||Double.IsInfinity(v))throw new ArgumentException(label+" must be a finite number.");parsed=v;}
                    else if(type==typeof(int)){int v;if(!Int32.TryParse(box.Text,out v))throw new ArgumentException(label+" must be an integer.");parsed=v;}
                    else throw new ArgumentException("Unsupported editable field "+path);
                    field.SetValue(owner,parsed);
                });
            }
            if(hint!=null)panel.Children.Add(new TextBlock{Text=hint,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(1,5,0,0),TextWrapping=TextWrapping.Wrap});content.Children.Add(row);
        }
        void Commit(){if(floorGrid!=null){floorGrid.CommitEdit(DataGridEditingUnit.Cell,true);floorGrid.CommitEdit(DataGridEditingUnit.Row,true);if(HasValidationError(floorGrid))throw new ArgumentException("Correct the highlighted floor input before continuing.");}foreach(var c in committers)c();ApplyProjectPresets();}
        bool HasValidationError(DependencyObject node){if(System.Windows.Controls.Validation.GetHasError(node))return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(HasValidationError(VisualTreeHelper.GetChild(node,i)))return true;return false;}
        void ShowPage(string page)
        {
            activePage=page;content.Children.Clear();editors.Clear();committers.Clear();formRows.Clear();floorGrid=null;presetRefreshers.Clear();presetChoices.Clear();managedRefreshers.Clear();personsLabel=null;
            ApplyProjectPresets();
            SelectNavigation(page);title.Text=PageLabel(page);subtitle.Text="Geared Traction  ·  Dimensions in millimetres unless noted";RefreshProjectStatus();pageScroll.ScrollToTop();
            if(page=="GENERAL"){
                FillBlanksButton("GENERAL");
                Heading("Project");Field("Project name","Info.Name");ProjectDetails();Heading("Elevator");
                Field("Capacity (kg)","Elevator.CapacityKg");TextBox capacity;if(editors.TryGetValue("Elevator.CapacityKg",out capacity))PersonsLabel(capacity);Field("Speed (m/s)","Elevator.SpeedMetresPerSecond");
                CalculationSheetDetails();
            }else if(page=="SHAFT"){
                FillBlanksButton("SHAFT");Heading("Shaft dimensions");Field("Shaft width · SW (mm)","Shaft.WidthMm");Field("Shaft depth · SD (mm)","Shaft.DepthMm");Field("Pit depth (mm)","Shaft.PitDepthMm");Field("Overhead (mm)","Shaft.OverheadMm");
                Heading("Calculated geometry");Derived();
            }else if(page=="FLOORS")Floors();
            else if(page=="CAR & DOORS"){
                Heading("Car display landing");var select=new ComboBox{ItemsSource=session.Project.Building.Floors.Where(f=>f.IsServed).Select(f=>new Choice{Id=f.FloorId,Name=f.Name}).ToList(),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=session.Project.Car.DisplayFloorId,Padding=new Thickness(8),Margin=new Thickness(0,0,0,14)};
                select.SelectionChanged+=(s,e)=>{session.Project.Car.DisplayFloorId=select.SelectedValue as string;session.Dirty=true;design=null;};content.Children.Add(select);
                Heading("Calculated geometry");Derived();
            }else if(page=="MACHINE"){
                FillBlanksButton("MACHINE");Field("Traction sheave pitch diameter (mm)","Machine.TractionSheavePitchDiameterMm");
                Field("Minimum deflector/slab gap (mm)","MachineRoom.MinimumDeflectorSlabClearanceMm");
            }else if(page=="SUSPENSION"){
                FillBlanksButton("SUSPENSION");Field("Rope diameter (mm)","Suspension.RopeDiameterMm");Field("Number of ropes","Suspension.RopeCount");Field("Roping ratio","Suspension.RopingRatio",null,true);
                RopeTractionInputs();
            }else if(page=="RAILS"){
                FillBlanksButton("RAILS");Field("Maximum bracket spacing (mm)","Rails.BracketMaximumSpacingMm");
            }else if(page=="CALCULATIONS")CalculationsPage();else if(page=="DRAWINGS")Drawings();else if(page=="ABOUT")About();else ValidationPage();
        }
        void Derived()
        {
            var current=design;
            if(current==null||current.Plan==null){try{current=new ElevatorDesignEngine().Calculate(session.Project,context,session.Views);}catch(ArgumentException){current=null;}}
            if(current==null||current.Plan==null)return;
            var p=current.Plan;Note(String.Format(CultureInfo.InvariantCulture,"Car: {0:0.###} × {1:0.###} mm    ·    Car DBG: {2:0.###} mm\nTZ: {3:0.###} mm    ·    CW DBG: {4:0.###} mm    ·    Door width: {5:0.###} mm",p.CarWidth,p.CarDepth,p.CarDbg,p.TZ,p.CwDbg,p.DoorWidth));
        }
        void Floors()
        {
            FillBlanksButton("FLOORS");
            floorRows.Clear();foreach(var f in session.Project.Building.Floors)floorRows.Add(new FloorRow(session,f,()=>{design=null;foreach(var row in floorRows)row.Refresh();}));
            floorGrid=new DataGrid{ItemsSource=floorRows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,MinHeight=220,MaxHeight=390,RowHeight=36,HeadersVisibility=DataGridHeadersVisibility.Column,Background=Brushes.White};
            floorGrid.Columns.Add(new DataGridTextColumn{Header="Floor name",Binding=new Binding("Name"){ValidatesOnExceptions=true},MinWidth=100,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            floorGrid.Columns.Add(new DataGridTextColumn{Header="Height to next (mm)",Binding=new Binding("Height"){ValidatesOnExceptions=true},MinWidth=145,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            floorGrid.Columns.Add(new DataGridCheckBoxColumn{Header="Served",Binding=new Binding("Served"),Width=90});
            floorGrid.Columns.Add(new DataGridTextColumn{Header="Elevation (mm) · calculated",Binding=new Binding("Elevation"),IsReadOnly=true,Width=240});content.Children.Add(floorGrid);
            var newName=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="New floor name"};var interval=new TextBox{Width=110,Margin=new Thickness(0,0,8,0),ToolTip="Added interval in mm",Text=ManagedText(ProjectPresets.NewFloorHeight(presets))};
            var bar=new WrapPanel{Margin=new Thickness(0,16,0,12)};bar.Children.Add(new TextBlock{Text="Name / interval (mm)  ",VerticalAlignment=VerticalAlignment.Center});bar.Children.Add(newName);bar.Children.Add(interval);
            bar.Children.Add(Button("Add before selected",()=>{Commit();var selected=floorGrid.SelectedItem as FloorRow;if(selected==null)throw new ArgumentException("Select a floor above the lowest datum.");double h=Double.Parse(interval.Text,CultureInfo.InvariantCulture);ProjectFloors.InsertBefore(session.Project.Building,selected.Floor.FloorId,new FloorDefinition{Name=newName.Text},h);session.Dirty=true;design=null;ShowPage("FLOORS");}));content.Children.Add(bar);
            var edits=new WrapPanel();edits.Children.Add(Button("Remove",()=>{Commit();var f=SelectedFloor();ProjectFloors.Remove(session.Project.Building,f.FloorId);session.Dirty=true;design=null;ShowPage("FLOORS");}));
            foreach(int delta in new[]{-1,1}){int d=delta;edits.Children.Add(Button(d<0?"Move down the shaft":"Move up the shaft",()=>{Commit();session.MoveIntermediate(SelectedFloor().FloorId,d);design=null;ShowPage("FLOORS");}));}content.Children.Add(edits);
        }
        FloorDefinition SelectedFloor(){var f=floorGrid.SelectedItem as FloorRow;if(f==null)throw new ArgumentException("Select an intermediate floor first.");return f.Floor;}
        void Drawings()
        {
            foreach(var pair in new[]{new Choice{Id="1",Name="Elevator Plan"},new Choice{Id="2",Name="Machine Room Plan"},new Choice{Id="4",Name="Section A-A"},new Choice{Id="8",Name="Section B-B"}}){var flag=(DrawingViews)Int32.Parse(pair.Id);var box=new CheckBox{Content=pair.Name,IsChecked=(session.Views&flag)!=0,Margin=new Thickness(0,0,0,12),FontSize=16};box.Click+=(s,e)=>{if(box.IsChecked==true)session.Views|=flag;else session.Views&=~flag;design=null;};content.Children.Add(box);}
            Heading("Output");var folder=new TextBox{Text=outputFolder,Margin=new Thickness(0,0,0,10)};var file=new TextBox{Text=outputName,Margin=new Thickness(0,0,0,12)};content.Children.Add(new TextBlock{Text="Output folder"});content.Children.Add(folder);content.Children.Add(new TextBlock{Text="DWG filename"});content.Children.Add(file);
            committers.Add(()=>{outputFolder=folder.Text;outputName=file.Text;if(String.IsNullOrWhiteSpace(outputName)||Path.GetFileName(outputName)!=outputName)throw new ArgumentException("Enter a filename without directories.");});
            var arrange=new CheckBox{Content="Automatic coordinated arrangement (measured extents)",IsChecked=session.Arrange,Margin=new Thickness(0,4,0,12)};arrange.Click+=(s,e)=>session.Arrange=arrange.IsChecked==true;content.Children.Add(arrange);
            Field("View margin (mm)","Drawing.ViewMarginMm");Field("Plan title","Drawing.PlanTitle");
            content.Children.Add(Button("GENERATE DRAWINGS",()=>StartGeneration(),true));
            Heading("3D model (STEP AP214, mm)");
            content.Children.Add(Button("Export STEP…",()=>StartStepExport()));
            if(lastStep!=null){Note("STEP exported and verified · "+lastStep.Verification.ReopenedSolidCount+" solids");OutputCard("STEP model",lastStep.OutputPath,lastStep.SourceFolder);}
            if(last!=null&&last.Success){Heading("DRAWING GENERATED SUCCESSFULLY");OutputCard("Coordinated drawing",last.OutputPath,null);var actions=new WrapPanel();actions.Children.Add(Button("Open Drawing",()=>System.Diagnostics.Process.Start(last.OutputPath)));actions.Children.Add(Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(last.OutputPath))));content.Children.Add(actions);}
        }
        void Calculate()
        {
            if(session.Views==0)throw new ArgumentException("Select at least one view.");
            design=new ElevatorDesignEngine().Calculate(session.Project,context,session.Views);
            SetStatus(design.CanGenerate?"Validation completed.":"Validation found blocking inputs; see the rows below.");
        }
        void ValidationPage()
        {
            if(design==null){try{Calculate();}catch(ArgumentException e){Note(e.Message);return;}}
            Heading("Engineering statuses");
            foreach(var pair in new[]{new Choice{Name="Layout",Id=StatusText(design.LayoutStatus)},new Choice{Name="Wrap angle",Id=StatusText(design.WrapAngleStatus)},new Choice{Name="D/d",Id=StatusText(design.CodeDiameterRatioStatus)}})
                content.Children.Add(new TextBlock{Text=pair.Name+"   ·   "+pair.Id,FontWeight=FontWeights.SemiBold,Foreground=pair.Id=="OK"?accent:Brushes.DarkRed,Margin=new Thickness(0,0,0,10)});
            var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,ItemsSource=design.Validation.Select(v=>new{Status=StatusText(v.Status),Field=v.Field,Message=MessageText(v.Message),Blocks=v.BlocksGeneration?"Yes":"No"}).ToList(),MinHeight=220,MaxHeight=420,Background=Brushes.White};
            foreach(var name in new[]{"Status","Field","Message","Blocks"})grid.Columns.Add(new DataGridTextColumn{Header=name,Binding=new Binding(name),MinWidth=name=="Message"?280:70,Width=name=="Message"?new DataGridLength(1,DataGridLengthUnitType.Star):new DataGridLength(name=="Status"?170:100),ElementStyle=new Style(typeof(TextBlock)){Setters={new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap),new Setter(FrameworkElement.MarginProperty,new Thickness(4))}}});content.Children.Add(grid);
        }
        // Validation page wording: short status words; rule messages are kept, with the status codes spelled plainly.
        static string StatusText(ValidationStatus s){return s==ValidationStatus.PASS?"OK":s==ValidationStatus.WARNING?"Warning":s==ValidationStatus.FAIL?"Not OK":s==ValidationStatus.NOT_YET_EVALUABLE?"Not evaluated":"Check";}
        static string MessageText(string m){return m==null?null:Plain(System.Text.RegularExpressions.Regex.Replace(m.Replace("NOT YET EVALUABLE","not evaluated").Replace("NOT_YET_EVALUABLE","not evaluated").Replace("NOT PROVIDED","not entered").Replace("Not provided","Missing").Replace("Vertical upper-to-lower car guide-shoe spacing h (mm)","Guide-shoe spacing h (mm)"),@"\s*\((?:project )?rule [A-Z]{2}-[A-Z0-9-]+\)",""));}
        bool ConfirmDiscard(){return !session.Dirty||MessageBox.Show(this,"Discard unsaved project changes?","Unsaved project",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
        void Open(){if(!ConfirmDiscard())return;var dialog=new OpenFileDialog{Filter="Elevator project|*.elevator.json|JSON|*.json"};if(dialog.ShowDialog(this)==true){session.Open(dialog.FileName);ResetCalculations();design=null;last=null;lastStep=null;SetOutputLocation(null);ShowPage("GENERAL");SetStatus("Project opened · "+dialog.FileName);}}
        void Save(bool saveAs){Commit();if(RelevantDraftErrors.Count>0)throw new ArgumentException("Correct invalid calculation text before saving: "+String.Join(", ",RelevantDraftErrors.Keys)+". Blank means missing; drawing generation remains available.");if(carGuideForceDraftError!=null)throw new ArgumentException(carGuideForceDraftError+" Correct h or intentionally leave it blank before saving.");string path=session.FilePath;if(saveAs||String.IsNullOrEmpty(path)){var dialog=new SaveFileDialog{Filter="Elevator project|*.elevator.json",DefaultExt=".elevator.json",FileName="Project.elevator.json"};if(dialog.ShowDialog(this)!=true)return;path=dialog.FileName;}session.Save(path);SetStatus("Project saved · "+path);}
        async void StartGeneration(){try{await Generate();}catch(Exception e){SetStatus(e.Message);if(!smoke)MessageBox.Show(this,e.Message,"Generation failed",MessageBoxButton.OK,MessageBoxImage.Warning);}}
        void SetBusy(bool value){busy=value;mainPanel.IsEnabled=!value;navigationPanel.IsEnabled=!value;busyIndicator.Visibility=value?Visibility.Visible:Visibility.Collapsed;RefreshProjectStatus();}
        async void StartStepExport()
        {
            if(busy)return;
            try
            {
                Commit();var client=new StepExportClient(bin);client.CheckDependencies();
                ElevatorDesignResult checkedDesign;StepInputFactory.Create(session.Project,context,Wire.Read<StepModelParameters>(Path.Combine(bin,"preliminary-3d-reference.json")),out checkedDesign);
                var dialog=new SaveFileDialog{Title="Export preliminary elevator STEP",Filter="STEP model (*.step;*.stp)|*.step;*.stp",DefaultExt=".step",AddExtension=true,OverwritePrompt=true,FileName="Elevator Assembly.step",InitialDirectory=Directory.Exists(outputFolder)?outputFolder:null};
                if(dialog.ShowDialog(this)!=true)return;
                SetBusy(true);SetStatus("Starting STEP export…");
                lastStep=await client.ExportAsync(session.Project,dialog.FileName,true,message=>Dispatcher.BeginInvoke(new Action(()=>SetStatus(message))));
                SetOutputLocation(lastStep.OutputPath);ShowPage("DRAWINGS");
                string messageText="STEP exported and reopened successfully.\n"+lastStep.OutputPath+"\n"+lastStep.Verification.ReopenedSolidCount+" solids · Units: mm · Solid faults: "+lastStep.Verification.FaultCount+"\nEditable source: "+lastStep.SourceFolder+"\nLayout: "+lastStep.LayoutStatus+" · Wrap: "+lastStep.WrapAngleStatus+" · D/d: "+lastStep.CodeDiameterRatioStatus;
                if(lastStep.Verification.Intersections.Length>0)messageText+="\n"+lastStep.Verification.Intersections.Length+" component intersections listed in verification.json.";
                SetStatus("STEP EXPORTED SUCCESSFULLY · "+lastStep.ElapsedSeconds.ToString("0.0")+" s · "+lastStep.OutputPath);
                MessageBox.Show(this,messageText,"STEP export complete",MessageBoxButton.OK,lastStep.Verification.Intersections.Length>0?MessageBoxImage.Warning:MessageBoxImage.Information);
            }
            catch(Exception e){SetStatus(e.Message);MessageBox.Show(this,e.Message,"STEP export failed",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{SetBusy(false);}
        }
        async Task Generate()
        {
            if(busy)return;Commit();Calculate();if(!design.CanGenerate){ShowPage("VALIDATION");throw new ArgumentException("Correct blocking validation issues before generation.");}
            string path=Path.GetFullPath(Path.Combine(outputFolder,outputName));bool confirmed=false;
            if(File.Exists(path)){if(smoke)throw new IOException("Smoke output already exists; choose a fresh test folder.");confirmed=MessageBox.Show(this,"Replace this existing drawing after successful generation and verification?\n"+path,"Confirm replacement",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes;if(!confirmed)return;}
            var consent=OutputConsent.Capture(path,confirmed);SetBusy(true);
            try{
                var client=new GenerationClient(root,bin,@"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
                last=await client.GenerateAsync(session.Project,new GenerationOptions{Views=session.Views,ArrangeViews=session.Arrange},consent,message=>Dispatcher.BeginInvoke(new Action(()=>SetStatus(message))));
                if(!last.Success)throw new InvalidOperationException(last.Error??"Host generation failed.");SetOutputLocation(last.OutputPath);
                design.Validation=last.Validation;design.LayoutStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),last.LayoutStatus);design.WrapAngleStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),last.WrapAngleStatus);design.CodeDiameterRatioStatus=(ValidationStatus)Enum.Parse(typeof(ValidationStatus),last.CodeDiameterRatioStatus);SetStatus("DRAWING GENERATED SUCCESSFULLY · "+last.ElapsedSeconds.ToString("0.0")+" s");ShowPage("DRAWINGS");
            }finally{SetBusy(false);}
        }
        void Screenshot(string path){UpdateLayout();var bitmap=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(shell);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);}
        async Task Smoke()
        {
            string evidence=Path.Combine(outputFolder,"ui-e2e-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(evidence);
            try
            {
                session=new ProjectSession(ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json"))));session.Project.Info.Name="Desktop V0.1 — 1450 × 1600";
                foreach(var f in session.Project.Building.Floors.Take(session.Project.Building.Floors.Count-1))f.HeightToNextMm=3000;
                ShowPage("GENERAL");editors["Info.Name"].Text="Desktop V0.1 — native four-view test";Commit();
                ShowPage("SHAFT");editors["Shaft.WidthMm"].Text="1450";editors["Shaft.DepthMm"].Text="1600";Commit();
                session.Save(Path.Combine(evidence,"baseline.elevator.json"));session.Open(session.FilePath);Calculate();
                foreach(string page in pages){ShowPage(page);await Task.Delay(120);Screenshot(Path.Combine(evidence,page.Replace(" & ","-")+".png"));}
                if(args.Contains("--screenshots-only")){File.WriteAllText(Path.Combine(evidence,"screenshots-result.json"),"{\"Status\":\"PASS\",\"Pages\":10}");return;}
                outputFolder=evidence;outputName="AllFour_1450_1600.dwg";ShowPage("DRAWINGS");await Generate();Screenshot(Path.Combine(evidence,"GENERATED.png"));
                if(!last.Success||last.ViewsGenerated.Length!=4)throw new Exception("All-four generation failed.");
                Wire.Write(Path.Combine(evidence,"baseline-result.json"),last);
                ShowPage("FLOORS");floorRows[0].Height="3100";floorRows[1].Height="2800";floorRows[2].Served=false;floorRows[3].Name="Upper";Commit();
                session.Save(Path.Combine(evidence,"unequal-no-stop.elevator.json"));outputName="Unequal_NO_STOP.dwg";ShowPage("DRAWINGS");await Generate();Wire.Write(Path.Combine(evidence,"unequal-result.json"),last);
                ShowPage("FLOORS");Screenshot(Path.Combine(evidence,"FLOORS-UNEQUAL.png"));
                File.WriteAllText(Path.Combine(evidence,"ui-e2e-result.json"),Wire.Json(new{Status="PASS",Application=Assembly.GetExecutingAssembly().Location,Workflow="Actual WPF window; editable controls and floor rows; ProjectJson save/open; desktop Generate; disposable native host; fresh process verify; publish",Baseline="AllFour_1450_1600.dwg",Unequal="Unequal_NO_STOP.dwg"}));
                ExitCode=0;
            }
            catch(Exception e){ExitCode=1;File.WriteAllText(Path.Combine(evidence,"ui-e2e-result.json"),Wire.Json(new{Status="FAIL",Error=e.ToString()}));}
            finally{SetBusy(false);Close();}
        }
        sealed class Choice{public string Id{get;set;}public string Name{get;set;}}
    }
}
