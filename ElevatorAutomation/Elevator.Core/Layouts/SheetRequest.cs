using System;
using System.Collections.Generic;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// What the AutoCAD host needs to add the A0 geared sheet to a generated drawing (L2b): the values of the title block (from
    /// <see cref="TitleBlockData.Attributes"/>), the company logo and the door type that chooses the reference door detail. A request
    /// without a sheet generates the four views exactly as before.
    /// </summary>
    public sealed class SheetRequest
    {
        /// <summary>The layout name in the generated drawing (the template's layout).</summary>
        public string LayoutName = SheetLayout.Name;
        /// <summary>The template DXF; null or empty: Templates/Layout_Geared_A0.dxf of the installation.</summary>
        public string TemplateFile;
        /// <summary>The logo image (PNG or JPEG), its full path; null: none. The drawing holds a link to the file.</summary>
        public string LogoFile;
        /// <summary>The logo size in pixels (the aspect ratio is kept inside the logo area).</summary>
        public int LogoWidthPx, LogoHeightPx;
        /// <summary>The project's door type (DoorTypes): Manual shows the swing-door reference, an automatic door the automatic one.</summary>
        public string DoorType;
        /// <summary>The attribute values of SM_TITLEBLOCK.</summary>
        public List<TitleBlockAttribute> Attributes = new List<TitleBlockAttribute>();
    }

    /// <summary>The names of the geared template.</summary>
    public static class SheetLayout
    {
        /// <summary>The layout of Templates/Layout_Geared_A0.dxf.</summary>
        public const string Name = "SM_Geared_A0";
        /// <summary>The title-block block of the template.</summary>
        public const string TitleBlock = "SM_TITLEBLOCK";
        /// <summary>The layer of the logo rectangle.</summary>
        public const string LogoLayer = "SM_LOGO_AREA";
        /// <summary>The layer of the viewport frames (not plotted).</summary>
        public const string ViewportLayer = "view port";
        /// <summary>The scale label height under each view, paper mm.</summary>
        public const double LabelHeight = 3.5;
        /// <summary>The margin around a generated view's extents, model mm (the dimensions are already inside the extents).</summary>
        public const double ViewMarginMm = 100;

        /// <summary>The margin (model mm) around the drawn extents of a reference detail: it has no dimensions outside them.</summary>
        public const double DetailMarginMm = 30;
        /// <summary>The printed height of every title on the sheet (paper mm), views and details alike, one line each.</summary>
        public const double TitlePaperHeightMm = 5;
        /// <summary>
        /// The largest model height of a title: 5 mm on paper up to 1:160 (800 mm). A title at 1:200 would be 1000 mm high and about 22 m
        /// wide, wider than the section and than the gap to the neighbouring views of the drawing, which the host rejects (views must
        /// not overlap); there it stays at 800 mm.
        /// </summary>
        public const double TitleMaxModelMm = 800;

        /// <summary>The model height of a title at the sheet scale 1:<paramref name="scaleDenominator"/>: 5 mm on paper (at most 800 model mm).</summary>
        public static double TitleModelHeight(int scaleDenominator)
        {
            return System.Math.Min(TitlePaperHeightMm * scaleDenominator, TitleMaxModelMm);
        }
        /// <summary>The scales a reference detail may take: its template scale or larger (the bracket detail was enlarged to 1:5).</summary>
        public static readonly int[] DetailScales = { 5, 10, 20 };
        public const string TemplateFileName = "Layout_Geared_A0.dxf";
        public const string DetailsFileName = "Layout_Geared_A0.details.json";
        public const string FieldsFileName = "Layout_Geared_A0.fields.json";
        /// <summary>The narrowest width factor an attribute takes before its height is reduced.</summary>
        public const double MinWidthFactor = 0.7;
        /// <summary>The smallest height of a shrunk attribute, as a share of its nominal height, before it takes two lines.</summary>
        public const double MinHeightShare = 0.55;

        /// <summary>The reference details shown for a project (Templates/Layout_Geared_A0.details.json), in sheet order.</summary>
        public static string[] ReferenceDetails(string doorType)
        {
            bool manual = String.IsNullOrWhiteSpace(doorType) || String.Equals(doorType, "Manual", StringComparison.OrdinalIgnoreCase);
            // Rails are the fixed ladder values CAR T70/65/9 and CW T50/50/5 (ElevatorDesignResult), so the CW 5 / car 9 details.
            return new[] { "FISH_PLATE", "RAILS_PLAN_CW5_CAR9", "RAIL_BRACKETS_CW5_CAR9", manual ? "DOOR_SWING" : "DOOR_AUTOMATIC" };
        }
    }

    /// <summary>One viewport of the assembled sheet, as written into the drawing.</summary>
    public sealed class SheetViewportInfo
    {
        public string Name;
        public string Kind;
        public double PaperX, PaperY, PaperWidth, PaperHeight;
        public int ScaleDenominator;
        public string Label;
        public double ModelCenterX, ModelCenterY;
        /// <summary>True when the scale label is drawn under the viewport (the view has no title of its own that states its scale).</summary>
        public bool HasLabel;
        /// <summary>The titles in the view's own drawing that carry its scale, as written ("SHAFT AND CAR 1:10"); empty when none.</summary>
        public List<string> Titles = new List<string>();
    }

    /// <summary>How an attribute of the title block fits its box (L2b fix round).</summary>
    public sealed class SheetAttributeFit
    {
        public string Tag;
        /// <summary>The text written into the attribute.</summary>
        public string Text;
        public double AvailableWidthMm;
        /// <summary>The widest line of the text as drawn (mm).</summary>
        public double WidthMm;
        public double WidthFactor;
        public double HeightMm;
        public double NominalHeightMm;
        public int Lines;
        public bool Fits;
    }

    /// <summary>What the host reports of the sheet it added.</summary>
    public sealed class SheetResult
    {
        public string LayoutName;
        public double DrawingAreaX, DrawingAreaY, DrawingAreaWidth, DrawingAreaHeight;
        public List<SheetViewportInfo> Viewports = new List<SheetViewportInfo>();
        public double FillRatio;
        public bool LogoPlaced;
        public string LogoMessage;
        /// <summary>The logo area of the template on the paper (mm from the paper's lower left corner): the PDF step puts the image there.</summary>
        public double LogoAreaX, LogoAreaY, LogoAreaWidth, LogoAreaHeight;
        public List<string> NotPlaced = new List<string>();
        public List<SheetAttributeFit> Attributes = new List<SheetAttributeFit>();
    }
}

namespace Elevator.Core.Layouts
{
    /// <summary>One reference detail of Templates/Layout_Geared_A0.details.json: its model-space window in the template.</summary>
    public sealed class SheetDetailSpec
    {
        public string Name;
        public string Kind;
        public int TemplateScale;
        public int Entities;
        /// <summary>minX, minY, maxX, maxY in model mm.</summary>
        public double[] Window;
    }

    /// <summary>Templates/Layout_Geared_A0.details.json.</summary>
    public sealed class SheetDetailsFile
    {
        public double[] Origin;
        public double PaperScaleFromSourceUnits;
        public System.Collections.Generic.List<SheetDetailSpec> Details = new System.Collections.Generic.List<SheetDetailSpec>();
    }
}

namespace Elevator.Core.Layouts
{
    /// <summary>The room of one title-block attribute (Templates/Layout_Geared_A0.fields.json, written by tools/layouts/build_template.py).</summary>
    public sealed class SheetFieldSpec
    {
        public string Tag;
        public string Style;
        public double HeightMm;
        /// <summary>"Left" (top-left anchored) or "Center".</summary>
        public string Justify;
        public double[] AnchorMm;
        /// <summary>The free width along the text band, from the static geometry around the anchor.</summary>
        public double AvailableWidthMm;
        public int MaxLines;
        /// <summary>The longest realistic value of the tag, for the box check.</summary>
        public string LongestValue;
    }

    public sealed class SheetFieldsFile
    {
        public System.Collections.Generic.List<SheetFieldSpec> Fields = new System.Collections.Generic.List<SheetFieldSpec>();
    }
}
