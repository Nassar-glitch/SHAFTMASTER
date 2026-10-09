using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.ComponentModel;
using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;
using Elevator.Core.Application;
namespace Elevator.Solution.Desktop
{
 public sealed partial class MainWindow
 {
  DockPanel mainPanel,navigationPanel;ScrollViewer pageScroll;WrapPanel toolbar;
  TextBlock projectName,dirtyLabel,operationLabel;Button outputFolderButton;
  string detailedStatus="Ready",actualOutput;bool selectingNavigation;DispatcherTimer statusTimer;
  readonly List<Grid> formRows=new List<Grid>();readonly List<FrameworkElement> toolbarGroups=new List<FrameworkElement>();
  void ConfigureStyles()
  {
   var style=new Style(typeof(TextBox));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,6,10,6)));style.Setters.Add(new Setter(Control.MinHeightProperty,34.0));style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));style.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(193,205,218))));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.White));
   var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Control.BorderBrushProperty,accent));focus.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(2)));style.Triggers.Add(focus);Resources.Add(typeof(TextBox),style);
   var dataStyle=new Style(typeof(DataGrid));dataStyle.Setters.Add(new Setter(Control.FontSizeProperty,13.0));dataStyle.Setters.Add(new Setter(DataGrid.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Auto));dataStyle.Setters.Add(new Setter(DataGrid.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto));Resources.Add(typeof(DataGrid),dataStyle);
  }
  void BuildShaftMasterShell()
  {
   shell=new Grid{Background=Background};shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});shell.ColumnDefinitions.Add(new ColumnDefinition());shell.RowDefinitions.Add(new RowDefinition());shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=shell;
   navigationPanel=new DockPanel{Background=ink,LastChildFill=true};Grid.SetRowSpan(navigationPanel,2);shell.Children.Add(navigationPanel);
   var brand=new StackPanel{Margin=new Thickness(16,19,12,20),Orientation=Orientation.Horizontal};DockPanel.SetDock(brand,Dock.Top);navigationPanel.Children.Add(brand);
   brand.Children.Add(new Border{Width=34,Height=48,Background=Brushes.White,CornerRadius=new CornerRadius(4),Padding=new Thickness(3),Margin=new Thickness(0,0,8,0),Child=new Image{Source=BrandAssets.Image("ShaftMasterMark.png"),Stretch=Stretch.Uniform}});
   var word=new StackPanel{VerticalAlignment=VerticalAlignment.Center};word.Children.Add(new TextBlock{Text="ShaftMaster",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});word.Children.Add(new TextBlock{Text="DESIGN SOLUTIONS",FontSize=9.5,Foreground=Brushes.LightSkyBlue,Margin=new Thickness(0,4,0,0)});brand.Children.Add(word);
   navigation=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Brushes.White,Padding=new Thickness(8,0,8,10),HorizontalContentAlignment=HorizontalAlignment.Stretch};
   foreach(var page in pages)navigation.Items.Add(new ListBoxItem{Content=PageLabel(page),Tag=page,Padding=new Thickness(13,9,10,9),Margin=new Thickness(0,page=="ABOUT"?14:1,0,1),FontSize=13,HorizontalContentAlignment=HorizontalAlignment.Stretch});
   navigation.SelectionChanged+=(s,e)=>{if(selectingNavigation||navigation.SelectedIndex<0)return;string next=pages[navigation.SelectedIndex];try{Commit();ShowPage(next);}catch(Exception ex){SetStatus(ex.Message);SelectNavigation(activePage);}};
   navigationPanel.Children.Add(new ScrollViewer{Content=navigation,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   mainPanel=new DockPanel{Margin=new Thickness(24,18,24,12),LastChildFill=true};Grid.SetColumn(mainPanel,1);shell.Children.Add(mainPanel);
   toolbar=new WrapPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(toolbar,Dock.Top);mainPanel.Children.Add(toolbar);
   ToolbarGroup("Project",new[]{Button("New Project",()=>{if(ConfirmDiscard()){session=new ProjectSession(NewProject());ResetCalculations();design=null;last=null;lastStep=null;SetOutputLocation(null);ShowPage("GENERAL");SetStatus("New project ready.");}}),Button("Open…",Open),Button("Save",()=>Save(false)),Button("Save As…",()=>Save(true)),FillAllButton()});
   ToolbarGroup("Workflow",new[]{Button("Validate",()=>{Commit();Calculate();ShowPage("VALIDATION");}),Button("Generate Drawings",()=>StartGeneration(),true),Button("Export STEP…",()=>StartStepExport()),CalculationSheetButton(),DesignNotesButton()});
   var header=new StackPanel{Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(header,Dock.Top);mainPanel.Children.Add(header);
   var projectRow=new Grid{Margin=new Thickness(0,0,0,8)};projectRow.ColumnDefinitions.Add(new ColumnDefinition());projectRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   projectName=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=ink,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};projectRow.Children.Add(projectName);
   dirtyLabel=new TextBlock{Text="Unsaved changes",FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(154,94,0)),Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(dirtyLabel,1);projectRow.Children.Add(dirtyLabel);header.Children.Add(projectRow);
   title=new TextBlock{FontSize=27,FontWeight=FontWeights.SemiBold};subtitle=new TextBlock{FontSize=12,Margin=new Thickness(0,5,0,0),Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap};header.Children.Add(title);header.Children.Add(subtitle);
   content=new StackPanel{MaxWidth=1100,HorizontalAlignment=HorizontalAlignment.Stretch};pageScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=content,Padding=new Thickness(0,0,6,0)};mainPanel.Children.Add(pageScroll);
   var footer=new StackPanel{Background=Brushes.White};Grid.SetColumn(footer,1);Grid.SetRow(footer,1);shell.Children.Add(footer);
   busyIndicator=new ProgressBar{IsIndeterminate=true,Height=4,Visibility=Visibility.Collapsed};footer.Children.Add(busyIndicator);
   var statusRow=new Grid{Margin=new Thickness(18,8,18,8)};statusRow.ColumnDefinitions.Add(new ColumnDefinition());statusRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var statusPanel=new StackPanel{VerticalAlignment=VerticalAlignment.Center};operationLabel=new TextBlock{Text="Working · project inputs are locked",FontSize=10,Foreground=accent,Visibility=Visibility.Collapsed};statusPanel.Children.Add(operationLabel);
   status=new TextBlock{Text="Ready · Create or open a project",FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=ink};statusPanel.Children.Add(status);statusRow.Children.Add(statusPanel);
   var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(actions,1);statusRow.Children.Add(actions);
   outputFolderButton=Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(actualOutput)));outputFolderButton.Visibility=Visibility.Collapsed;outputFolderButton.FontSize=11;outputFolderButton.Padding=new Thickness(10,5,10,5);actions.Children.Add(outputFolderButton);
   var details=Button("Details",ShowStatusDetails);details.FontSize=11;details.Padding=new Thickness(10,5,10,5);actions.Children.Add(details);footer.Children.Add(statusRow);
   pageScroll.SizeChanged+=(s,e)=>UpdateResponsiveLayout();toolbar.SizeChanged+=(s,e)=>UpdateResponsiveLayout();
  }
  void ToolbarGroup(string name,IEnumerable<Button> buttons)
  {
   var group=new StackPanel();group.Children.Add(new TextBlock{Text=name,FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,6)});
   var actions=new WrapPanel();foreach(var b in buttons){b.FontSize=12;b.Padding=new Thickness(12,7,12,7);b.Margin=new Thickness(0,0,6,5);actions.Children.Add(b);}group.Children.Add(actions);
   var card=new Border{Child=group,Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(225,231,238)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(10,8,4,3),Margin=new Thickness(0,0,10,6)};toolbarGroups.Add(card);toolbar.Children.Add(card);
  }
  static string PageLabel(string page){return page=="CAR & DOORS"?"Car & Doors":System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(page.ToLowerInvariant());}
  void SelectNavigation(string page)
  {
   selectingNavigation=true;try{navigation.SelectedIndex=Array.IndexOf(pages,page);foreach(ListBoxItem item in navigation.Items){bool selected=Convert.ToString(item.Tag)==page;item.Background=selected?new SolidColorBrush(Color.FromRgb(0,104,180)):Brushes.Transparent;item.Foreground=selected?Brushes.White:new SolidColorBrush(Color.FromRgb(222,233,246));item.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;}}finally{selectingNavigation=false;}
  }
  Grid FormRow(string label,StackPanel panel)
  {
   var row=new Grid{Margin=new Thickness(0,0,0,15)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(215)});row.ColumnDefinitions.Add(new ColumnDefinition());row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   row.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,7,18,0),FontSize=13});row.Children.Add(panel);Grid.SetColumn(panel,1);formRows.Add(row);row.Loaded+=(s,e)=>UpdateResponsiveLayout();return row;
  }
  void UpdateResponsiveLayout()
  {
   if(pageScroll==null)return;bool stacked=pageScroll.ActualWidth<630;
   foreach(var row in formRows){var label=(FrameworkElement)row.Children[0];var panel=(FrameworkElement)row.Children[1];row.ColumnDefinitions[0].Width=stacked?new GridLength(0):new GridLength(215);Grid.SetColumn(label,0);Grid.SetColumnSpan(label,stacked?2:1);label.Margin=stacked?new Thickness(0,0,0,5):new Thickness(0,7,18,0);Grid.SetRow(panel,stacked?1:0);Grid.SetColumn(panel,stacked?0:1);Grid.SetColumnSpan(panel,stacked?2:1);}
   foreach(var card in toolbarGroups)card.MaxWidth=Math.Max(220,toolbar.ActualWidth-10);
   // Narrow windows: short label keeps the Workflow actions on one row; the full name stays in tooltip/automation.
   if(calculationSheetButton!=null)calculationSheetButton.Content=toolbar.ActualWidth<620?"Calc Sheet…":"Export Calculation Sheet…";
  }
  static string DisplayValue(string path,object value)
  {
   if(path=="Elevator.SystemId"&&Convert.ToString(value)=="traction-geared")return "Geared Traction";
   if(path=="Suspension.RopingRatio"&&value!=null&&Convert.ToDouble(value)==1)return "1:1";
   return value==null?"":Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
  }
  void ProjectDetails()
  {
   var box=new StackPanel{Margin=new Thickness(0,8,0,10)};box.Children.Add(new TextBlock{Text="Project identifier",FontSize=12,Foreground=Brushes.SlateGray});
   box.Children.Add(new TextBox{Text=session.Project.Info.ProjectId,IsReadOnly=true,Background=new SolidColorBrush(Color.FromRgb(232,238,245)),Margin=new Thickness(0,5,0,8)});box.Children.Add(Button("Copy identifier",()=>{Clipboard.SetText(session.Project.Info.ProjectId);SetStatus("Project identifier copied.");}));
   content.Children.Add(new Expander{Header="Project details",Content=box,Margin=new Thickness(0,0,0,12)});
  }
  void About()
  {
   content.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(0,0,0,14),Child=new Image{Source=BrandAssets.Image("ShaftMasterLogo.png"),Stretch=Stretch.Uniform,MaxWidth=640,MaxHeight=290,HorizontalAlignment=HorizontalAlignment.Left}});
   Heading("ShaftMaster — Elevator Design Solutions");Note("Version "+typeof(Program).Assembly.GetName().Version.ToString(3));
   Heading("System requirements");Note("Windows x64 and .NET Framework 4.8.\nDWG generation: AutoCAD 2023 and the existing reviewed drawing templates.\nSTEP export: installed, licensed SOLIDWORKS with a configured English-named default part template; verified with SOLIDWORKS 2022. Keep the packaged application files together.");
   Heading("Supported workflow");Note("Geared Traction · 1:1 suspension · Elevator Plan, Machine Room Plan, Section A-A and Section B-B. ");
  }
  void RefreshProjectStatus()
  {
   if(projectName==null)return;TextBox editor;string name=editors.TryGetValue("Info.Name",out editor)?editor.Text:session.Project.Info.Name;
   projectName.Text=String.IsNullOrWhiteSpace(name)?"Untitled project":name;projectName.ToolTip=projectName.Text;dirtyLabel.Visibility=session.Dirty?Visibility.Visible:Visibility.Collapsed;
   operationLabel.Visibility=busy?Visibility.Visible:Visibility.Collapsed;outputFolderButton.IsEnabled=!busy&&!String.IsNullOrEmpty(actualOutput);
  }
  void SetStatus(string message)
  {
   detailedStatus=Plain(message)??"Ready";string brief=detailedStatus.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"Ready";
   if(brief.StartsWith("Project opened"))brief="Project opened.";else if(brief.StartsWith("Project saved"))brief="Project saved.";
   else{var drive=System.Text.RegularExpressions.Regex.Match(brief,@"[A-Za-z]:[\\/]");if(drive.Success)brief=brief.Substring(0,drive.Index).TrimEnd(' ','·','-');if(brief.Length>155)brief=brief.Substring(0,152)+"…";}
   status.Text=brief;status.ToolTip=detailedStatus;RefreshProjectStatus();
  }
  void SetOutputLocation(string path){actualOutput=path;outputFolderButton.Visibility=String.IsNullOrEmpty(path)?Visibility.Collapsed:Visibility.Visible;RefreshProjectStatus();}
  void ShowStatusDetails()
  {
   var dialog=new Window{Owner=this,Title="ShaftMaster — Operation details",FontFamily=FontFamily,FontSize=13,Icon=Icon,Background=Background};WindowLayout.Initial(dialog);dialog.Width=Math.Min(dialog.Width,760);dialog.Height=Math.Min(dialog.Height,450);
   dialog.Content=new TextBox{Text=detailedStatus,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16),VerticalContentAlignment=VerticalAlignment.Top};dialog.ShowDialog();
  }
  void OutputCard(string label,string output,string source)
  {
   var panel=new StackPanel{Margin=new Thickness(0,0,0,15)};panel.Children.Add(new TextBlock{Text=label+" · "+Path.GetFileName(output),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
   var details=new StackPanel();details.Children.Add(new TextBox{Text=output,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)});if(source!=null)details.Children.Add(new TextBox{Text="Editable source: "+source,IsReadOnly=true,TextWrapping=TextWrapping.Wrap});panel.Children.Add(new Expander{Header="Output details",Content=details});
   panel.Children.Add(Button("Open Output Folder",()=>System.Diagnostics.Process.Start("explorer.exe",Path.GetDirectoryName(output))));content.Children.Add(panel);
  }
  void InitializeChromeEvents()
  {
   SourceInitialized+=(s,e)=>WindowLayout.Clamp(this);StateChanged+=(s,e)=>{if(WindowState==WindowState.Normal)Dispatcher.BeginInvoke(new Action(()=>WindowLayout.Clamp(this)));};
   SystemParameters.StaticPropertyChanged+=OnDisplayChanged;statusTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};statusTimer.Tick+=(s,e)=>RefreshProjectStatus();statusTimer.Start();
   statusTimer.Tick+=(s,e)=>CheckCalculationCurrency();
   Closed+=(s,e)=>{statusTimer.Stop();SystemParameters.StaticPropertyChanged-=OnDisplayChanged;};
  }
  void OnDisplayChanged(object sender,PropertyChangedEventArgs e){if(e.PropertyName=="WorkArea")Dispatcher.BeginInvoke(new Action(()=>WindowLayout.Clamp(this)));}
 }
}
