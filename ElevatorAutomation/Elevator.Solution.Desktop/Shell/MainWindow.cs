using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Elevator.Core.Application;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Figures;
using Elevator.Solution.Desktop.Common;
using Elevator.Solution.Desktop.Pages;
namespace Elevator.Solution.Desktop
{
 // The shell of ShaftMaster: navigation, toolbar, page header and status bar; it shows one page view at a time
 // (Pages/<Page>). Project, preset and calculation state live in the Elevator.Core.Application services.
 public sealed class MainWindow:Window
 {
  public int ExitCode;
  internal readonly string Bin=AppDomain.CurrentDomain.BaseDirectory;
  internal string Root;
  internal readonly string[] PageKeys={"GENERAL","SHAFT","FLOORS","CAR & DOORS","MACHINE","SUSPENSION","RAILS","CALCULATIONS","DRAWINGS","VALIDATION","ABOUT"};
  internal readonly Brush Ink=new SolidColorBrush(Color.FromRgb(7,51,101)),Accent=new SolidColorBrush(Color.FromRgb(0,133,218));
  // Application services (Elevator.Core.Application).
  internal ProjectSession Session;
  internal DesignContext Context;
  internal readonly PresetFlow Presets;
  internal readonly LiveCalculation Live;
  // Shell parts.
  internal readonly PageForm Form;
  internal readonly LiveUpdates Updates;
  internal readonly PresetEditing PresetEditing;
  internal readonly ReportExport Reports;
  internal readonly ProjectCommands Commands;
  // Pages.
  readonly Dictionary<string,PageView> views=new Dictionary<string,PageView>();
  internal readonly GeneralPage General;
  internal readonly FloorsPage Floors;
  internal readonly CalculationsPage Calculations;
  internal readonly DrawingsPage Drawings;
  internal string ActivePage="GENERAL";
  internal bool Busy,Smoke;
  internal readonly string[] Args;
  // Shell controls.
  internal Grid LayoutRoot;internal ListBox Navigation;internal DockPanel MainPanel,NavigationPanel;internal ScrollViewer PageScroll;internal WrapPanel Toolbar;
  internal TextBlock StatusText,ProjectName,DirtyLabel;internal Button OutputFolderButton;
  internal string DetailedStatus="Ready";
  ProgressBar busyIndicator;TextBlock title,subtitle,operationLabel;
  string actualOutput;bool selectingNavigation;DispatcherTimer statusTimer;
  readonly List<FrameworkElement> toolbarGroups=new List<FrameworkElement>();
  TestAutomation automation;
  // In-process test access (InternalsVisibleTo the UI test programs).
  internal TestAutomation Automation{get{return automation??(automation=new TestAutomation(this));}}
  public MainWindow(string[] arguments)
  {
   // Report figures: WPF text measurement and PNG rendering (Elevator.Reporting has no WPF).
   if(FigureRender.Rasterizer==null)FigureRender.Rasterizer=new WpfFigureRasterizer();
   Args=arguments;Root=FindRoot(Bin);
   string outputFolder=Path.Combine(Root,"output","solution-v01");Directory.CreateDirectory(outputFolder);
   Form=new PageForm(this);Updates=new LiveUpdates(this);PresetEditing=new PresetEditing(this);Reports=new ReportExport(this);Commands=new ProjectCommands(this);
   Title="ShaftMaster — Elevator Design Solutions";WindowLayout.Initial(this);Icon=BrandAssets.Image("ShaftMasterMark.png");
   FontFamily=new FontFamily("Segoe UI");FontSize=14;Foreground=Ink;Background=new SolidColorBrush(Color.FromRgb(242,245,248));
   ConfigureStyles();
   string contextPath=Path.Combine(Bin,"reviewed-context.json");if(File.Exists(contextPath))Context=Wire.Read<DesignContext>(contextPath);
   string chassisPath=Path.Combine(Bin,"reviewed-chassis-evidence.json");if(Context!=null&&File.Exists(chassisPath))Context.ChassisEvidence=Wire.Read<Elevator.Core.Calculations.ChassisGeometryEvidence>(chassisPath);
   Presets=PresetFlow.Load(Path.Combine(Bin,"presets.json"),Context);Live=new LiveCalculation(Context,Presets);
   General=new GeneralPage(this);Floors=new FloorsPage(this);Calculations=new CalculationsPage(this);Drawings=new DrawingsPage(this,outputFolder);
   foreach(var view in new PageView[]{General,new ShaftPage(this),Floors,new CarDoorsPage(this),new MachinePage(this),new SuspensionPage(this),new RailsPage(this),Calculations,Drawings,new ValidationPage(this),new AboutPage(this)})views[view.Key]=view;
   Session=new ProjectSession(Commands.NewProject());
   BuildShell();ShowPage("GENERAL");Closing+=(s,e)=>{if(Busy){SetStatus("An operation is running. Close is available after it finishes; Minimize and Maximize remain available.");e.Cancel=true;return;}if(!Smoke&&!Commands.ConfirmDiscard())e.Cancel=true;};
   InitializeChromeEvents();
   Loaded+=async(s,e)=>{WindowLayout.Clamp(this);if(Args.Contains("--smoke")){Smoke=true;await Commands.Smoke();}};
  }
  // The folder with config/ and Templates/: the parent of the binary folder for a release (bin-shaftmaster-vNN), an
  // ancestor for a build in artifacts/bin.
  static string FindRoot(string binary)
  {
   var parent=Directory.GetParent(binary.TrimEnd(Path.DirectorySeparatorChar));
   for(var d=parent;d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"config"))&&Directory.Exists(Path.Combine(d.FullName,"Templates")))return d.FullName;
   return parent.FullName;
  }
  internal void Guard(Action action){try{action();}catch(Exception e){SetStatus(e.Message);if(!Smoke)MessageBox.Show(this,e.Message,"ShaftMaster",MessageBoxButton.OK,MessageBoxImage.Warning);else throw;}}
  // Writes the visible editors into the project (floor grid first); invalid text stops with its message.
  internal void Commit(){var grid=Form.FloorGrid;if(grid!=null){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);if(HasValidationError(grid))throw new ArgumentException("Correct the highlighted floor input before continuing.");}foreach(var c in Form.Committers)c();PresetEditing.Apply();}
  bool HasValidationError(DependencyObject node){if(System.Windows.Controls.Validation.GetHasError(node))return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(HasValidationError(VisualTreeHelper.GetChild(node,i)))return true;return false;}
  internal void ShowPage(string page)
  {
   ActivePage=page;Form.Clear();
   PresetEditing.Apply();
   SelectNavigation(page);title.Text=DisplayText.PageLabel(page);subtitle.Text="Geared Traction  ·  Dimensions in millimetres unless noted";RefreshProjectStatus();PageScroll.ScrollToTop();
   PageView view;if(!views.TryGetValue(page,out view))view=views["VALIDATION"];view.Build();
  }
  // Every result and the result panels start again (new or opened project).
  internal void ResetCalculations(){Live.Reset();Calculations.ResetViews();}
  internal void SetBusy(bool value){Busy=value;MainPanel.IsEnabled=!value;NavigationPanel.IsEnabled=!value;busyIndicator.Visibility=value?Visibility.Visible:Visibility.Collapsed;RefreshProjectStatus();}
  internal void Screenshot(string path){UpdateLayout();var bitmap=new RenderTargetBitmap((int)LayoutRoot.ActualWidth,(int)LayoutRoot.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(LayoutRoot);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);}

  // ---------- chrome: navigation, toolbar, header, status bar ----------
  void ConfigureStyles()
  {
   var style=new Style(typeof(TextBox));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,6,10,6)));style.Setters.Add(new Setter(Control.MinHeightProperty,34.0));style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));style.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(193,205,218))));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.White));
   var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Control.BorderBrushProperty,Accent));focus.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(2)));style.Triggers.Add(focus);Resources.Add(typeof(TextBox),style);
   var dataStyle=new Style(typeof(DataGrid));dataStyle.Setters.Add(new Setter(Control.FontSizeProperty,13.0));dataStyle.Setters.Add(new Setter(DataGrid.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Auto));dataStyle.Setters.Add(new Setter(DataGrid.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto));Resources.Add(typeof(DataGrid),dataStyle);
  }
  void BuildShell()
  {
   LayoutRoot=new Grid{Background=Background};LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition());LayoutRoot.RowDefinitions.Add(new RowDefinition());LayoutRoot.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=LayoutRoot;
   NavigationPanel=new DockPanel{Background=Ink,LastChildFill=true};Grid.SetRowSpan(NavigationPanel,2);LayoutRoot.Children.Add(NavigationPanel);
   var brand=new StackPanel{Margin=new Thickness(16,19,12,20),Orientation=Orientation.Horizontal};DockPanel.SetDock(brand,Dock.Top);NavigationPanel.Children.Add(brand);
   brand.Children.Add(new Border{Width=34,Height=48,Background=Brushes.White,CornerRadius=new CornerRadius(4),Padding=new Thickness(3),Margin=new Thickness(0,0,8,0),Child=new Image{Source=BrandAssets.Image("ShaftMasterMark.png"),Stretch=Stretch.Uniform}});
   var word=new StackPanel{VerticalAlignment=VerticalAlignment.Center};word.Children.Add(new TextBlock{Text="ShaftMaster",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});word.Children.Add(new TextBlock{Text="DESIGN SOLUTIONS",FontSize=9.5,Foreground=Brushes.LightSkyBlue,Margin=new Thickness(0,4,0,0)});brand.Children.Add(word);
   Navigation=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Brushes.White,Padding=new Thickness(8,0,8,10),HorizontalContentAlignment=HorizontalAlignment.Stretch};
   foreach(var page in PageKeys)Navigation.Items.Add(new ListBoxItem{Content=DisplayText.PageLabel(page),Tag=page,Padding=new Thickness(13,9,10,9),Margin=new Thickness(0,page=="ABOUT"?14:1,0,1),FontSize=13,HorizontalContentAlignment=HorizontalAlignment.Stretch});
   Navigation.SelectionChanged+=(s,e)=>{if(selectingNavigation||Navigation.SelectedIndex<0)return;string next=PageKeys[Navigation.SelectedIndex];try{Commit();ShowPage(next);}catch(Exception ex){SetStatus(ex.Message);SelectNavigation(ActivePage);}};
   NavigationPanel.Children.Add(new ScrollViewer{Content=Navigation,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   MainPanel=new DockPanel{Margin=new Thickness(24,18,24,12),LastChildFill=true};Grid.SetColumn(MainPanel,1);LayoutRoot.Children.Add(MainPanel);
   Toolbar=new WrapPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(Toolbar,Dock.Top);MainPanel.Children.Add(Toolbar);
   ToolbarGroup("Project",new[]{Form.Button("New Project",Commands.New),Form.Button("Open…",Commands.Open),Form.Button("Save",()=>Commands.Save(false)),Form.Button("Save As…",()=>Commands.Save(true)),PresetEditing.FillAllButton()});
   ToolbarGroup("Workflow",new[]{Form.Button("Validate",Commands.ValidateAndShow),Form.Button("Generate Drawings",()=>Commands.StartGeneration(),true),Form.Button("Export STEP…",()=>Commands.StartStepExport()),Reports.CalculationSheetButton(),Reports.DesignNotesButton()});
   var header=new StackPanel{Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(header,Dock.Top);MainPanel.Children.Add(header);
   var projectRow=new Grid{Margin=new Thickness(0,0,0,8)};projectRow.ColumnDefinitions.Add(new ColumnDefinition());projectRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   ProjectName=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Ink,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};projectRow.Children.Add(ProjectName);
   DirtyLabel=new TextBlock{Text="Unsaved changes",FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(154,94,0)),Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(DirtyLabel,1);projectRow.Children.Add(DirtyLabel);header.Children.Add(projectRow);
   title=new TextBlock{FontSize=27,FontWeight=FontWeights.SemiBold};subtitle=new TextBlock{FontSize=12,Margin=new Thickness(0,5,0,0),Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap};header.Children.Add(title);header.Children.Add(subtitle);
   Form.Content=new StackPanel{MaxWidth=1100,HorizontalAlignment=HorizontalAlignment.Stretch};PageScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=Form.Content,Padding=new Thickness(0,0,6,0)};MainPanel.Children.Add(PageScroll);
   var footer=new StackPanel{Background=Brushes.White};Grid.SetColumn(footer,1);Grid.SetRow(footer,1);LayoutRoot.Children.Add(footer);
   busyIndicator=new ProgressBar{IsIndeterminate=true,Height=4,Visibility=Visibility.Collapsed};footer.Children.Add(busyIndicator);
   var statusRow=new Grid{Margin=new Thickness(18,8,18,8)};statusRow.ColumnDefinitions.Add(new ColumnDefinition());statusRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var statusPanel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};operationLabel=new TextBlock{Text="Working · project inputs are locked",FontSize=10,Foreground=Accent,Visibility=Visibility.Collapsed};statusPanel.Children.Add(operationLabel);
   StatusText=new TextBlock{Text="Ready · Create or open a project",FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Ink};statusPanel.Children.Add(StatusText);statusRow.Children.Add(statusPanel);
   var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(actions,1);statusRow.Children.Add(actions);
   OutputFolderButton=Form.Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(actualOutput)));OutputFolderButton.Visibility=Visibility.Collapsed;OutputFolderButton.FontSize=11;OutputFolderButton.Padding=new Thickness(10,5,10,5);actions.Children.Add(OutputFolderButton);
   var details=Form.Button("Details",ShowStatusDetails);details.FontSize=11;details.Padding=new Thickness(10,5,10,5);actions.Children.Add(details);footer.Children.Add(statusRow);
   PageScroll.SizeChanged+=(s,e)=>UpdateResponsiveLayout();Toolbar.SizeChanged+=(s,e)=>UpdateResponsiveLayout();
  }
  void ToolbarGroup(string name,IEnumerable<Button> buttons)
  {
   var group=new StackPanel();group.Children.Add(new TextBlock{Text=name,FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,6)});
   var actions=new WrapPanel();foreach(var b in buttons){b.FontSize=12;b.Padding=new Thickness(12,7,12,7);b.Margin=new Thickness(0,0,6,5);actions.Children.Add(b);}group.Children.Add(actions);
   var card=new Border{Child=group,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(225,231,238)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(10,8,4,3),Margin=new Thickness(0,0,10,6)};toolbarGroups.Add(card);Toolbar.Children.Add(card);
  }
  void SelectNavigation(string page)
  {
   selectingNavigation=true;try{Navigation.SelectedIndex=Array.IndexOf(PageKeys,page);foreach(ListBoxItem item in Navigation.Items){bool selected=Convert.ToString(item.Tag)==page;item.Background=selected?new SolidColorBrush(Color.FromRgb(0,104,180)):Brushes.Transparent;item.Foreground=selected?Brushes.White:new SolidColorBrush(Color.FromRgb(222,233,246));item.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;}}finally{selectingNavigation=false;}
  }
  internal void UpdateResponsiveLayout()
  {
   if(PageScroll==null)return;bool stacked=PageScroll.ActualWidth<630;
   foreach(var row in Form.FormRows){var label=(FrameworkElement)row.Children[0];var panel=(FrameworkElement)row.Children[1];row.ColumnDefinitions[0].Width=stacked?new GridLength(0):new GridLength(215);Grid.SetColumn(label,0);Grid.SetColumnSpan(label,stacked?2:1);label.Margin=stacked?new Thickness(0,0,0,5):new Thickness(0,7,18,0);Grid.SetRow(panel,stacked?1:0);Grid.SetColumn(panel,stacked?0:1);Grid.SetColumnSpan(panel,stacked?2:1);}
   foreach(var card in toolbarGroups)card.MaxWidth=Math.Max(220,Toolbar.ActualWidth-10);
   // Narrow windows: short label keeps the Workflow actions on one row; the full name stays in tooltip/automation.
   if(Reports.SheetButton!=null)Reports.SheetButton.Content=Toolbar.ActualWidth<620?"Calc Sheet…":"Export Calculation Sheet…";
  }
  internal void RefreshProjectStatus()
  {
   if(ProjectName==null)return;TextBox editor;string name=Form.Editors.TryGetValue("Info.Name",out editor)?editor.Text:Session.Project.Info.Name;
   ProjectName.Text=String.IsNullOrWhiteSpace(name)?"Untitled project":name;ProjectName.ToolTip=ProjectName.Text;DirtyLabel.Visibility=Session.Dirty?Visibility.Visible:Visibility.Collapsed;
   operationLabel.Visibility=Busy?Visibility.Visible:Visibility.Collapsed;OutputFolderButton.IsEnabled=!Busy&&!String.IsNullOrEmpty(actualOutput);
  }
  internal void SetStatus(string message)
  {
   DetailedStatus=DisplayText.Plain(message)??"Ready";string brief=DetailedStatus.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"Ready";
   if(brief.StartsWith("Project opened"))brief="Project opened.";else if(brief.StartsWith("Project saved"))brief="Project saved.";
   else{var drive=System.Text.RegularExpressions.Regex.Match(brief,@"[A-Za-z]:[\\/]");if(drive.Success)brief=brief.Substring(0,drive.Index).TrimEnd(' ','·','-');if(brief.Length>155)brief=brief.Substring(0,152)+"…";}
   StatusText.Text=brief;StatusText.ToolTip=DetailedStatus;RefreshProjectStatus();
  }
  internal void SetOutputLocation(string path){actualOutput=path;OutputFolderButton.Visibility=String.IsNullOrEmpty(path)?Visibility.Collapsed:Visibility.Visible;RefreshProjectStatus();}
  void ShowStatusDetails()
  {
   var dialog=new Window{Owner=this,Title="ShaftMaster — Operation details",FontFamily=FontFamily,FontSize=13,Icon=Icon,Background=Background};WindowLayout.Initial(dialog);dialog.Width=Math.Min(dialog.Width,760);dialog.Height=Math.Min(dialog.Height,450);
   dialog.Content=new TextBox{Text=DetailedStatus,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16),VerticalContentAlignment=VerticalAlignment.Top};dialog.ShowDialog();
  }
  void InitializeChromeEvents()
  {
   SourceInitialized+=(s,e)=>WindowLayout.Clamp(this);StateChanged+=(s,e)=>{if(WindowState==WindowState.Normal)Dispatcher.BeginInvoke(new Action(()=>WindowLayout.Clamp(this)));};
   SystemParameters.StaticPropertyChanged+=OnDisplayChanged;statusTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};statusTimer.Tick+=(s,e)=>RefreshProjectStatus();statusTimer.Start();
   statusTimer.Tick+=(s,e)=>Updates.CheckCurrency();
   Closed+=(s,e)=>{statusTimer.Stop();SystemParameters.StaticPropertyChanged-=OnDisplayChanged;};
  }
  void OnDisplayChanged(object sender,PropertyChangedEventArgs e){if(e.PropertyName=="WorkArea")Dispatcher.BeginInvoke(new Action(()=>WindowLayout.Clamp(this)));}
 }
}
