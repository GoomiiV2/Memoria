using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria.Models
{
    internal class PullLog
    {
        public string ZoneName;
        public string ContentName;
        public DateTime PullStartTime;
        public TimeSpan PullLength;
        public DateTime LockoutStartTime;
        public int PullNumber;
        public string RecordingPath;

        public List<PartyMember> Party = [];
    }
}
