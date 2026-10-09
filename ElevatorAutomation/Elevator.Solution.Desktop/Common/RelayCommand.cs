using System;
using System.Windows.Input;
namespace Elevator.Solution.Desktop.Common
{
 // ICommand over delegates (no external MVVM package).
 sealed class RelayCommand:ICommand
 {
  readonly Action<object> execute;readonly Func<object,bool> canExecute;
  public RelayCommand(Action execute,Func<bool> canExecute=null):this(p=>execute(),canExecute==null?(Func<object,bool>)null:p=>canExecute()){}
  public RelayCommand(Action<object> execute,Func<object,bool> canExecute=null)
  {
   if(execute==null)throw new ArgumentNullException("execute");
   this.execute=execute;this.canExecute=canExecute;
  }
  public bool CanExecute(object parameter){return canExecute==null||canExecute(parameter);}
  public void Execute(object parameter){execute(parameter);}
  public event EventHandler CanExecuteChanged{add{CommandManager.RequerySuggested+=value;}remove{CommandManager.RequerySuggested-=value;}}
 }
}
