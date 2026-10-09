using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public static class MachineRoomVerticalGenerator
    {
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
