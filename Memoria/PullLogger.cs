using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Lumina.Excel.GeneratedSheets;
using Memoria.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Common.Component.BGCollision.MeshPCB;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Dalamud.Game.ClientState.Conditions;

namespace Memoria
{
    internal class PullLogger
    {
        private Configuration? Config { get; set; }

        private PullLog? CurrentPull = null;
        private int PullNumber = 0;
        private DateTime LockoutStartTime = DateTime.Now;
        private bool HasCombatStarted = false;

        public void Init(Configuration Config)
        {
            this.Config = Config;
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Plugin.DutyState.DutyStarted        += OnDutyStarted;
            Plugin.DutyState.DutyWiped          += OnDutyWiped;
            Plugin.DutyState.DutyCompleted      += OnDutyCompleted;
            Plugin.DutyState.DutyRecommenced    += OnDutyRecommenced;
            Plugin.ChatGui.ChatMessage          += ChatGui_ChatMessage;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            Plugin.Condition.ConditionChange    += OnConditionChange;

            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_EnemyList", OnEnemyListPostDraw);
        }

        public void UnInit()
        {
            Plugin.DutyState.DutyStarted        -= OnDutyStarted;
            Plugin.DutyState.DutyWiped          -= OnDutyWiped;
            Plugin.DutyState.DutyCompleted      -= OnDutyCompleted;
            Plugin.DutyState.DutyRecommenced    -= OnDutyRecommenced;
            Plugin.ChatGui.ChatMessage          -= ChatGui_ChatMessage;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            Plugin.Condition.ConditionChange    -= OnConditionChange;

            Plugin.AddonLifecycle.UnregisterListener(OnEnemyListPostDraw);
        }

        private void ChatGui_ChatMessage(Dalamud.Game.Text.XivChatType type, int timestamp, ref Dalamud.Game.Text.SeStringHandling.SeString sender, ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (type == (Dalamud.Game.Text.XivChatType)185) // Countdown start seems to be 185, not system message
            {
                if (Data.CountdownStartStrings.TryGetValue(Plugin.PluginInterface.UiLanguage, out var battleStartLine)
                    && message.TextValue.StartsWith(battleStartLine) && message.TextValue.EndsWith(")"))
                {
                    Plugin.Log.Information($"");
                    OnCountdownStarted();
                }
            }
            //else if (type == Dalamud.Game.Text.XivChatType.)
            {

            }

            //Plugin.ClientState.LocalPlayer.

            Plugin.Log.Information($"{type} {message.TextValue} {sender.TextValue}");
        }

        private void OnDutyStarted(object? sender, ushort e)
        {
            Plugin.Log.Information("DutyState_DutyStarted");
        }

        private void OnDutyRecommenced(object? sender, ushort e)
        {
            Plugin.Log.Information("DutyState_DutyRecommenced");
        }

        private void OnDutyCompleted(object? sender, ushort e)
        {
            Plugin.Log.Information("DutyState_DutyCompleted");
        }

        private void OnDutyWiped(object? sender, ushort e)
        {
            Plugin.Log.Information("DutyState_DutyWiped");
            PullStop();
        }

        private void OnCountdownStarted()
        {
            Plugin.Log.Information("CountdownStarted");
            PullStart();
        }

        private void OnCountdownFinsihed()
        {
            Plugin.Log.Information("CountdownFinsihed");
        }

        private void OnCountdownCanceled()
        {
            Plugin.Log.Information("CountdownCanceled");
        }

        private void OnTerritoryChanged(ushort obj)
        {
            Plugin.Log.Information("ClientState_TerritoryChanged");
            OnEnteredZone();
        }

        private void OnEnemyListPostDraw(AddonEvent type, AddonArgs args)
        {
            if (!HasCombatStarted)
            {
                //OnCombatStart();
                HasCombatStarted = true;
            }
        }

        private void OnCombatStart()
        {
            Plugin.Log.Information("OnCombatStart");
            if (CurrentPull == null)
            {
                PullStart();
            }
        }

        private void OnCombatEnd()
        {
            Plugin.Log.Information("OnCombatEnd");
        }

        private void OnEnteredZone()
        {
            PullNumber = 0;
            LockoutStartTime = DateTime.Now;
        }

        private void OnConditionChange(ConditionFlag flag, bool value)
        {
            Plugin.Log.Information($"OnConditionChange: {flag}: {value}");
            if (flag == ConditionFlag.InCombat)
            {
                if (value)
                    OnCombatStart();
                else
                    OnCombatEnd();
            }
        }

        private void PullStart()
        {
            Plugin.Log.Information("PullStart");

            if (CurrentPull != null)
            {
                // Save the pull incase
                SavePullLog();
            }

            StartNewPullLog();
            Plugin.OBSLink.StartRecording();
        }

        private void PullStop()
        {
            Plugin.Log.Information("PullStop");
            SavePullLog();
            CurrentPull = null;
            HasCombatStarted = false;
            Plugin.OBSLink.StopRecording();
        }

        private void StartNewPullLog()
        {
            Plugin.Log.Information($"FullTerritoryType: {Plugin.ClientState.TerritoryType}");

            CurrentPull = new PullLog()
            {
                ZoneName         = Data.GetTerritory(Plugin.ClientState.TerritoryType)?.PlaceName?.Value?.Name ?? "Unknown",
                ContentName      = Data.GetContentFinderCondition(Plugin.ClientState.TerritoryType)?.Name ?? "Unknown",
                PullNumber       = ++PullNumber,
                LockoutStartTime = LockoutStartTime,
                PullStartTime    = DateTime.Now
            };
        }

        private void SavePullLog()
        {
            if (CurrentPull != null)
            {
                try
                {
                    var pathSafeContentName = string.Join("", CurrentPull.ContentName.Split(Path.GetInvalidFileNameChars()));
                    var basePath = Path.Combine(Config.PullSaveLocation, pathSafeContentName);
                    if (!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);

                    var pullName = $"{CurrentPull.PullStartTime.ToShortDateString().Replace("/", "-")} {CurrentPull.PullStartTime.ToString("HH-MM-ss")} - Pull {PullNumber}.json";
                    var jsonStr = JsonConvert.SerializeObject(CurrentPull, Formatting.Indented);
                    var fullPath = Path.Combine(basePath, pullName);

                    Plugin.Log.Information($"Saved pull log to {fullPath}");
                    File.WriteAllText(fullPath, jsonStr);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error saving pull log: {ex}");
                }
            }
        }
    }
}
