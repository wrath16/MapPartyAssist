using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Services {
    //internal service for managing duties and duty results
    internal class DutyManager : IDisposable {

        private Plugin _plugin;
        private bool _firstTerritoryChange;
        private DutyResults? _currentDutyResults;
        private Duty? _currentDuty {
            get {
                return IsDutyInProgress() ? Duties[_currentDutyResults!.DutyId] : null;
            }
        }

        internal readonly Dictionary<int, Duty> Duties = new Dictionary<int, Duty>() {
            { 179 , new Duty(179, "the aquapolis", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty"),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens"),
                new Checkpoint("Clear 2nd chamber", "The cages are empty"),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens"),
                new Checkpoint("Clear 3rd chamber", "The cages are empty"),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens"),
                new Checkpoint("Clear 4th chamber", "The cages are empty"),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens"),
                new Checkpoint("Clear 5th chamber", "The cages are empty"),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens"),
                new Checkpoint("Clear 6th chamber", "The cages are empty"),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens"),
                new Checkpoint("Clear final chamber", "The cages are empty")
            }, new Checkpoint("Failure", "The Aquapolis has ended")) },
            { 268, new Duty(268, "the lost canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty"),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens"),
                new Checkpoint("Clear 2nd chamber", "The cages are empty"),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens"),
                new Checkpoint("Clear 3rd chamber", "The cages are empty"),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens"),
                new Checkpoint("Clear 4th chamber", "The cages are empty"),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens"),
                new Checkpoint("Clear 5th chamber", "The cages are empty"),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens"),
                new Checkpoint("Clear 6th chamber", "The cages are empty"),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens"),
                new Checkpoint("Clear final chamber", "The cages are empty")
            }, new Checkpoint("Failure", "The Lost Canals of Uznair has ended")) },
            { 276 , new Duty(276, "the hidden canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty"),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens"),
                new Checkpoint("Clear 2nd chamber", "The cages are empty"),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens"),
                new Checkpoint("Clear 3rd chamber", "The cages are empty"),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens"),
                new Checkpoint("Clear 4th chamber", "The cages are empty"),
                new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens"),
                new Checkpoint("Clear 5th chamber", "The cages are empty"),
                new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens"),
                new Checkpoint("Clear 6th chamber", "The cages are empty"),
                new Checkpoint("Open final chamber", "The gate to the final chamber opens"),
                new Checkpoint("Clear final chamber", "The cages are empty")
            }, new Checkpoint("Failure", "The Hidden Canals of Uznair has ended")) },
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
            }, new Checkpoint("Failure", "The Shifting Altars of Uznair has ended"),
                new string[] {"altar beast", "altar chimera", "altar dullahan", "altar skatene", "altar totem", "hati" },
                new string[] {"altar arachne", "altar kelpie", "the older one", "the winged" },
                new string[] {"altar airavata", "altar mandragora", "the great gold whisker" },
                new string[] {"altar apanda", "altar diresaur", "altar manticore" }) },
            { 688 , new Duty(688, "the dungeons of lyhe ghiah", DutyStructure.Doors, 5, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty"),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens"),
                new Checkpoint("Clear 2nd chamber", "The cages are empty"),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens"),
                new Checkpoint("Clear 3rd chamber", "The cages are empty"),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens"),
                new Checkpoint("Clear 4th chamber", "The cages are empty"),
                new Checkpoint("Open final chamber", @"(The gate to Condemnation( is)? open(s)?|The gate to the final chamber opens)"),
                new Checkpoint("Clear final chamber", "The cages are empty")
            }, new Checkpoint("Failure", "The Dungeons of Lyhe Ghiah has ended")) },
            { 745 , new Duty(745, "the shifting oubliettes of lyhe ghiah", DutyStructure.Roulette, 5, new() {
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
            }, new Checkpoint("Failure", "The Shifting Oubliettes of Lyhe Ghiah has ended."),
                new string[] {"secret undine", "secret djinn", "secret swallow", "secret serpent", "secret cladoselache", "secret worm" },
                new string[] {"greedy pixie", "secret basket", "secret pegasus", "secret porxie" },
                new string[] {"secret korrigan", "secret keeper", "fuath troublemaker" },
                new string[] {"daen ose the avaricious" }) },
            { 819 , new Duty(819, "the excitatron 6000", DutyStructure.Doors, 5, new() {
                new Checkpoint("Clear 1st chamber", "The cages are empty"),
                new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens"),
                new Checkpoint("Clear 2nd chamber", "The cages are empty"),
                new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens"),
                new Checkpoint("Clear 3rd chamber", "The cages are empty"),
                new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens"),
                new Checkpoint("Clear 4th chamber", "The cages are empty"),
                new Checkpoint("Open final chamber", @"(The gate to Condemnation( is)? open(s)?|The gate to the final chamber opens)"),
                new Checkpoint("Clear final chamber", "The cages are empty")
            }, new Checkpoint("Failure", "The Excitatron 6000 has ended")) },
            { 909 , new Duty(909, "the shifting gymnasion agonon", DutyStructure.Roulette, 5, new() {
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
            }, new Checkpoint("Failure", "The Shifting Gymnasion Agonon has ended"),
                new string[] {"gymnasiou megakantha", "gymnasiou triton", "gymnasiou satyros", "gymnasiou leon", "gymnasiou pithekos", "gymnasiou tigris" },
                new string[] {"gymnasiou styphnolobion", "gymnasiou meganereis", "gymnasiou sphinx", "gymnasiou acheloios" },
                new string[] {"lyssa chrysine", "lampas chrysine", "gymnasiou mandragoras" },
                new string[] {"hippomenes", "phaethon", "narkissos" }) }
        };

        public DutyManager(Plugin plugin) {
            _plugin = plugin;
            _plugin.DutyState.DutyStarted += OnDutyStart;
            _plugin.DutyState.DutyCompleted += OnDutyCompleted;
            _plugin.DutyState.DutyWiped += OnDutyWiped;
            _plugin.DutyState.DutyCompleted += OnDutyRecommenced;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            _plugin.ChatGui.ChatMessage += OnChatMessage;

            //attempt to pickup
            if(_plugin.ClientState.IsLoggedIn && _plugin.IsEnglishClient() && !IsDutyInProgress()) {
                PickupLastDuty();
            }
        }

        public void Dispose() {
#if DEBUG
            _plugin.Log.Debug("disposing duty manager");
#endif
            _plugin.DutyState.DutyStarted -= OnDutyStart;
            _plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            _plugin.DutyState.DutyWiped -= OnDutyWiped;
            _plugin.DutyState.DutyCompleted -= OnDutyRecommenced;
            _plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            _plugin.ChatGui.ChatMessage -= OnChatMessage;
        }

        //attempt to start new duty results
        //returns true if succesfully started
        private bool StartNewDuty(int dutyId) {

            //abort if not in English-language client
            if(!_plugin.IsEnglishClient()) {
                return false;
            }

            if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null) {
                var lastMap = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted).OrderBy(m => m.Time).ToList().Last();
                _plugin.Log.Information($"Starting new duty results for duty id: {dutyId}");
                //_currentDutyResults = new DutyResults(dutyId, Duties[dutyId].Name, _plugin.CurrentPartyList, "");
                _currentDutyResults = new DutyResults {
                    DutyId = dutyId,
                    DutyName = Duties[dutyId].Name,
                    Players = _plugin.CurrentPartyList.Keys.ToArray(),
                    Owner = ""
                };
                //check last map, 10 min fallback for linking to most recent map
                if((DateTime.Now - lastMap.Time).TotalMinutes < 10) {
                    _currentDutyResults.Map = lastMap;
                    _currentDutyResults.Owner = lastMap.Owner!;
                    lastMap.IsPortal = true;
                    lastMap.DutyName = Duties[dutyId].GetDisplayName();
                    _plugin.StorageManager.UpdateMap(lastMap);
                } else {
                    _currentDutyResults.Map = null;
                    _currentDutyResults.Owner = "";
                }

                _plugin.StorageManager.AddDutyResults(_currentDutyResults);
                _plugin.Save();
                return true;
            }
            return false;
        }

        //attempt to pickup duty that did not complete
        //returns true if duty results was succesfully picked up
        private bool PickupLastDuty(bool toSave = true) {
            int dutyId = _plugin.Functions.GetCurrentDutyId();
            var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            var lastDutyResults = _plugin.StorageManager.GetDutyResults().Query().OrderBy(dr => dr.Time).ToList().LastOrDefault();
            if(lastDutyResults != null) {
                TimeSpan lastTimeDiff = DateTime.Now - lastDutyResults.Time;
                //pickup if duty is valid, and matches the last duty which was not completed and not more than an hour has elapsed (fallback)
                if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null && lastDutyResults.DutyId == dutyId && !lastDutyResults.IsComplete && !_firstTerritoryChange && lastTimeDiff.TotalHours < 1) {
                    _plugin.Log.Information($"re-picking up last duty results id:{lastDutyResults.Id.ToString()}");
                    _currentDutyResults = lastDutyResults;
                    _currentDutyResults.IsPickup = true;

                    _plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                    //if(toSave) {
                    //    Plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                    //    Plugin.Save();
                    //}

                    return true;
                } else {
                    return false;
                }
            }
            return false;
        }

        //refreshes current duty results from DB. Useful if duty results is edited manually while running
        //returns true if successful
        internal bool RefreshCurrentDutyResults() {
            if(!IsDutyInProgress()) {
                return false;
            }
            var storageDutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Id.Equals(_currentDutyResults!.Id)).FirstOrDefault();
            if(storageDutyResults == null) {
                return false;
            }
            _currentDutyResults = storageDutyResults;
            return true;
        }

        internal List<DutyResults> GetRecentDutyResultsList(int? dutyId = null) {
            return _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).Where(dr => dr.Map != null && !dr.Map.IsArchived && !dr.Map.IsDeleted && dr.IsComplete && (dutyId == null || dr.DutyId == dutyId)).ToList();
        }

        internal Duty? GetDutyByName(string name) {
            foreach(var duty in Duties) {
                if(duty.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                    return duty.Value;
                }
            }
            return null;
        }

        //validate duty results and fill in missing data if possible
        private bool ValidateUpdateDutyResults(DutyResults dutyResults) {
            //check for no players
            if(dutyResults.Players == null || dutyResults.Players.Length <= 0) {
                _plugin.Log.Warning($"No players on duty results {dutyResults.Id.ToString()}");
                if(dutyResults.Owner.IsNullOrEmpty()) {
                    _plugin.Log.Warning($"No owner on duty results {dutyResults.Id.ToString()}");
                } else {
                    //dutyResults.Players = new[] { dutyResults.Owner };
                }
                dutyResults.Players = _plugin.CurrentPartyList.Keys.ToArray();
                return false;
            }
            return true;
        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            _plugin.Log.Verbose($"Duty has started with territory id: {territoryId} name: {_plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = _plugin.Functions.GetCurrentDutyId();
            _plugin.Log.Verbose($"Current duty ID: {dutyId}");
            var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            _plugin.Log.Verbose($"Duty Name: {duty?.Name}");

            //check if duty is ongoing to attempt to pickup...
            _plugin.Log.Verbose($"Current duty ongoing? {_currentDutyResults != null}");
        }

        private void OnDutyCompleted(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty completed!");
            //EndDuty();
        }

        private void OnDutyWiped(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty wiped!");
            //EndDuty();
        }

        private void OnDutyRecommenced(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty recommenced!");
            //EndDuty();
        }

        private void OnTerritoryChanged(ushort territoryId) {
            var dutyId = _plugin.Functions.GetCurrentDutyId();
            var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            _plugin.Log.Verbose($"Territory changed: {territoryId}, Current duty: {_plugin.Functions.GetCurrentDutyId()}");

            if(IsDutyInProgress()) {
                //clear current duty if it was completed successfully or clear as a fallback. attempt to pickup otherwise on disconnect
                if(_currentDutyResults!.IsComplete || dutyId != _currentDutyResults.DutyId) {
                    EndCurrentDuty();
                }
            } else if(duty != null) {
                //attempt to pickup if game closed without completing properly
                if(!PickupLastDuty(true)) {
                    StartNewDuty(dutyId);
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
                        _plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                        _plugin.Save();
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
                    _plugin.StorageManager.UpdateDutyResults(_currentDutyResults!);
                    _plugin.Save();
                }
            }
        }

        //return true if updates made
        private bool ProcessCheckpointsDoors(XivChatType type, uint senderId, SeString sender, SeString message) {
            var nextCheckpoint = _currentDuty!.Checkpoints![_currentDutyResults!.CheckpointResults.Count];
            if(((int)type == 2233 || (int)type == 2105) && Regex.IsMatch(message.ToString(), nextCheckpoint.Message!, RegexOptions.IgnoreCase)) {
                _currentDutyResults.CheckpointResults.Add(new CheckpointResults(nextCheckpoint, true));
                //if all checkpoints reached, set to duty complete
                if(_currentDutyResults!.CheckpointResults.Where(cr => cr.IsReached).Count() == _currentDuty.Checkpoints!.Count) {
                    _currentDutyResults.IsComplete = true;
                    _currentDutyResults.CompletionTime = DateTime.Now;
                }
                return true;
            }

            //check for failure
            if(((int)type == 2233 || (int)type == 2105) && Regex.IsMatch(message.ToString(), _currentDuty!.FailureCheckpoint!.Message!, RegexOptions.IgnoreCase)) {
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
                Match shiftMatch = Regex.Match(message.ToString(), @"(?<=The circles shift and (a |an )?)" + _currentDuty!.GetSummonPatternString(Summon.Elder) + @"(?=,? appears?)", RegexOptions.IgnoreCase);
                if(shiftMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Gold, shiftMatch.Value, isSave);
                    return true;
                }
                //check for special summon
                Match specialMatch = Regex.Match(message.ToString(), @"^The .* retreats into the shadows", RegexOptions.IgnoreCase);
                if(specialMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Silver, null, isSave);
                    //add next checkpoint as well
                    AddRouletteCheckpointResults(null);
                    if(_currentDutyResults!.CheckpointResults.Where(cr => cr.IsReached).Count() == _currentDuty.Checkpoints!.Count) {
                        _currentDutyResults.IsComplete = true;
                        _currentDutyResults.CompletionTime = DateTime.Now;
                    }
                    return true;
                }
                //check for lesser summon
                Match lesserMatch = Regex.Match(message.ToString(), _currentDuty.GetSummonPatternString(Summon.Lesser) + @"(?=,? appears?)", RegexOptions.IgnoreCase);
                if(lesserMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Lesser, lesserMatch.Value, isSave);
                    return true;
                }
                //check for greater summon
                Match greaterMatch = Regex.Match(message.ToString(), _currentDuty.GetSummonPatternString(Summon.Greater) + @"(?=,? appears?)", RegexOptions.IgnoreCase);
                if(greaterMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Greater, greaterMatch.Value, isSave);
                    return true;
                }
                //check for elder summon
                Match elderMatch = Regex.Match(message.ToString(), _currentDuty.GetSummonPatternString(Summon.Elder) + "(?=,? appears?)", RegexOptions.IgnoreCase);
                if(elderMatch.Success) {
                    AddRouletteCheckpointResults(Summon.Elder, elderMatch.Value, isSave);
                    return true;
                }
                //enemy defeated
                if(Regex.IsMatch(message.ToString(), @"^(The summon is dispelled|The trial is passed)", RegexOptions.IgnoreCase)) {
                    AddRouletteCheckpointResults(null);
                    if(_currentDutyResults!.CheckpointResults.Where(cr => cr.IsReached).Count() == _currentDuty.Checkpoints!.Count) {
                        _currentDutyResults.IsComplete = true;
                        _currentDutyResults.CompletionTime = DateTime.Now;
                    }
                    return true;
                }

                //check for unknown enemy
                //Match unknownMatch = Regex.Match(message.ToString(), ".*(?=,? appears?)", RegexOptions.IgnoreCase);
                //(?<=\ban?\b ).*(?=,? appears\.*\!*$)
            }
            //failure
            if(((int)type == 2233 || (int)type == 2105) && Regex.IsMatch(message.ToString(), _currentDuty!.FailureCheckpoint!.Message!, RegexOptions.IgnoreCase)) {
                _currentDutyResults!.IsComplete = true;
                _currentDutyResults.CompletionTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private void AddRouletteCheckpointResults(Summon? summon, string? monsterName = null, bool isSaved = false) {
            int size = _currentDutyResults!.CheckpointResults.Count;
            //_currentDutyResults.CheckpointResults.Add(new RouletteCheckpointResults(_currentDuty!.Checkpoints![size], summon, monsterName, isSaved, true));
            _currentDutyResults.CheckpointResults.Add(new RouletteCheckpointResults {
                Checkpoint = _currentDuty!.Checkpoints![size],
                Time = DateTime.Now,
                SummonType = summon,
                MonsterName = monsterName,
                IsSaved = isSaved,
                IsReached = true
            });

            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).SummonType = summon;
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).Enemy = enemy;
        }

        private void EndCurrentDuty() {
            if(IsDutyInProgress()) {
                _plugin.Log.Information($"Ending duty results id: {_currentDutyResults!.Id}");
                _currentDutyResults!.IsComplete = true;
                if(_currentDutyResults.CompletionTime.Ticks == 0) {
                    _currentDutyResults.CompletionTime = DateTime.Now;
                }
                //check for malformed/missing data
                ValidateUpdateDutyResults(_currentDutyResults);
                _currentDutyResults = null;
                _plugin.Save();
            }
        }

        private bool IsDutyInProgress() {
            return _currentDutyResults != null;
        }
    }
}
