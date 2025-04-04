using System.Collections.Generic;

namespace Memoria.Models.GameData
{
    internal class GameData
    {
        public Dictionary<ushort, Status> Status = [];
        public Dictionary<uint, Action> Action = [];
    }
}
