using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memoria
{
    public static class Data
    {
        public static Dictionary<string, string> CountdownStartStrings = new()
        {
            { "en", "Battle commencing" }
        };

        public static Dictionary<string, string> CountdownEndStrings = new()
        {
            { "en", "Battle commencing" }
        };

        public static Dictionary<string, string> CountdownCanceledStrings = new()
        {
            { "en", "Battle commencing" }
        };

        public static List<TerritoryType> Territories { get; private set; } = [];
        public static List<ContentFinderCondition> ContentFinderConditions { get; private set; } = [];

        public static void Init()
        {
            Territories = Plugin.DataManager?.GetExcelSheet<TerritoryType>()?.ToList() ?? [];
            ContentFinderConditions = Plugin.DataManager?.GetExcelSheet<ContentFinderCondition>()?.ToList() ?? [];
        }

        public static TerritoryType? GetTerritory(ushort territoryId)
        {
            var territory = Territories.FirstOrDefault(row => row.RowId == territoryId);
            return territory;
        }

        public static ContentFinderCondition? GetContentFinderCondition(ushort territoryId)
        {
            var content = ContentFinderConditions.FirstOrDefault(row => row.TerritoryType.Row == territoryId);
            return content;
        }
    }
}
