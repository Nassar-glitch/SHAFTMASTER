using System;
using System.IO;
using Elevator.Core.Project;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 sealed class DrawingsViewModel:PageViewModel
 {
  // OutputName stays empty: the output set (DWG, PDF, project file) is named automatically from the project (DrawingFileName).
  string outputFolder,outputName="";
  public DrawingsViewModel(string outputFolder):base("DRAWINGS"){this.outputFolder=outputFolder;}
  // Where Generate writes the coordinated DWG.
  public string OutputFolder{get{return outputFolder;}set{SetProperty(ref outputFolder,value);}}
  public string OutputName{get{return outputName;}set{SetProperty(ref outputName,value);}}
  // The four drawing views (flag value, name).
  public static readonly Choice[] Views={new Choice{Id="1",Name="Elevator Plan"},new Choice{Id="2",Name="Machine Room Plan"},new Choice{Id="4",Name="Section A-A"},new Choice{Id="8",Name="Section B-B"}};
  public static DrawingViews Flag(Choice view){return (DrawingViews)Int32.Parse(view.Id);}
  // Commit of the output fields: a filename without directories.
  public void SetOutput(string folder,string name)
  {
   OutputFolder=folder;OutputName=name;if(!String.IsNullOrWhiteSpace(OutputName)&&Path.GetFileName(OutputName)!=OutputName)throw new ArgumentException("Enter a filename without directories.");
  }
 }
}
