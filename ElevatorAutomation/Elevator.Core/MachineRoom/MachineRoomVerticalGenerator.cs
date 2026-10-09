using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Adds the machine room to the vertical section geometry.</summary>
    public static class MachineRoomVerticalGenerator
    {
        /// <summary>Adds the machine room to the vertical section geometry.</summary>
        public static void Generate(VerticalGeometry g, VerticalProjectData d, VerticalSource s)
        {
            g.Note(
                "MR.Title",
                "MACHINE ROOM  |  COORDINATED B-B\\PEquipment translated by R; original geometry retained",
                s.RoomRearX,
                g.CeilingZ + 300);
        }
    }
}
