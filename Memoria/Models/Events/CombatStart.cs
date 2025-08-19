namespace Memoria.Models.Events;

public class CombatStart : TimelineEvent
{
    public override EventType EventType => EventType.CombatStart;
}
