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
using Newtonsoft.Json.Converters;

namespace Memoria
{
    internal class PullLogger
    {
        private Configuration Config { get; set; } = new ();

        private PullLog? CurrentPull = null;
        private int PullNumber = 0;
        private DateTime LockoutStartTime = DateTime.Now;
        private bool HasCombatStarted = false;

        public void Init(Configuration Config)
        {
            this.Config = Config;
            RegisterEvents();

            //Data.DumpContentFinderConditions("I:\\Recordings\\UWU\\ContentFinderConditions.txt");
            //Data.DumpChatChannels("I:\\Recordings\\UWU\\DumpChatChannels.txt");
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
            if ((int)type is 185 or 569) // Countdown start seems to be 185 or 569, not system message
            {
                if (Data.CountdownStartStrings.TryGetValue(Plugin.PluginInterface.UiLanguage, out var battleStartLine)
                    && message.TextValue.StartsWith(battleStartLine) && message.TextValue.EndsWith(")"))
                {
                    OnCountdownStarted();
                }
                else if (Data.CountdownCanceledStrings.TryGetValue(Plugin.PluginInterface.UiLanguage, out var battleCanceledLine)
                    && message.TextValue.StartsWith(battleCanceledLine))
                {
                    OnCountdownCanceled();
                }
                else if (Data.CountdownEndStrings.TryGetValue(Plugin.PluginInterface.UiLanguage, out var battleCommenceLine)
                    && message.TextValue.StartsWith(battleCommenceLine))
                {
                    OnCountdownFinsihed();
                }
            }

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
            PullStop(PullState.Cleared);
        }

        private void OnDutyWiped(object? sender, ushort e)
        {
            Plugin.Log.Information("DutyState_DutyWiped");
            PullStop(PullState.Wiped);
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
            PullStop(PullState.Canceled, true);
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

        private bool ShouldRecord()
        {
            var contentId = Data.GetContentTypeIdForZone();
            Plugin.Log.Information($"{contentId}");
            if ((contentId == ContentTypeId.Trial && Config.RecInNormTrials) ||
                (contentId == ContentTypeId.ExTrial && Config.RecInExTrials) ||
                (contentId == ContentTypeId.Raid && Config.RecInNormRaids) ||
                (contentId == ContentTypeId.SavageRaid && Config.RecInSavageRaids) ||
                (contentId == ContentTypeId.Ultimate && Config.RecInUltimates))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async void PullStart()
        {
            if (!ShouldRecord())
                return;

            Plugin.Log.Information("PullStart");

            if (CurrentPull != null)
            {
                // Save the pull incase
                await PullStop(PullState.Unknown, true);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            StartNewPullLog();
            await Plugin.OBSLink.StartRecording();
        }

        private async Task PullStop(PullState pullState, bool forceStop = false)
        {
            if (!ShouldRecord())
                return;

            Plugin.Log.Information("PullStop");

            if (!forceStop)
                await Task.Delay(TimeSpan.FromSeconds(Config.DelayAfterPullEndToStopRec));

            var stopRecTask = Plugin.OBSLink.StopRecording();
            var stopRecTimeout = Task.Delay(TimeSpan.FromSeconds(2));
            await Task.WhenAny(stopRecTask, stopRecTimeout);

            if (CurrentPull != null)
            {
                if (stopRecTask.IsCompleted)
                {
                    CurrentPull.RecordingPath = MoveAndRenameRecording(stopRecTask.Result);
                }
                CurrentPull.PullState = pullState;
                CurrentPull.PullLength = DateTime.Now - CurrentPull.PullStartTime;
                SavePullLog();
                CurrentPull = null;
                HasCombatStarted = false;
            }
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
                    var jsonStr = JsonConvert.SerializeObject(CurrentPull, Formatting.Indented, new StringEnumConverter());
                    var fullPath = GetFileNameForPull("json");

                    Plugin.Log.Information($"Saved pull log to {fullPath}");
                    File.WriteAllText(fullPath, jsonStr);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error saving pull log: {ex}");
                }
            }
        }

        private string MoveAndRenameRecording(string recPath)
        {
            try
            {
                var newPath = GetFileNameForPull("mp4");
                var newPathDir = Path.GetDirectoryName(newPath);
                if (!Directory.Exists(newPathDir))
                    Directory.CreateDirectory(newPathDir);

                try
                {
                    File.Move(recPath, newPath);
                }
                catch (IOException)
                {
                    Plugin.Log.Information("MoveAndRenameRecording, failed to move file trying agian in 4 seconds");
                    // try again, abit later
                    Task.Delay(TimeSpan.FromSeconds(4)).ContinueWith(t =>
                    {
                        Plugin.Log.Information("MoveAndRenameRecording, trying again");
                        File.Move(recPath, newPath);
                    });
                }

                return newPath;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"MoveAndRenameRecording Error: {ex.ToString()}");
                return recPath;
            }
        }

        private string GetFileNameForPull(string ext)
        {
            var pathSafeContentName = string.Join("", CurrentPull.ContentName.Split(Path.GetInvalidFileNameChars()));
            var lockoutStartTime = CurrentPull.LockoutStartTime.ToString("yyyy-MM-dd HH-mm-ss");
            var pullStartTime = CurrentPull.PullStartTime.ToString("yyyy-MM-dd HH-mm-ss");
            var pullFileName = $"{pullStartTime} - Pull {CurrentPull.PullNumber}";
            var fullPath = Path.Combine(Config.PullSaveLocation, pathSafeContentName, lockoutStartTime, $"{pullFileName}.{ext}");

            return fullPath;
        }
    }
}
