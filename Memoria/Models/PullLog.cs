using Memoria.Models.Events;
using Memoria.Models.GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.Models
{
    internal class PullLog
    {
        public string    ZoneName;
        public string    ContentName;
        public string    PlayerName;
        public string    PlayerId;
        public DateTime  PullStartTime;
        public TimeSpan  PullLength;
        public float     BossHPPct;
        public DateTime  LockoutStartTime;
        public int       PullNumber;
        public string    RecordingPath;
        public PullState PullState = PullState.Unknown;

        public List<PartyMember> Party = [];
        public PlayerLoadout PlayerLoadout;

        public List<TimelineEvent> Timeline = new();
        public Dictionary<long, EntityData> Entities = [];
        public Memoria.Models.GameData.GameData GameData = new();
        public List<EntitySnapshot> EntitySnapshots = [];
        public List<uint> BossEntites = [];
    }
}
