
namespace Memoria.Models.Events
{
    public class GetStatusEffectEvent : TimelineEvent
    {
        public GetStatusEffectEvent(long caster, long target, long status, int casterSnapshot, int targetSnapshot, uint? actionId = null)
        {
            Caster          = caster;
            Target          = target;
            Status          = status;
            CasterSnapshot  = casterSnapshot;
            TargetSnapshot  = targetSnapshot;
            CausingActionId = actionId;
        }
        
        // Selfcast
        public GetStatusEffectEvent(long caster, long status, int casterSnapshot, uint? actionId = null)
        {
            Caster         = caster;
            Target         = caster;
            Status         = status;
            CasterSnapshot = casterSnapshot;
            TargetSnapshot = casterSnapshot;
            CausingActionId = actionId;
        }

        public override EventType EventType => EventType.GotStatusEffect;
        public          long      Caster;
        public          long      Target;
        public          long      Status;
        public          uint?      CausingActionId;

        public int CasterSnapshot;
        public int TargetSnapshot;
    }
}
