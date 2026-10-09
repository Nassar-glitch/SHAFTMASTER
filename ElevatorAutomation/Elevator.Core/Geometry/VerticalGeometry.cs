using System.Collections.Generic;
using Elevator.Core.GuideRails;

namespace Elevator.Core.Geometry
{
    /// <summary>Generated geometry of the vertical section: levels, bracket layout and drawing primitives.</summary>
    public sealed class VerticalGeometry
    {
        public List<VerticalPrimitive> Entities = new List<VerticalPrimitive>();
        public List<string> ReviewItems = new List<string>();
        public List<VerticalFloor> Floors;
        public BracketLayout Brackets;
        public double PitFloorZ;
        public double HighestServedZ;
        public double LowestServedZ;
        public double TravelMm;
        public double MRSlabUndersideZ;
        public double RoofZ;
        public double CeilingZ;
        public double CarFloorZ;
        public double CarBottomZ;
        public double CarTopZ;
        public double CwBottomZ;
        public double CwTopZ;
        public string CWAbsolutePositionStatus;
        public string LayoutStatus = "PASS";
        public string CodeDiameterRatioStatus;

        /// <summary>Adds a drawing primitive.</summary>
        public void Add(string id, string kind, string layer, string provenance, string text, params double[] points)
        {
            Entities.Add(new VerticalPrimitive
            {
                Id = "VerticalExperiment." + id,
                Kind = kind,
                Layer = layer,
                Provenance = provenance,
                Text = text,
                Points = points
            });
        }

        /// <summary>Adds a line.</summary>
        public void Line(string id, string layer, double x, double z, double x2, double z2)
        {
            Add(id, "Line", layer, "PROJECT_DERIVED", null, x, z, x2, z2);
        }

        /// <summary>Adds a rectangle.</summary>
        public void Box(string id, string layer, double x, double z, double x2, double z2)
        {
            Add(id, "Box", layer, "PROJECT_DERIVED", null, x, z, x2, z2);
        }

        /// <summary>Adds a text note.</summary>
        public void Note(string id, string text, double x, double z)
        {
            Add(id, "Text", "Annotation", "DRAWING_PRESENTATION", text, x, z);
        }
    }
}
