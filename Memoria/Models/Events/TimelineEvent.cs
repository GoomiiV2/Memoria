namespace Memoria.Models.Events
{
    public class TimelineEvent
    {
        public virtual EventCategory Category => EventCategory.Undefined;
        public virtual EventType EventType => EventType.Undefined;
        public double Time;
    }
}
