using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Plan text annotations: preparation, update and validation.</summary>
    public static class PlanAnnotations
    {
        /// <summary>Key of the plan title annotation.</summary>
        public const string TitleId = "Plan.CenterFront.Part013";

        /// <summary>The text of an annotation entity.</summary>
        public static string Text(Entity entity)
        {
            var text = entity as MText;
            if (text != null)
                return text.Contents;
            var leader = entity as MLeader;
            if (leader != null && leader.ContentType == ContentType.MTextContent)
            {
                using (var content = leader.MText)
                {
                    if (content != null)
                        return content.Contents;
                }
            }
            throw new InvalidOperationException("Annotation binding requires an MText or an MText-content multileader.");
        }

        /// <summary>Escapes a value for AutoCAD text.</summary>
        public static string Escape(string value)
        {
            return value.Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r\n", "\\P")
                .Replace("\n", "\\P")
                .Replace("\r", "\\P")
                .Replace("%<", "% <");
        }

        /// <summary>Reads the annotation templates from the drawing.</summary>
        public static Dictionary<string, string> Prepare(
            Database db,
            Transaction tr,
            DrawingSnapshot snapshot,
            ProjectInput input,
            ElevatorParameters design,
            Dictionary<string, double> gaps)
        {
            var values = PlanAnnotationRules.Values(input, design, gaps);
            var result = new Dictionary<string, string>();
            foreach (var binding in snapshot.Bindings.Values)
            {
                string template = binding.AnnotationTemplate;
                if (template == null && binding.Key == TitleId && !String.IsNullOrWhiteSpace(input.PlanTitle))
                {
                    string existing = Text(snapshot.Entities[binding.Key]);
                    if (!existing.Contains("SHAFT AND CAR"))
                    {
                        throw new InvalidOperationException(
                                                    "The title was edited. Use ELEVATORANNOTATION to bind its text explicitly before "
                                                    + "setting PlanTitle.");
                    }
                    template = existing.Replace("SHAFT AND CAR", "{{PlanTitle}}");
                    binding.AnnotationTemplate = template;
                    // Additive binding metadata; same semantic identity, no handle dependency.
                    var manifest = ComponentBinding.ReadManifest(db, tr);
                    manifest.Entities.Find(b => b.Key == binding.Key).AnnotationTemplate = template;
                    ProjectDataStorage.Write(db, tr, "PlanBindings", manifest);
                }
                if (template == null)
                    continue;
                Text(snapshot.Entities[binding.Key]); // Reject incompatible content before geometry is changed.
                result[binding.Key] = PlanAnnotationRules.Render(template, values, Escape);
            }
            return result;
        }

        /// <summary>Writes the annotation values.</summary>
        public static void Apply(DrawingSnapshot snapshot, Dictionary<string, string> contents)
        {
            foreach (var pair in contents)
            {
                var entity = snapshot.Entities[pair.Key];
                entity.UpgradeOpen();
                var text = entity as MText;
                if (text != null)
                    text.Contents = pair.Value;
                else
                {
                    var leader = (MLeader)entity;
                    using (var content = leader.MText)
                    {
                        content.Contents = pair.Value;
                        leader.MText = content;
                    }
                }
            }
        }

        /// <summary>Checks the annotation texts against the expected values.</summary>
        public static void Validate(DrawingSnapshot snapshot, Dictionary<string, string> contents)
        {
            foreach (var pair in contents)
            {
                if (Text(snapshot.Entities[pair.Key]) != pair.Value)
                    throw new InvalidOperationException("Annotation verification failed: " + pair.Key);
            }
        }
    }
}
