using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Application
{
    public sealed class FloorRow : INotifyPropertyChanged
    {
        readonly ProjectSession session;
        public readonly FloorDefinition Floor;
        readonly Action changed;

        public FloorRow(ProjectSession s, FloorDefinition f, Action callback)
        {
            session = s;
            Floor = f;
            changed = callback;
        }

        public string Name
        {
            get
            {
                return Floor.Name;
            }
            set
            {
                ProjectFloors.Rename(session.Project.Building, Floor.FloorId, value);
                Notify("Name");
            }
        }

        public string Height
        {
            get
            {
                return Floor.HeightToNextMm.HasValue ? Floor.HeightToNextMm.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";
            }
            set
            {
                double parsed;
                double? height = null;
                if (!String.IsNullOrWhiteSpace(value))
                {
                    if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                        || !ProjectFloors.FinitePositive(parsed))
                        throw new ArgumentException("Height must be a finite positive number in mm.");
                    height = parsed;
                }
                if (session.Project.Building.Floors.Last() == Floor && height.HasValue)
                    throw new ArgumentException("The final floor has no interval above it.");
                Floor.HeightToNextMm = height;
                Notify("Height");
            }
        }

        public bool Served
        {
            get
            {
                return Floor.IsServed;
            }
            set
            {
                Floor.IsServed = value;
                Notify("Served");
            }
        }

        public string Elevation
        {
            get
            {
                try
                {
                    return ProjectFloors.Elevations(session.Project.Building)
                        .Single(f => f.FloorId == Floor.FloorId)
                        .ElevationMm
                        .ToString("0.###");
                }
                catch (ArgumentException) { return ""; }
            }
        }

        void Notify(string name)
        {
            session.Dirty = true;
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            changed();
        }

        public void Refresh()
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs("Elevation"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
