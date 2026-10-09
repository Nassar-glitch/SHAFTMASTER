using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using ElevatorPlanGenerator;
public sealed class PlotPreview
{
    [CommandMethod("ELEVATORPLOTPREVIEW")]
    public void Plot()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        string output=Environment.GetEnvironmentVariable("ELEVATOR_PREVIEW_PDF");
        if(String.IsNullOrEmpty(output))output=Path.Combine(Configuration.Root,"tests","generated-preview.pdf");
        object background=Application.GetSystemVariable("BACKGROUNDPLOT");
        try {
            Application.SetSystemVariable("BACKGROUNDPLOT",0);
            using(var tr=doc.Database.TransactionManager.StartTransaction()) {
                ObjectId layoutId=LayoutManager.Current.GetLayoutId("Model");var layout=(Autodesk.AutoCAD.DatabaseServices.Layout)tr.GetObject(layoutId,OpenMode.ForRead);
                using(var settings=new PlotSettings(layout.ModelType)) {
                    settings.CopyFrom(layout);var validator=PlotSettingsValidator.Current;
                    validator.SetPlotConfigurationName(settings,"DWG To PDF.pc3",null);validator.RefreshLists(settings);
                    string media=null;foreach(string name in validator.GetCanonicalMediaNameList(settings))if(name.Contains("ISO_A3")&&name.Contains("297.00")&&name.Contains("420.00")){media=name;break;}
                    if(media==null)foreach(string name in validator.GetCanonicalMediaNameList(settings))if(name.Contains("A3")){media=name;break;}
                    if(media!=null)validator.SetCanonicalMediaName(settings,media);
                    validator.SetPlotPaperUnits(settings,PlotPaperUnit.Millimeters);
                    validator.SetPlotRotation(settings,PlotRotation.Degrees000);
                    validator.SetPlotType(settings,Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
                    validator.SetUseStandardScale(settings,true);validator.SetStdScaleType(settings,StdScaleType.ScaleToFit);validator.SetPlotCentered(settings,true);
                    settings.PrintLineweights=true;settings.PlotPlotStyles=true;
                    using(var info=new PlotInfo()) {
                        info.Layout=layoutId;info.OverrideSettings=settings;
                        using(var piValidator=new PlotInfoValidator()) {piValidator.MediaMatchingPolicy=MatchingPolicy.MatchEnabled;piValidator.Validate(info);}
                        using(var engine=PlotFactory.CreatePublishEngine()) {
                            engine.BeginPlot(null,null);engine.BeginDocument(info,doc.Name,null,1,true,output);
                            using(var page=new PlotPageInfo()) {engine.BeginPage(page,info,true,null);engine.BeginGenerateGraphics(null);engine.EndGenerateGraphics(null);engine.EndPage(null);}
                            engine.EndDocument(null);engine.EndPlot(null);
                        }
                    }
                }
            }
            doc.Editor.WriteMessage("\nPreview plotted: "+output+"\n");
        } finally {Application.SetSystemVariable("BACKGROUNDPLOT",background);}
    }
}
