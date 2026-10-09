using System.Collections.Generic;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>The list of plan bindings and the template identifier.</summary>
    public sealed class BindingFile
    {
        public int SchemaVersion { get; set; }
        public string TemplateId { get; set; }
        public string TemplateSha256 { get; set; }
        public int ExpectedEntityCount { get; set; }
        public List<Binding> Entities { get; set; }
    }
}
