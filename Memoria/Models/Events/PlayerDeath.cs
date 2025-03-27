using System.Numerics;

namespace Memoria.Models.Events
{
    public class PlayerDeath : TimelineEvent
    {
        public override EventCategory Category => EventCategory.Player;
        public override EventType EventType => EventType.PlayerDeath;
        public string PlayerName;
        public Vector3 Position;
    }
}
