using Memoria.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria
{
    public class PlayerLoadout
    {
        public int ILevel;
        public Dictionary<GearSlotId, GearItem> Gear = [];
    }
}
