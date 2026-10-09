using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Application;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop.Common
{
 // A part of the window built in code (a page or a panel of a page): access to the shell, the project session, the
 // application services and the form of the visible page.
 abstract class ViewPart
 {
  protected readonly MainWindow Shell;
  protected ViewPart(MainWindow shell){Shell=shell;}
  protected ProjectSession Session{get{return Shell.Session;}}
  protected DesignContext Context{get{return Shell.Context;}}
  protected PresetFlow Presets{get{return Shell.Presets;}}
  protected LiveCalculation Live{get{return Shell.Live;}}
  protected PageForm Form{get{return Shell.Form;}}
  protected StackPanel Content{get{return Form.Content;}set{Form.Content=value;}}
  protected Brush Ink{get{return Shell.Ink;}}
  protected Brush Accent{get{return Shell.Accent;}}
  protected void Note(string text){Form.Note(text);}
  protected void Heading(string text){Form.Heading(text);}
 }
 // A navigation page: Build adds its controls to the cleared form of the window.
 abstract class PageView:ViewPart
 {
  protected PageView(MainWindow shell):base(shell){}
  public abstract string Key {get;}
  public abstract void Build();
 }
}
