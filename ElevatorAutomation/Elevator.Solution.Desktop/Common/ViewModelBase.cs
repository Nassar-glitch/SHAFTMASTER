using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace Elevator.Solution.Desktop.Common
{
 // Base of the page view-models (INotifyPropertyChanged); no external MVVM package.
 abstract class ViewModelBase:INotifyPropertyChanged
 {
  public event PropertyChangedEventHandler PropertyChanged;
  protected void OnPropertyChanged([CallerMemberName]string name=null){var handler=PropertyChanged;if(handler!=null)handler(this,new PropertyChangedEventArgs(name));}
  // Sets the field and raises PropertyChanged when the value changed.
  protected bool SetProperty<T>(ref T field,T value,[CallerMemberName]string name=null)
  {
   if(EqualityComparer<T>.Default.Equals(field,value))return false;
   field=value;OnPropertyChanged(name);return true;
  }
 }
 // A page view-model: the navigation key and the page title.
 abstract class PageViewModel:ViewModelBase
 {
  protected PageViewModel(string key){Key=key;}
  public string Key {get;private set;}
  public string Title {get{return DisplayText.PageLabel(Key);}}
 }
}
