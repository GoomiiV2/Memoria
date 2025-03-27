using System.Collections.Generic;

namespace Memoria.Models.GameData
{
    internal class GameData
    {
        public Dictionary<ushort, Status> Status = [];
        public Dictionary<long, Action> Action = [];
    }
}
