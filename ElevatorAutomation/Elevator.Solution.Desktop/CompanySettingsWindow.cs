using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Elevator.Core.Project;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;
namespace Elevator.Solution.Desktop
{
 // Company / report settings: company name and logo shared by all projects (stored under %AppData%\ShaftMaster).
 public sealed class CompanySettingsWindow:Window
 {
  readonly CompanyReportSettings settings;
  readonly TextBox company;readonly Image preview;readonly TextBlock logoInfo;
  string pendingLogo;bool removeLogo;
  public bool Saved {get;private set;}
  public TextBox CompanyBox {get{return company;}}
  public Image Preview {get{return preview;}}
  public CompanySettingsWindow(CompanyReportSettings current)
  {
   settings=current??new CompanyReportSettings();
   Title="Company / report settings";Width=520;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.White;
   var panel=new StackPanel{Margin=new Thickness(20)};
   panel.Children.Add(new TextBlock{Text="Shown on every exported calculation sheet when filled. Both are optional and are not stored in the project file.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,14)});
   panel.Children.Add(new TextBlock{Text="Company name",FontWeight=FontWeights.SemiBold});
   company=new TextBox{Text=settings.CompanyName??"",Margin=new Thickness(0,4,0,14),Padding=new Thickness(4)};System.Windows.Automation.AutomationProperties.SetName(company,"Company name");panel.Children.Add(company);
   panel.Children.Add(new TextBlock{Text="Company logo (PNG or JPG)",FontWeight=FontWeights.SemiBold});
   preview=new Image{Stretch=Stretch.Uniform,MaxHeight=90,MaxWidth=260,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,4)};panel.Children.Add(preview);
   logoInfo=new TextBlock{Foreground=Brushes.SlateGray,FontSize=11,Margin=new Thickness(0,0,0,8)};panel.Children.Add(logoInfo);
   var logoButtons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,16)};
   var browse=new Button{Content="Browse…",Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0)};var remove=new Button{Content="Remove",Padding=new Thickness(12,5,12,5)};
   browse.Click+=(s,e)=>{var dialog=new OpenFileDialog{Title="Choose company logo",Filter="Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(this)==true){try{LoadLogo(dialog.FileName);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Logo",MessageBoxButton.OK,MessageBoxImage.Warning);}}};
   remove.Click+=(s,e)=>RemoveLogo();
   logoButtons.Children.Add(browse);logoButtons.Children.Add(remove);panel.Children.Add(logoButtons);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   var save=new Button{Content="Save",Padding=new Thickness(14,6,14,6),Margin=new Thickness(0,0,8,0),IsDefault=true};var cancel=new Button{Content="Cancel",Padding=new Thickness(14,6,14,6),IsCancel=true};
   save.Click+=(s,e)=>{try{Accept();Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Company / report settings",MessageBoxButton.OK,MessageBoxImage.Warning);}};
   cancel.Click+=(s,e)=>Close();
   actions.Children.Add(save);actions.Children.Add(cancel);panel.Children.Add(actions);Content=panel;
   ShowLogo(settings.LogoPath);
  }
  void ShowLogo(string path)
  {
   preview.Source=null;logoInfo.Text="No logo";
   if(path==null||!File.Exists(path))return;
   var image=DocxImage.FromFile(path);var bitmap=new BitmapImage();
   bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=new MemoryStream(image.Data);bitmap.EndInit();bitmap.Freeze(); // no file lock
   preview.Source=bitmap;logoInfo.Text=image.PixelWidth+" × "+image.PixelHeight+" px · aspect ratio kept on the sheet";
  }
  // Validates and previews a picked file; it is copied into the settings folder on Save.
  public void LoadLogo(string path){DocxImage.FromFile(path);pendingLogo=path;removeLogo=false;ShowLogo(path);}
  public void RemoveLogo(){pendingLogo=null;removeLogo=true;ShowLogo(null);}
  public CompanyReportSettings Accept()
  {
   settings.CompanyName=String.IsNullOrWhiteSpace(company.Text)?null:company.Text.Trim();
   if(removeLogo)settings.RemoveLogo();
   if(pendingLogo!=null)settings.SetLogo(pendingLogo);
   settings.Save();Saved=true;return settings;
  }
 }
 public sealed partial class MainWindow
 {
  // General page: optional project-level sheet details. Editing them never invalidates calculations.
  void CalculationSheetDetails()
  {
   if(session.Project.Report==null)session.Project.Report=new ProjectReportDetails();
   Heading("Calculation sheet details");
   Field("Client","Report.ClientName");Field("Document number","Report.DocumentNumber");Field("Revision","Report.Revision");
   Field("Prepared by","Report.PreparedBy");Field("Checked by","Report.CheckedBy");Field("Approved by","Report.ApprovedBy");
   var company=CompanyReportSettings.Load();
   Note("Company (all projects): "+(company.CompanyName??"no name")+" · "+(company.LogoFile!=null?"logo set":"no logo"));
   content.Children.Add(Button("Company / report settings…",ShowCompanySettings));
  }
  void ShowCompanySettings()
  {
   var dialog=new CompanySettingsWindow(CompanyReportSettings.Load()){Owner=this,FontFamily=FontFamily,Foreground=ink};
   dialog.ShowDialog();
   if(dialog.Saved){SetStatus("Company / report settings saved.");if(activePage=="GENERAL"){Commit();ShowPage("GENERAL");}}
  }
 }
}
