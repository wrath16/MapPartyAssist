using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Services {
    internal class DutyManager : IDisposable {

        private Plugin Plugin;
        private DutyResults? _currentDutyResults;
        private bool _firstTerritoryChange;

        public readonly Dictionary<int, Duty> Duties = new Dictionary<int, Duty>() {
            { 179 , new Duty(179, "the aquapolis", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty."),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens."),
                new Checkpoint("Clear 2nd chamber", "The cages are empty."),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens."),
                new Checkpoint("Clear 3rd chamber", "The cages are empty."),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens."),
                new Checkpoint("Clear 4th chamber", "The cages are empty."),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens."),
                new Checkpoint("Clear 5th chamber", "The cages are empty."),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens."),
                new Checkpoint("Clear 6th chamber", "The cages are empty."),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens."),
                new Checkpoint("Clear final chamber", "The cages are empty.")
            }, new Checkpoint("Failure", "The Aquapolis has ended.")) },
            { 268, new Duty(268, "the lost canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty."),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens."),
                new Checkpoint("Clear 2nd chamber", "The cages are empty."),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens."),
                new Checkpoint("Clear 3rd chamber", "The cages are empty."),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens."),
                new Checkpoint("Clear 4th chamber", "The cages are empty."),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens."),
                new Checkpoint("Clear 5th chamber", "The cages are empty."),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens."),
                new Checkpoint("Clear 6th chamber", "The cages are empty."),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens."),
                new Checkpoint("Clear final chamber", "The cages are empty.")
            }, new Checkpoint("Failure", "The Lost Canals of Uznair has ended.")) },
            { 276 , new Duty(276, "the hidden canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty."),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens."),
                new Checkpoint("Clear 2nd chamber", "The cages are empty."),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens."),
                new Checkpoint("Clear 3rd chamber", "The cages are empty."),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens."),
                new Checkpoint("Clear 4th chamber", "The cages are empty."),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens."),
                new Checkpoint("Clear 5th chamber", "The cages are empty."),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens."),
                new Checkpoint("Clear 6th chamber", "The cages are empty."),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens."),
                new Checkpoint("Clear final chamber", "The cages are empty.")
            }, new Checkpoint("Failure", "The Hidden Canals of Uznair has ended.")) },
            { 586, new Duty(586, "the shifting altars of uznair", DutyStructure.Roulette, 5, new() {
                new Checkpoint("Complete 1st Summon"),
                new Checkpoint("Defeat 1st Summon"),
                new Checkpoint("Complete 2nd Summon"),
                new Checkpoint("Defeat 2nd Summon"),
                new Checkpoint("Complete 3rd Summon"),
                new Checkpoint("Defeat 3rd Summon"),
                new Checkpoint("Complete 4th Summon"),
                new Checkpoint("Defeat 4th Summon"),
                new Checkpoint("Complete final Summon"),
                new Checkpoint("Defeat final Summon")
            }, new Checkpoint("Failure", "The Shifting Altars of Uznair has ended.")) },
            { 688 , new Duty(688, "the dungeons of lyhe ghiah", DutyStructure.Doors, 5) },
            { 745 , new Duty(745, "the shifting oubliettes of lyhe ghiah", DutyStructure.Roulette, 5) },
            { 819 , new Duty(819, "the excitatron 6000", DutyStructure.Doors, 5) },
            { 909 , new Duty(909, "the shifting gymnasion agonon", DutyStructure.Roulette, 5) }
        };

        private Duty? _currentDuty {
            get {
                return IsDutyInProgress() ? Duties[_currentDutyResults.DutyId] : null;
            }
        }

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
        }

        private void StartNewDuty(int dutyId, Dictionary<string, MPAMember> players, string owner) {

            if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null) {
                //object[] conParams = { dutyId, Duties[dutyId].Name, players, owner };
                //_currentDutyResults = Duties[dutyId].ResultsType!.GetConstructors().First().Invoke(conParams) as DutyResults;
                _currentDutyResults = new DutyResults(dutyId, Duties[dutyId].Name, players, owner);
                //assume last map is the one...
                _currentDutyResults.Map = Plugin.StorageManager.GetMaps().Query().OrderBy(dr => dr.Time).ToList().Last();

                //Plugin.Configuration.DutyResults.Add(_currentDutyResults);
                Plugin.StorageManager.AddDutyResults(_currentDutyResults);
                Plugin.Save();
            }
        }

        public List<DutyResults> GetRecentDutyResultsList(int? dutyId = null) {
            return Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Map != null && !dr.Map.IsArchived && !dr.Map.IsDeleted && dr.IsComplete && (dutyId == null || dr.DutyId == dutyId)).ToList();
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


            //if(!IsDutyInProgress()) {
            //    var lastDuty = Plugin.StorageManager.GetDutyResults().Query().OrderBy(dr => dr.Time).ToList().Last();
            //    if(lastDuty != null) {
            //        TimeSpan lastTimeDiff = DateTime.Now - lastDuty.Time;
            //        //pickup if duty is valid, and matches the last duty which was not completed and not more than an hour has elapsed (fallback)
            //        if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null && lastDuty.DutyId == dutyId && !lastDuty.IsComplete && !_firstTerritoryChange && lastTimeDiff.TotalHours < 1) {
            //            PluginLog.Debug("re-picking up duty results...");
            //            _currentDutyResults = lastDuty;
            //            _currentDutyResults.IsPickup = true;
            //            Plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
            //            Plugin.Save();
            //        }
            //    }
            //}
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
                //var lastDuty = Plugin.Configuration.DutyResults.Last();
                var lastDuty = Plugin.StorageManager.GetDutyResults().Query().OrderBy(dr => dr.Time).ToList().Last();
                if(lastDuty != null) {
                    TimeSpan lastTimeDiff = DateTime.Now - lastDuty.Time;
                    //pickup if duty is valid, and matches the last duty which was not completed and not more than an hour has elapsed (fallback)
                    if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null && lastDuty.DutyId == dutyId && !lastDuty.IsComplete && !_firstTerritoryChange && lastTimeDiff.TotalHours < 1) {
                        PluginLog.Debug("re-picking up duty results...");
                        _currentDutyResults = lastDuty;
                        _currentDutyResults.IsPickup = true;
                        Plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                        Plugin.Save();
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

                //check for gil obtained
                if((int)type == 62) {
                    Match m = Regex.Match(message.ToString(), @"(?<=You obtain )[\d,\.]+(?= gil)");
                    if(m.Success) {
                        string parsedGilString = m.Value.Replace(",", "").Replace(".", "");
                        _currentDutyResults!.TotalGil += int.Parse(parsedGilString);
                        Plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                        Plugin.Save();
                        return;
                    }
                }

                bool isChange = false;
                switch(_currentDuty!.Structure) {
                    case DutyStructure.Doors:
                        isChange = ProcessCheckpointsDoors(type, senderId, sender, message);
                        break;
                    case DutyStructure.Roulette:
                        isChange = ProcessCheckpointsRoulette(type, senderId, sender, message);
                        break;
                    default:
                        break;
                }
                //save if changes discovered
                if(isChange) {
                    Plugin.StorageManager.UpdateDutyResults(_currentDutyResults!);
                    Plugin.Save();
                }
            }
        }

        //return true if updates made
        private bool ProcessCheckpointsDoors(XivChatType type, uint senderId, SeString sender, SeString message) {
            Duty currentDuty = Duties[_currentDutyResults!.DutyId];
            var nextCheckpoint = currentDuty.Checkpoints![_currentDutyResults.CheckpointResults.Count];
            if(((int)type == 2233 || (int)type == 2105) && nextCheckpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                _currentDutyResults.CheckpointResults.Add(new CheckpointResults(nextCheckpoint, true));
                //if all checkpoints reached, set to duty complete
                if(_currentDutyResults!.CheckpointResults.Where(cr => cr.IsReached).Count() == currentDuty.Checkpoints!.Count) {
                    _currentDutyResults.IsComplete = true;
                    _currentDutyResults.CompletionTime = DateTime.Now;
                }
                return true;
            }

            //check for failure
            if(((int)type == 2233 || (int)type == 2105) && currentDuty.FailureCheckpoint!.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                //CheckpointResults.Add(new CheckpointResults(nextCheckpoint, false));
                _currentDutyResults.IsComplete = true;
                _currentDutyResults.CompletionTime = DateTime.Now;
                //_currentDutyResults.CheckpointResults.Add(new CheckpointResults(FailureCheckpoint, false));
                return true;
            }
            return false;
        }

        private bool ProcessCheckpointsRoulette(XivChatType type, uint senderId, SeString sender, SeString message) {
            if((int)type == 2105 || (int)type == 2233) {
                //check for save
                bool isSave = Regex.IsMatch(message.ToString(), @"^An unknown force", RegexOptions.IgnoreCase);
                //check for circles shift
                Match shiftMatch = Regex.Match(message.ToString(), @"(?<=The circles shift and (a |an )?)(the great gold whisker|altar airavata|altar mandragora|altar manticore|altar apanda|altar diresaur)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(shiftMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Gold, shiftMatch.Value, isSave);
                    return true;
                }
                //check for special summon
                Match specialMatch = Regex.Match(message.ToString(), @"^The .* retreats into the shadows", RegexOptions.IgnoreCase);
                if(specialMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Silver, null, isSave);
                    if(_currentDutyResults.CheckpointResults.Where(cr => cr.IsReached).Count() == _currentDuty.Checkpoints!.Count) {
                        _currentDutyResults.IsComplete = true;
                        _currentDutyResults.CompletionTime = DateTime.Now;
                    }
                    return true;
                }
                //check for lesser summon
                Match lesserMatch = Regex.Match(message.ToString(), @"(Hati|altar chimera|altar beast|altar dullahan|altar skatene|altar totem)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(lesserMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Lesser, lesserMatch.Value, isSave);
                    return true;
                }
                //check for greater summon
                Match greaterMatch = Regex.Match(message.ToString(), @"(The Winged|The Older One|altar kelpie|altar arachne)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(greaterMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Greater, greaterMatch.Value, isSave);
                    return true;
                }
                //check for elder summon
                Match elderMatch = Regex.Match(message.ToString(), @"(the great gold whisker|altar airavata|altar mandragora|altar manticore|altar apanda|altar diresaur)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(elderMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Elder, elderMatch.Value, isSave);
                    return true;
                }
                //enemy defeated
                if(Regex.IsMatch(message.ToString(), @"^The summon is dispelled.$", RegexOptions.IgnoreCase)) {
                    AddRouletteCheckpointResults(null);
                    if(_currentDutyResults.CheckpointResults.Where(cr => cr.IsReached).Count() == _currentDuty.Checkpoints!.Count) {
                        _currentDutyResults.IsComplete = true;
                        _currentDutyResults.CompletionTime = DateTime.Now;
                    }
                    return true;
                }
            }
            //failure
            if(((int)type == 2233 || (int)type == 2105) && _currentDuty!.FailureCheckpoint!.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                _currentDutyResults.IsComplete = true;
                _currentDutyResults.CompletionTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private void AddRouletteCheckpointResults(Summon? summon, string? monsterName = null, bool isSaved = false) {
            var size = _currentDutyResults.CheckpointResults.Count;
            _currentDutyResults.CheckpointResults.Add(new RouletteCheckpointResults(_currentDuty.Checkpoints[size], summon, monsterName, isSaved, true));
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).SummonType = summon;
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).Enemy = enemy;
        }

        private void EndDuty(bool success = false) {
            if(IsDutyInProgress()) {
                _currentDutyResults = null;
                Plugin.Save();
            }
        }

        private bool IsDutyInProgress() {
            return _currentDutyResults != null;
        }
    }
}
