using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Memoria.Models;
using Memoria.Models.Events;
using Memoria.Models.GameData;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Status = Memoria.Models.GameData.Status;

namespace Memoria
{
    internal class PullLogger
    {
        private Configuration Config { get; set; } = new();

        private PullLog? CurrentPull = null;
        private int PullNumber = 0;
        private DateTime LockoutStartTime = DateTime.Now;
        private bool HasCombatStarted = false;
        private bool ShouldRecord = false;

        private static bool EnableHooks = false;
        private Hook<ActionEffectHandler.Delegates.Receive> ActionEffectHandler_RecvHook;
        private Hook<StatusManager.Delegates.AddStatus> StatusManager_AddStatusHook;

        public void Init(Configuration Config)
        {
            this.Config = Config;
            ShouldRecord = ShouldRecordInCurrentDuty();
            RegisterEvents();

            //Data.DumpContentFinderConditions("I:\\Recordings\\UWU\\ContentFinderConditions.txt");
            //Data.DumpChatChannels("I:\\Recordings\\UWU\\DumpChatChannels.txt");
        }

        private unsafe void RegisterEvents()
        {
            Plugin.DutyState.DutyStarted += OnDutyStarted;
            Plugin.DutyState.DutyWiped += OnDutyWiped;
            Plugin.DutyState.DutyCompleted += OnDutyCompleted;
            Plugin.DutyState.DutyRecommenced += OnDutyRecommenced;
            Plugin.ChatGui.ChatMessage += ChatGui_ChatMessage;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            Plugin.Condition.ConditionChange += OnConditionChange;

            if (EnableHooks)
            {
                ActionEffectHandler_RecvHook = Plugin.GameInteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(ActionEffectHandler.MemberFunctionPointers.Receive, ActionEffectHandlerRecvDetour);
                ActionEffectHandler_RecvHook.Enable();

                //StatusManager_AddStatusHook = Plugin.GameInteropProvider.HookFromAddress<StatusManager.Delegates.AddStatus>(StatusManager.MemberFunctionPointers.AddStatus, StatusManager_AddStatusDetour);
                //StatusManager_AddStatusHook.Enable();
            }

            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_EnemyList", OnEnemyListPostDraw);
        }

        public void UnInit()
        {
            Plugin.DutyState.DutyStarted -= OnDutyStarted;
            Plugin.DutyState.DutyWiped -= OnDutyWiped;
            Plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            Plugin.DutyState.DutyRecommenced -= OnDutyRecommenced;
            Plugin.ChatGui.ChatMessage -= ChatGui_ChatMessage;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            Plugin.Condition.ConditionChange -= OnConditionChange;

            if (EnableHooks)
            {
                ActionEffectHandler_RecvHook.Dispose();
                //StatusManager_AddStatusHook.Dispose();
            }

            Plugin.AddonLifecycle.UnregisterListener(OnEnemyListPostDraw);
        }

        private unsafe void ActionEffectHandlerRecvDetour(uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            ActionEffectHandler_RecvHook.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);

            if (!ShouldRecord)
                return;

            for (var i = 0; i < effects->Effects.Length; i++)
            {
                var effect = effects->Effects[i];
                if ((ActionEffectType)effect.Type == ActionEffectType.ApplyStatusEffectTarget)
                {
                    for (int eye = 0; eye < header->NumTargets; eye++)
                    {
                        OnReciveStatusEffect(casterPtr, targetEntityIds[eye], effect.Value, header->ActionId);
                    }
                }
            }
        }

        private unsafe void StatusManager_AddStatusDetour(StatusManager* thisPtr, ushort statusId, ushort param, void* u3)
        {
            StatusManager_AddStatusHook.Original(thisPtr, statusId, param, u3);

            var status = Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()?.GetRow(statusId);
            Plugin.Log.Information($"Status: {statusId} {status?.Name} {param}");
        }

        private unsafe void OnReciveStatusEffect(Character* casterPtr, GameObjectId targetEntityId, ushort statusId, uint actionId)
        {
            var action = Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.GetRow(actionId);
            var status = Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Status>()?.GetRow(statusId);
            var targetObject = Plugin.ObjectTable.SearchById(targetEntityId);
            var casterGameObj = Plugin.ObjectTable.SearchByEntityId(casterPtr->EntityId);
            Plugin.Log.Information($"status: {casterPtr->NameString} applies {status?.Name}({statusId}, {status.StatusCategory}) to {targetObject?.Name} (from {action.Name})");

            TryLogEntity(targetObject);
            TryLogEntity(casterGameObj);

            var targetSnapshot = AddEntitySnapsdshot(targetObject);
            var castertSanpshot = AddEntitySnapsdshot(casterGameObj);

            TryLogStatus(status);
            TryLogAction(action);

            var getStatusEvent = new GetStatusEffectEvent()
            {
                Caster = casterPtr->EntityId,
                Target = targetEntityId.ObjectId,
                Status = statusId,
                TargetSnapshot = targetSnapshot,
                CasterSnapshot = castertSanpshot,
            };

            AddTimelineEvent(getStatusEvent);
        }

        private void TryLogEntity(IGameObject gameObj)
        {
            if (CurrentPull != null && gameObj != null && !CurrentPull.Entities.ContainsKey(gameObj.EntityId))
            {
                var battleChar = gameObj as IBattleChara;
                var entityData = new EntityData()
                {
                    Name = gameObj.Name.TextValue,
                    MaxHP = battleChar?.MaxHp ?? 0,
                    MaxMP = battleChar?.MaxMp ?? 0,
                    Level = battleChar?.Level ?? 0,
                };

                CurrentPull.Entities.Add(gameObj.EntityId, entityData);
            }
        }

        private void TryLogStatus(Lumina.Excel.GeneratedSheets.Status status)
        {
            if (CurrentPull != null && status != null && !CurrentPull.GameData.Status.ContainsKey((ushort)status.RowId))
            {
                var statusData = new Status()
                {
                    Name = status.Name,
                    Desc = status.Description,
                    IconId = status.Icon
                };

                CurrentPull.GameData.Status.Add((ushort)status.RowId, statusData);
            }
        }

        private void TryLogAction(Lumina.Excel.GeneratedSheets.Action action)
        {
            if (CurrentPull != null && action != null && !CurrentPull.GameData.Action.ContainsKey((ushort)action.RowId))
            {
                var actionData = new Models.GameData.Action()
                {
                    Name = action.Name,
                    IconId = action.Icon
                };

                CurrentPull.GameData.Action.Add((ushort)action.RowId, actionData);
            }
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

            //Plugin.Log.Information($"{type} {message.TextValue} {sender.TextValue}");
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
            ShouldRecord = ShouldRecordInCurrentDuty();
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

        private bool ShouldRecordInCurrentDuty()
        {
            var contentId = Data.GetContentTypeIdForZone();
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
            if (!ShouldRecord)
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
            if (!ShouldRecord)
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
                ZoneName = Data.GetTerritory(Plugin.ClientState.TerritoryType)?.PlaceName?.Value?.Name ?? "Unknown",
                ContentName = Data.GetContentFinderCondition(Plugin.ClientState.TerritoryType)?.Name ?? "Unknown",
                PlayerName = Plugin.ClientState.LocalPlayer?.Name?.TextValue ?? "Unknown",
                PlayerId = $"{Plugin.ClientState.LocalContentId}",
                PullNumber = ++PullNumber,
                LockoutStartTime = LockoutStartTime,
                PullStartTime = DateTime.Now
            };

            AddPartyToPullLog();
            AddPlayerLoadout();
        }

        private void SavePullLog()
        {
            if (CurrentPull != null)
            {
                try
                {
                    var jsonStr = JsonConvert.SerializeObject(CurrentPull, Formatting.Indented, new StringEnumConverter());
                    var fullPath = GetFileNameForPull("json");

                    var fullPathDir = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(fullPathDir))
                        Directory.CreateDirectory(fullPathDir);

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
                if (recPath == null)
                    return "";

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

        private void AddPartyToPullLog()
        {
            for (var i = 0; i < Plugin.PartyList.Length; i++)
            {
                var member = Plugin.PartyList[i];
                var partyMember = new PartyMember()
                {
                    Name = member?.Name?.TextValue ?? "",
                    World = member?.World?.GameData?.Name ?? "",
                    Job = member?.ClassJob?.GameData?.Name ?? "",
                    MaxHP = member?.MaxHP ?? 0,
                    MaxMP = member?.MaxMP ?? 0,
                    Level = member?.Level ?? 0,
                    EntityId = member?.GameObject?.EntityId ?? 0
                };

                CurrentPull.Party.Add(partyMember);
            }
        }

        private unsafe void AddPlayerLoadout()
        {
            var equipedInv = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            CurrentPull.PlayerLoadout = new PlayerLoadout()
            {
                ILevel = 0
            };

            double combinedIlevel = 0;
            int numValidIlevelItems = 0;
            for (int i = 0; i < equipedInv->Size; i++)
            {
                if (i >= (int)GearSlotId.JobStone)
                    break;

                var item = equipedInv->Items[i];
                var itemInfo = Data.GetItemFromId(item.ItemId);

                var gearItem = new GearItem()
                {
                    Id = item.ItemId,
                    Name = itemInfo.Name,
                    ILevel = itemInfo.LevelItem.Row
                };

                if (item.ItemId != 0)
                {
                    numValidIlevelItems++;
                    combinedIlevel += (int)itemInfo.LevelItem.Row;
                }

                CurrentPull.PlayerLoadout.Gear.Add((GearSlotId)i, gearItem);
            }

            CurrentPull.PlayerLoadout.ILevel = (int)Math.Round(combinedIlevel / (double)numValidIlevelItems);
        }

        private void AddTimelineEvent(TimelineEvent timelineEvent)
        {
            timelineEvent.Time = (DateTime.Now - CurrentPull.PullStartTime).TotalMilliseconds;

            CurrentPull.Timeline.Add(timelineEvent);
        }

        private int AddEntitySnapsdshot(uint entityId) => AddEntitySnapsdshot(Plugin.ObjectTable.SearchByEntityId(entityId));
        private int AddEntitySnapsdshot(IGameObject gameObj)
        {
            var battleChar = gameObj as IBattleChara;
            var snapShot = new EntitySnapshot()
            {
                EntityId = gameObj.EntityId,
                Hp = battleChar?.CurrentHp ?? 0,
                Mp = battleChar?.CurrentMp ?? 0,
                WorldPos = gameObj?.Position ?? Vector3.Zero
            };

            CurrentPull?.EntitySnapshots.Add(snapShot);

            return CurrentPull?.EntitySnapshots?.Count - 1 ?? -1;
        }

        // private bool IsBoss(IGameObject chara) => Plugin.DataManager.GetExcelSheet<BNpcBase>()!.GetRow(chara.DataId)?.Rank is 1 or 2 or 6;
    }

    internal enum ActionEffectType : byte
    {
        Nothing = 0,
        Miss = 1,
        FullResist = 2,
        Damage = 3,
        Heal = 4,
        BlockedDamage = 5,
        ParriedDamage = 6,
        Invulnerable = 7,
        NoEffectText = 8,
        MpLoss = 10,
        MpGain = 11,
        TpLoss = 12,
        TpGain = 13,
        ApplyStatusEffectTarget = 14,
        ApplyStatusEffectSource = 15,
        RecoveredFromStatusEffect = 16,
        LoseStatusEffectTarget = 17,
        LoseStatusEffectSource = 18,
        StatusNoEffect = 20,
        ThreatPosition = 24,
        EnmityAmountUp = 25,
        EnmityAmountDown = 26,
        StartActionCombo = 27,
        Knockback = 33,
        Mount = 40,
        FullResistStatus = 55,
        Vfx = 59,
        Gauge = 60,
        PartialInvulnerable = 74,
        Interrupt = 75,
    }
}
