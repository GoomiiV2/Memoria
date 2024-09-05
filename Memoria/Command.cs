using Dalamud.Game.Command;

namespace Memoria
{
    internal class Command(string Name, CommandInfo Info)
    {
        public string Name { get; } = Name;
        public CommandInfo Info { get; } = Info;
    }
}
