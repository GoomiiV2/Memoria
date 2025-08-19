using System.Collections.Frozen;
using Lumina.Excel.Sheets;
using Memoria.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            { "en", "Engage!" }
        };

        public static Dictionary<string, string> CountdownCanceledStrings = new()
        {
            { "en", "Countdown canceled" }
        };

        public static List<TerritoryType>          Territories             { get; private set; } = [];
        public static List<ContentFinderCondition> ContentFinderConditions { get; private set; } = [];
        public static HashSet<uint>                IsBoss                  { get; private set; } = [];

        public static void Init()
        {
            Territories = Plugin.DataManager?.GetExcelSheet<TerritoryType>()?.ToList() ?? [];
            ContentFinderConditions = Plugin.DataManager?.GetExcelSheet<ContentFinderCondition>()?.ToList() ?? [];
            BuildBossTable();
        }

        private static void BuildBossTable()
        {
            var bnpcs = Plugin.DataManager?.GetExcelSheet<BNpcBase>();
            foreach (var bnpc in bnpcs)
            {
                if (bnpc.Rank is 1 or 2 or 6)
                {
                    IsBoss.Add(bnpc.RowId);
                }
            }
        }

        public static TerritoryType? GetTerritory(ushort territoryId)
        {
            var territory = Territories.FirstOrDefault(row => row.RowId == territoryId);
            return territory;
        }

        public static ContentFinderCondition? GetContentFinderCondition(ushort territoryId)
        {
            var content = ContentFinderConditions.FirstOrDefault(row => row.TerritoryType.RowId == territoryId);
            return content;
        }

        public static ContentTypeId GetContentTypeIdForZone(ushort? territoryId = null)
        {
            var territoryType = GetTerritory(territoryId ?? Plugin.ClientState.TerritoryType);
            var cfCond        = territoryType?.ContentFinderCondition.Value;
            var cTypeId       = cfCond?.ContentType.RowId;
            var isHighEnd     = cfCond?.HighEndDuty;
            var id            = (cTypeId, isHighEnd) switch
            {
                (4, false) => ContentTypeId.Trial,
                (5, false) => ContentTypeId.Raid,
                (28, true) => ContentTypeId.Ultimate,
                (4, true)  => ContentTypeId.ExTrial,
                (5, true)  => ContentTypeId.SavageRaid,
                _          => ContentTypeId.Unknown
            };

            return id;
        }

        public static Item? GetItemFromId(uint itemId)
        {
            var item = Plugin.DataManager?.GetExcelSheet<Item>().GetRow(itemId);
            return item ?? null;
        }

        public static void DumpContentFinderConditions(string path)
        {
            var sb = new StringBuilder();
            foreach (var condition in ContentFinderConditions)
            {
                sb.AppendLine($"{condition.Name}, {condition.HighEndDuty}, {condition.ContentType.Value.Name} ({condition.ContentType.RowId})");
            }

            File.WriteAllText(path, sb.ToString());

            /*var jsonStr = JsonConvert.SerializeObject(ContentFinderConditions, Formatting.Indented, new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            File.WriteAllText(path, jsonStr);*/
        }

        public static void DumpChatChannels(string path)
        {
            var logKinds = Plugin.DataManager?.GetExcelSheet<Lumina.Excel.Sheets.LogKind>();
            var sb       = new StringBuilder();
            foreach (var logKind in logKinds)
            {
                sb.AppendLine($"{logKind.RowId} {logKind.Format.ToString()}");
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}
