using System;

namespace Elevator.Core
{
    public static class CommonRules
    {
        public static void ValidateProject(ProjectInput p, FamilyDefinition family)
        {
            if (p == null || p.SchemaVersion != 1)
                throw new ArgumentException("Unsupported or missing project schema.");
            if (family == null || !family.Enabled)
                throw new ArgumentException(
                    "This elevator family needs its own validated master template and profile before automation is enabled.");
            if (p.TemplateId != family.TemplateId)
                throw new ArgumentException(
                    "Project family does not match this drawing's template. Start from the corresponding family master.");
        }
    }
}
