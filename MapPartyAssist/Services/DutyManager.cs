using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapPartyAssist.Services {
    internal class DutyManager : IDisposable {

        private Plugin Plugin;
        private DutyResults? _currentDutyResults;

        private bool _firstTerritoryChange;

        //private static Dictionary<string, Type> DutyResultTypes = new Dictionary<string, Type>() {
        //    { "the lost canals of uznair", typeof(LostCanalsOfUznairResults) },
        //    { "the shifting altars of uznair", typeof(ShiftingAltarsOfUznairResults) },
        //    { "the hidden canals of uznair", typeof(HiddenCanalsOfUznairResults) }
        //};

        public readonly Dictionary<int, Duty> Duties = new Dictionary<int, Duty>() {
            { 179 , new Duty(179, "the aquapolis", DutyStructure.Doors, 7) },
            { 268, new Duty(268, "the lost canals of uznair", DutyStructure.Doors, 7, typeof(LostCanalsOfUznairResults)) },
            { 276 , new Duty(276, "the hidden canals of uznair", DutyStructure.Doors, 7, typeof(HiddenCanalsOfUznairResults)) },
            { 586, new Duty(586, "the shifting altars of uznair", DutyStructure.Roulette, 5, typeof(ShiftingAltarsOfUznairResults)) },
            { 688 , new Duty(688, "the dungeons of lyhe ghiah", DutyStructure.Doors, 5) },
            { 745 , new Duty(745, "the shifting oubliettes of lyhe ghiah", DutyStructure.Roulette, 5) },
            { 819 , new Duty(819, "the excitatron 6000", DutyStructure.Doors, 5) },
            { 909 , new Duty(909, "the shifting gymnasion agonon", DutyStructure.Roulette, 5) }
        };

        public DutyManager(Plugin plugin) {
            Plugin = plugin;
            Plugin.DutyState.DutyStarted += OnDutyStart;
            Plugin.DutyState.DutyCompleted += OnDutyCompleted;
            Plugin.DutyState.DutyWiped += OnDutyWiped;
            Plugin.DutyState.DutyCompleted += OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            Plugin.ChatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose() {
            PluginLog.Debug("disposing duty manager");
            Plugin.DutyState.DutyStarted -= OnDutyStart;
            Plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            Plugin.DutyState.DutyWiped -= OnDutyWiped;
            Plugin.DutyState.DutyCompleted -= OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            Plugin.ChatGui.ChatMessage -= OnChatMessage;

            //if(IsDutyInProgress()) {
            //    Plugin.ChatGui.ChatMessage -= _currentDutyResults.OnChatMessage;
            //}
        }

        private void StartNewDuty(int dutyId, Dictionary<string, MPAMember> players, string owner) {
            
            if(Duties.ContainsKey(dutyId) && Duties[dutyId].ResultsType != null) {
                object[] conParams = { dutyId, Duties[dutyId].Name, players, owner };
                _currentDutyResults = Duties[dutyId].ResultsType!.GetConstructors().First().Invoke(conParams) as DutyResults;

                Plugin.Configuration.DutyResults.Add(_currentDutyResults);
                Plugin.Save();
            }

            //dutyName = dutyName.ToLower();
            //if(DutyResultTypes.ContainsKey(dutyName)) {
            //    //var dutyResults = new DutyResults(dutyName, players, owner);
            //    //_currentDutyResults = typeof(DutyResultTypes[dutyName]) dutyResults as typeof(DutyResultTypes[dutyName]);
            //    //_currentDutyResults = typeof(DutyResultTypes[dutyName]).GetConstructor().Invoke(dutyId, players, owner);
            //    object[] conParams = { dutyId, dutyName, players, owner };
            //    _currentDutyResults = DutyResultTypes[dutyName].GetConstructors().First().Invoke(conParams) as DutyResults;
            //    //_currentDutyResults = DutyResultTypes[dutyName].GetConstructors().First().Invoke(conParams);
            //    //DutyResultTypes[dutyName].Name;
            //    Plugin.Configuration.DutyResults.Add(_currentDutyResults!);
            //    Plugin.Save();
            //}
        }

        //find map with same duty and most proximal time
        public MPAMap? FindMapForDutyResults(DutyResults results) {
            MPAMap? topCandidateMap = null;
            foreach(var player in Plugin.Configuration.RecentPartyList) {
                foreach(var map in player.Value.Maps) {
                    topCandidateMap ??= map;
                    TimeSpan currentMapSpan = results.Time - map.Time;
                    TimeSpan topCandidateMapSpan = results.Time - topCandidateMap.Time;
                    //same duty and must happen afterwards, but not more than 20 mins as a fallback
                    bool sameDuty = !map.DutyName.IsNullOrEmpty() && map.DutyName.Equals(results.DutyName, StringComparison.OrdinalIgnoreCase);
                    bool validTime = currentMapSpan.TotalMilliseconds > 0 && currentMapSpan.TotalMinutes < 20;
                    bool closerTime = currentMapSpan < topCandidateMapSpan;
                    if(sameDuty && validTime && closerTime) {
                        topCandidateMap = map;
                        //clear top candidate if a closer time is found but with the wrong duty
                    } else if(!sameDuty && validTime && closerTime) {
                        topCandidateMap = null;
                        //clear invalid top candidates
                    } else if(map == topCandidateMap && (!sameDuty || !validTime)) {
                        topCandidateMap = null;
                    }
                }
            }
            return topCandidateMap;
        }

        public Duty? GetDutyByName(string name) {
            foreach(var duty in Duties) {
                if(duty.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                    return duty.Value;
                }
            }
            return null;
        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            PluginLog.Debug($"Duty has started with territory id: {territoryId} name: {Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = Plugin.Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");

            //check if duty is ongoing to attempt to pickup...
            PluginLog.Debug($"Current duty ongoing? {_currentDutyResults != null}");

            //if(duty != null) {
            //    StartNewDuty(duty.Name.ToString(), dutyId, Plugin.CurrentPartyList, Plugin.MapManager!.LastMapPlayerKey);
            //}
        }

        private void OnDutyCompleted(object? sender, ushort param1) {
            PluginLog.Debug("Duty completed!");
            //EndDuty();
        }

        private void OnDutyWiped(object? sender, ushort param1) {
            PluginLog.Debug("Duty wiped!");
            //EndDuty();
        }

        private void OnDutyRecommenced(object? sender, ushort param1) {
            PluginLog.Debug("Duty recommenced!");
            //EndDuty();
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            var dutyId = Plugin.Functions.GetCurrentDutyId();
            var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Territory changed: {territoryId}, Current duty: {Plugin.Functions.GetCurrentDutyId()}");

            if(IsDutyInProgress()) {
                //end duty if it was completed successfully or clear as a fallback. attempt to pickup otherwise on disconnect
                if(_currentDutyResults!.IsComplete || dutyId != _currentDutyResults.DutyId) {
                    EndDuty();
                }
            } else if(duty != null) {
                //attempt to pickup if game closed without completing properly
                var lastDuty = Plugin.Configuration.DutyResults.Last();
                if(lastDuty != null) {
                    TimeSpan lastTimeDiff = DateTime.Now - lastDuty.Time;
                    //pickup if duty is valid, and matches the last duty which was not completed and not more than an hour has elapsed (fallback)
                    if(Duties.ContainsKey(dutyId) && Duties[dutyId].ResultsType != null && lastDuty.DutyId == dutyId && !lastDuty.IsComplete && !_firstTerritoryChange && lastTimeDiff.TotalHours < 1) {
                        //_currentDutyResults = lastDuty as (Duties[dutyId].ResultsType);
                        //_currentDutyResults = Convert.ChangeType(lastDuty, Duties[dutyId].ResultsType) as DutyResults;

                        //TODO there must be a better way to do this
                        //switch(dutyId) {
                        //    case 268:
                        //        _currentDutyResults = lastDuty as LostCanalsOfUznairResults;
                        //        break;
                        //    case 276:
                        //        _currentDutyResults = lastDuty as HiddenCanalsOfUznairResults;
                        //        break;
                        //    case 586:
                        //        _currentDutyResults = lastDuty as ShiftingAltarsOfUznairResults;
                        //        break;
                        //    default:
                        //        break;
                        //}
                        //_currentDutyResults.IsPickup = true;
                    } else {
                        //otherwise attempt to start new duty!
                        StartNewDuty(dutyId, Plugin.CurrentPartyList, Plugin.MapManager!.LastMapPlayerKey);
                    }
                } else {
                    StartNewDuty(dutyId, Plugin.CurrentPartyList, Plugin.MapManager!.LastMapPlayerKey);
                }
            }
            _firstTerritoryChange = true;
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if(IsDutyInProgress()) {
                //save if changes discovered
                if(_currentDutyResults!.ProcessChat(type, senderId, sender, message)) {
                    Plugin.Save();
                }
            }
        }

        private void EndDuty(bool success = false) {
            if(IsDutyInProgress()) {
                //_currentDutyResults.IsComplete = success;
                //Plugin.ChatGui.ChatMessage -= _currentDutyResults.OnChatMessage;
                //only save full runs
                //if(_currentDutyResults.IsComplete) {
                //    //Plugin.Configuration.DutyResults.Add(_currentDutyResults);
                //    Plugin.Configuration.Save();
                //}
                //_currentDutyResults.CompletionTime = DateTime.Now;
                _currentDutyResults = null;
                Plugin.Save();
            }
        }

        private bool IsDutyInProgress() {
            return _currentDutyResults != null;
        }
    }
}
