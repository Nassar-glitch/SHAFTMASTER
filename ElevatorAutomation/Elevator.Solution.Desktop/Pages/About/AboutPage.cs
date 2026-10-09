using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // ABOUT: logo, version, system requirements and the supported workflow.
 sealed class AboutPage:PageView
 {
  readonly AboutViewModel vm=new AboutViewModel();
  public AboutPage(MainWindow shell):base(shell){}
  public override string Key{get{return vm.Key;}}
  public override void Build()
  {
   Content.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(0,0,0,14),Child=new Image{Source=BrandAssets.Image("ShaftMasterLogo.png"),Stretch=Stretch.Uniform,MaxWidth=640,MaxHeight=290,HorizontalAlignment=HorizontalAlignment.Left}});
   Heading(AboutViewModel.Product);Note(vm.VersionText);
   Heading("System requirements");Note(AboutViewModel.Requirements);
   Heading("Supported workflow");Note(AboutViewModel.Workflow);
  }
 }
}
