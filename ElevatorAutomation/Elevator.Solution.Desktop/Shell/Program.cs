using System;
using System.Windows;
namespace Elevator.Solution.Desktop
{
    public sealed class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            var app=new Application();var window=new MainWindow(args);app.Run(window);return window.ExitCode;
        }
    }
}
