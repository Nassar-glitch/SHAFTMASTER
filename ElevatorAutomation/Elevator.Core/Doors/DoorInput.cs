using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>The door inputs that the plan recipe needs: the type, the locked values and the catalog.</summary>
    public sealed class DoorInput
    {
        public string Type;
        public DoorSettings Settings = new DoorSettings();
        public DoorCatalog Catalog;

        /// <summary>The door input of a project, or null for a Manual door (the existing recipe applies unchanged).</summary>
        public static DoorInput From(ElevatorProject project, DoorCatalog catalog)
        {
            if (project == null || project.Doors == null || !DoorTypes.IsAutomatic(project.Doors.Type))
                return null;
            return new DoorInput
            {
                Type = DoorTypes.Normalize(project.Doors.Type),
                Settings = DoorSettings.Read(project),
                Catalog = catalog ?? DoorCatalog.Default()
            };
        }

        /// <summary>The front zone of this door: car zone + sill gap + landing zone.</summary>
        public double FrontZoneMm()
        {
            var t = Catalog.For(Type);
            return (Settings.CarZoneMm ?? t.CarZoneMm.Value)
                + (Settings.SillGapMm ?? t.SillGapMm.Value)
                + (Settings.LandingZoneMm ?? t.LandingZoneMm.Value);
        }
    }
}
