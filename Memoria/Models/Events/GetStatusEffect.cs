using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.Models.Events
{
    public class GetStatusEffectEvent : TimelineEvent
    {
        public override EventType EventType => EventType.GotStatusEffect;
        public long Caster;
        public long Target;
        public long Status;

        public int CasterSnapshot;
        public int TargetSnapshot;
    }
}
