using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Automation;
using Elevator.Core.Project;
namespace Elevator.Solution.Desktop
{
 // DESIGN-FLOW stage 2 in the window: the preset flow (PresetFlow) and the editors that show it. Project fields follow
 // config/presets.json until the user edits them (lock); Reset unlocks.
 sealed class PresetEditing
 {
  readonly MainWindow shell;
  public PresetEditing(MainWindow shell){this.shell=shell;}
  // Writes the presets of every unlocked field and refreshes the visible editors (except the one being typed in).
  // Rope sizing runs the rope / traction checks, so typing skips it; Commit and page changes run it.
  public void Apply(string typing=null,bool sizeRopes=true)
  {
   if(!shell.Presets.Apply(shell.Session.Project,sizeRopes))return;
   foreach(var pair in shell.Form.ManagedRefreshers.ToList())if(pair.Key!=typing)pair.Value();
   RefreshPresetEditors();shell.General.RefreshPersons();
  }
  public void RefreshPresetEditors(){foreach(var a in shell.Form.PresetRefreshers.ToList())a();}
  public void Edited(PresetField f)
  {
   if(f==null)return;
   // Lock and follow only. The editor's own handler invalidates the results it affects; results whose inputs change
   // through a follower are caught by their input fingerprints (currency checks), so unrelated results stay current.
   shell.Presets.Edited(shell.Session,f);
   Apply(f.Key,false);
   shell.RefreshProjectStatus();
  }
  public ContextMenu ResetMenu(PresetField f)
  {
   var menu=new ContextMenu();var item=new MenuItem{Header="Reset to preset"};
   item.Click+=(s,e)=>shell.Guard(()=>{
    shell.Presets.Reset(shell.Session,f);shell.Live.Design=null;shell.Live.ClearDraft(f.Key);
    Apply();shell.Updates.InvalidateCalculation();shell.Updates.InvalidateRopeTraction();shell.Updates.InvalidateCarGuideForces();shell.RefreshProjectStatus();
    // Pages whose layout depends on the value (counts, Basic weights) are rebuilt.
    if(shell.ActivePage=="CALCULATIONS")shell.ShowPage("CALCULATIONS");
   });
   menu.Items.Add(item);return menu;
  }
  // The global action of the toolbar.
  public Button FillAllButton(){var b=shell.Form.Button("Fill blanks from presets",()=>FillBlanks(null));AutomationProperties.SetName(b,"Fill blanks from presets (all pages)");return b;}
  public void FillBlanks(string page)
  {
   shell.Presets.EnsureLibrary();
   shell.Commit();
   shell.Presets.FillBlanks(shell.Session,page);
   shell.Live.InputsReplaced();shell.Calculations.ResetPanels();
   shell.ShowPage(page??shell.ActivePage);shell.SetStatus(page==null?"Blank fields filled from presets.":"Blank fields on this page filled from presets.");
  }
 }
}
