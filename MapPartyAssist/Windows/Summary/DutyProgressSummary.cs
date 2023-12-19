using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Helper;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using MapPartyAssist.Windows.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Summary {
    internal class DutyProgressSummary {

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private Plugin _plugin;
        private StatsWindow _statsWindow;
        //native filters
        TimeFilter _timeFilter;
        SingleDutyFilter _dutyFilter;
        //duty id for draw time
        private int _dutyId => _dutyFilter.DutyId;
        private bool _isUpdating = false;

        ////linked filters
        //TimeFilter? _linkedTimeFilter;
        //DutyFilter? _linkedDutyFilter;

        private List<DutyResults> _dutyResults = new();
        private List<DutyResultsImport> _dutyResultsImports = new();


        //stats
        private int _totalGil = 0;
        private int _totalClears = 0;
        private int _totalWipes = 0;
        private int _totalRuns = 0;
        private int _runsSinceLastClear = 0;
        int[] _openChambers = new int[0];
        float[] _openChambersRates = new float[0];
        int[] _endChambers = new int[0];
        List<int> _chamberTotals = new();
        List<int> _ejectTotals = new();
        List<int> _clearSequence = new();
        List<int> _clearSequenceTotal = new();
        List<DutyResults> _clearDuties = new();
        Dictionary<Summon, int> _summonTotals = new();
        private int _saveCount = 0;
        private bool _hasGil;
        private bool _hasSequence;
        private bool _hasFloors;
        private bool _hasSummons;

        internal DutyProgressSummary(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
            //setup duty name combo
            List<string> dutyNames = new();
            foreach(var duty in _plugin.DutyManager.Duties) {
                dutyNames.Add(duty.Value.GetDisplayName());
            }

            _dutyFilter = new(_plugin, UpdateFiltersAndRefresh, true);
            _timeFilter = new(_plugin, UpdateFiltersAndRefresh);
            //Refresh();
        }

        public async void Refresh(List<DutyResults> dutyResults) {
            try {
                await _refreshLock.WaitAsync();
                UpdateDutyFilter();
                UpdateTimeFilter();

                if(_dutyFilter.DutyId == 0) {
                    _dutyResults = new();
                    return;
                }

                //var dutyResults = _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).Where(dr => dr.IsComplete).OrderBy(dr => dr.Time).ToList();
                //if(_plugin.Configuration.CurrentCharacterStatsOnly && !_plugin.GetCurrentPlayer().IsNullOrEmpty()) {
                //    dutyResults = dutyResults.Where(dr => dr.Players.Contains(_plugin.GetCurrentPlayer())).ToList();
                //}

                //if(_plugin.Configuration.DutyConfigurations[_dutyFilter.DutyId].OmitZeroCheckpoints) {
                //    dutyResults = dutyResults.Where(dr => dr.CheckpointResults.Count > 0).ToList();
                //}

                ////apply filters
                //foreach(var filter in _statsWindow.Filters) {
                //    switch(filter.GetType()) {
                //        case Type _ when filter.GetType() == typeof(DutyFilter):
                //            var dutyFilter = (DutyFilter)filter;
                //            dutyResults = dutyResults.Where(dr => dutyFilter.FilterState[dr.DutyId]).ToList();
                //            break;
                //        case Type _ when filter.GetType() == typeof(MapFilter):
                //            break;
                //        case Type _ when filter.GetType() == typeof(OwnerFilter):
                //            var ownerFilter = (OwnerFilter)filter;
                //            string trimmedOwner = ownerFilter.Owner.Trim();
                //            dutyResults = dutyResults.Where(dr => dr.Owner.Contains(trimmedOwner, StringComparison.OrdinalIgnoreCase)).ToList();
                //            break;
                //        case Type _ when filter.GetType() == typeof(PartyMemberFilter):
                //            var partyMemberFilter = (PartyMemberFilter)filter;
                //            if(partyMemberFilter.PartyMembers.Length <= 0) {
                //                break;
                //            }
                //            dutyResults = dutyResults.Where(dr => {
                //                bool allMatch = true;
                //                foreach(string partyMemberFilter in partyMemberFilter.PartyMembers) {
                //                    bool matchFound = false;
                //                    string partyMemberFilterTrimmed = partyMemberFilter.Trim();
                //                    foreach(string partyMember in dr.Players) {
                //                        if(partyMember.Contains(partyMemberFilterTrimmed, StringComparison.OrdinalIgnoreCase)) {
                //                            matchFound = true;
                //                            break;
                //                        }
                //                    }
                //                    allMatch = allMatch && matchFound;
                //                    if(!allMatch) {
                //                        return false;
                //                    }
                //                }
                //                return allMatch;
                //            }).ToList();
                //            break;
                //        case Type _ when filter.GetType() == typeof(TimeFilter):
                //            var timeFilter = (TimeFilter)filter;
                //            switch(timeFilter.StatRange) {
                //                case StatRange.Current:
                //                    dutyResults = dutyResults.Where(dr => dr.Map != null && !dr.Map.IsArchived).ToList();
                //                    break;
                //                case StatRange.PastDay:
                //                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalHours < 24).ToList();
                //                    break;
                //                case StatRange.PastWeek:
                //                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalDays < 7).ToList();
                //                    break;
                //                case StatRange.SinceLastClear:
                //                    //have to include duty info or this will be inaccurate!
                //                    var dutyFilter2 = (DutyFilter)_statsWindow.Filters.Where(f => f.GetType() == typeof(DutyFilter)).First();
                //                    var lastClear = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete).OrderBy(dr => dr.Time).ToList()
                //                        .Where(dr => dutyFilter2.FilterState[dr.DutyId] && dr.CheckpointResults.Count == _plugin.DutyManager.Duties[dr.DutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                //                    if(lastClear != null) {
                //                        dutyResults = dutyResults.Where(dr => dr.Time > lastClear.Time).ToList();
                //                    }
                //                    break;
                //                case StatRange.AllLegacy:
                //                    _dutyResultsImports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted && i.DutyId == _dutyFilter.DutyId).OrderBy(i => i.Time).ToList();
                //                    break;
                //                case StatRange.All:
                //                default:
                //                    break;
                //            }
                //            break;
                //        default:
                //            break;
                //    }
                //}

                //calculate stats
                var duty = _plugin.DutyManager.Duties[_dutyFilter.DutyId];
                bool isRoulette = duty.Structure == DutyStructure.Roulette;
                int numChambers = duty.ChamberCount;
                string successVerb = isRoulette ? "Complete" : "Open";
                string passiveSuccessVerb = isRoulette ? "Completed" : "Reached";
                string stageNoun = isRoulette ? "summon" : "chamber";
                string gateNoun = isRoulette ? "summon" : "gate";
                string chamberPattern = @"(?<=(Open|Complete) )[\d|final]+(?=(st|nd|rd|th)? (chamber|summon|trial))";

                _openChambers = new int[numChambers - 1];
                _openChambersRates = new float[numChambers - 1];
                _endChambers = new int[numChambers];

                _totalGil = 0;
                _runsSinceLastClear = 0;
                _totalClears = 0;
                _totalRuns = dutyResults.Count();
                _totalWipes = 0;

                _clearSequence = new();
                _clearDuties = new();

                _saveCount = 0;
                _summonTotals = new() {
                { Summon.Lesser, 0 },
                { Summon.Greater, 0 },
                { Summon.Elder, 0 },
                { Summon.Gold, 0 },
                { Summon.Silver, 0 }
            };

                //import specific stuff
                int currentImportIndex = 0;
                _hasSequence = true;
                _hasFloors = true;
                _hasGil = true;
                _hasSummons = true;

                var processImport = (DutyResultsImport import) => {
                    _totalRuns += (int)import.TotalRuns;
                    _totalClears += (int)import.TotalClears;
                    //check for gil
                    if(import.TotalGil != null) {
                        _totalGil += (int)import.TotalGil!;
                    } else {
                        _hasGil = false;
                    }
                    //checkfor checkpoint totals
                    if(import.CheckpointTotals != null) {
                        //var importOpenChambers = currentImport.CheckpointTotals.Where(cpt => Plugin.DutyManager.Duties[currentImport.DutyId].Checkpoints[])
                        for(int i = 0; i < import.CheckpointTotals.Count; i++) {
                            var currentCheckpointTotal = import.CheckpointTotals[i];
                            Match importChamberMatch = Regex.Match(duty.Checkpoints![i].Name, chamberPattern, RegexOptions.IgnoreCase);
                            if(importChamberMatch.Success) {
                                int importChamberNumber;
                                if(int.TryParse(importChamberMatch.Value, out importChamberNumber) && importChamberNumber != 1) {
                                    _openChambers[importChamberNumber - 2] += (int)currentCheckpointTotal;
                                } else if(importChamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
                                    _openChambers[_openChambers.Length - 1] += (int)currentCheckpointTotal;
                                }
                            }
                        }
                    } else {
                        _hasFloors = false;
                    }
                    //check for clear sequence
                    if(import.TotalClears == 0) {
                        _runsSinceLastClear += (int)import.TotalRuns;
                    } else if(import.ClearSequence != null) {
                        for(int i = 0; i < import.ClearSequence!.Count; i++) {
                            int curSequenceValue = (int)import.ClearSequence![i];
                            if(i == 0) {
                                _clearSequence.Add(_runsSinceLastClear + curSequenceValue);
                                _runsSinceLastClear = 0;
                            } else {
                                _clearSequence.Add(curSequenceValue);
                            }
                        }
                        _runsSinceLastClear += (int)import.RunsSinceLastClear!;
                    } else {
                        _hasSequence = false;
                    }
                };

                foreach(var result in dutyResults) {
                    //add import data
                    while(_timeFilter.StatRange == StatRange.AllLegacy && currentImportIndex < _dutyResultsImports.Count && _dutyResultsImports[currentImportIndex].Time < result.Time) {
                        processImport(_dutyResultsImports[currentImportIndex]);
                        currentImportIndex++;
                    }

                    _runsSinceLastClear++;
                    _totalGil += result.TotalGil;

                    //no checkpoint results
                    if(result.CheckpointResults.Count <= 0) {
                        _totalWipes++;
                        continue;
                    }

                    var lastCheckpoint = result.CheckpointResults.Last();

                    //check for clear
                    //string finalChamberCheckpoint = isRoulette ? "Defeat final summon" : "Clear final chamber";
                    string? finalChamberCheckpoint = duty.Checkpoints?.LastOrDefault()?.Name;
                    if(lastCheckpoint.Checkpoint.Name.Equals(finalChamberCheckpoint, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                        _clearSequence.Add(_runsSinceLastClear);
                        _clearDuties.Add(result);
                        _runsSinceLastClear = 0;
                        _totalClears++;
                    }

                    //check for death/abandon
                    if(lastCheckpoint.Checkpoint.Name.StartsWith(successVerb, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                        _totalWipes++;
                    }

                    //find the last reached door checkpoint
                    for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith(successVerb) || !lastCheckpoint.IsReached) && i <= result.CheckpointResults.Count; i++) {
                        lastCheckpoint = result.CheckpointResults.ElementAt(result.CheckpointResults.Count - i);
                    }

                    //did not find a valid checkpoint
                    if(lastCheckpoint == result.CheckpointResults[0] && (!lastCheckpoint.IsReached || !lastCheckpoint.Checkpoint.Name.StartsWith(successVerb))) {
                        continue;
                    }

                    Match chamberMatch = Regex.Match(lastCheckpoint.Checkpoint.Name, chamberPattern, RegexOptions.IgnoreCase);
                    int chamberNumber;
                    if(!chamberMatch.Success) {
                        //did not find a match
                        continue;
                    } else if(chamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
                        //endChambers[endChambers.Length - 1]++;
                        for(int i = 0; i < _openChambers.Length; i++) {
                            _openChambers[i]++;
                        }
                    } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
                        //endChambers[chamberNumber - 1]++;
                        for(int i = 0; i < chamberNumber - 1; i++) {
                            _openChambers[i]++;
                        }
                    } else {
                        continue;
                    }
                }
                //check for remaining imports
                if(_timeFilter.StatRange == StatRange.AllLegacy && currentImportIndex != _dutyResultsImports.Count) {
                    while(currentImportIndex < _dutyResultsImports.Count) {
                        processImport(_dutyResultsImports[currentImportIndex]);
                        currentImportIndex++;
                    }
                }

                //calculate chamber rates
                for(int i = 0; i < numChambers - 1; i++) {
                    if(i == 0) {
                        _openChambersRates[i] = _totalRuns > 0 ? (float)_openChambers[i] / _totalRuns : 0f;
                    } else {
                        _openChambersRates[i] = _openChambers[i - 1] > 0 ? (float)_openChambers[i] / _openChambers[i - 1] : 0f;
                    }
                }

                //calculate endChambers
                for(int i = 0; i < _endChambers.Length; i++) {
                    if(i == 0) {
                        _endChambers[i] = _totalRuns - _openChambers[i];
                    } else if(i == _endChambers.Length - 1) {
                        _endChambers[i] = _openChambers[i - 1];
                    } else {
                        _endChambers[i] = _openChambers[i - 1] - _openChambers[i];
                    }
                }

                //summon data
                if(_plugin.DutyManager.Duties[_dutyId].Structure == DutyStructure.Roulette) {
                    //check import data
                    if(_timeFilter.StatRange == StatRange.AllLegacy) {
                        foreach(var import in _dutyResultsImports) {
                            if(import.SummonTotals != null) {
                                foreach(var summonTotal in import.SummonTotals) {
                                    _summonTotals[summonTotal.Key] += (int)import.SummonTotals[summonTotal.Key];
                                }
                            } else {
                                _hasSummons = false;
                            }
                        }
                    }

                    foreach(var result in dutyResults) {
                        foreach(var checkpoint in result.CheckpointResults.Where(c => c.IsReached)) {
                            if(checkpoint.SummonType is null) {
                                continue;
                            }
                            _summonTotals[(Summon)checkpoint.SummonType!]++;
                            if(checkpoint.IsSaved) {
                                _saveCount++;
                            }
                        }
                    }
                }
                _dutyResults = dutyResults;
            } finally {
                _refreshLock.Release();
            }
        }

        public void UpdateFiltersAndRefresh() {
            //set filters
            var dutyFilter = (DutyFilter)_statsWindow.Filters.Where(f => f.GetType() == typeof(DutyFilter)).First();
            foreach(var duty in dutyFilter.FilterState) {
                dutyFilter.FilterState[duty.Key] = false;
            }
            if(_dutyFilter.DutyId != 0) {
                dutyFilter.FilterState[_dutyFilter.DutyId] = true;
            }
            var timeFilter = (TimeFilter)_statsWindow.Filters.Where(f => f.GetType() == typeof(TimeFilter)).First();
            timeFilter.StatRange = _timeFilter.StatRange;

            _statsWindow.Refresh();
        }

        private void UpdateDutyFilter() {
            var dutyFilter = (DutyFilter)_statsWindow.Filters.Where(f => f.GetType() == typeof(DutyFilter)).First();
            int numSelected = 0;
            int lastDutyId = 0;
            foreach(var duty in dutyFilter.FilterState) {
                if(duty.Value) {
                    numSelected++;
                    lastDutyId = duty.Key;
                }
            }
            if(numSelected != 1) {
                _dutyFilter.DutyId = 0;
            } else {
                _dutyFilter.DutyId = lastDutyId;
            }
        }

        private void UpdateTimeFilter() {
            _timeFilter.StatRange = ((TimeFilter)_statsWindow.Filters.Where(f => f.GetType() == typeof(TimeFilter)).First()).StatRange;
        }

        public void Draw() {
            //ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2f);
            ImGui.SetNextItemWidth(float.Max(ImGui.GetContentRegionAvail().X / 2f, _statsWindow.SizeConstraints!.Value.MinimumSize.X));
            _dutyFilter.Draw();
            //ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2f);
            ImGui.SetNextItemWidth(float.Max(ImGui.GetContentRegionAvail().X / 2f, _statsWindow.SizeConstraints!.Value.MinimumSize.X));
            _timeFilter.Draw();

            if(_timeFilter.StatRange == StatRange.AllLegacy) {
                if(ImGui.Button("Manage Imports")) {
                    //if(!_viewImportsWindow.IsOpen) {
                    //    _viewImportsWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                    //    _viewImportsWindow.IsOpen = true;
                    //}
                    //_viewImportsWindow.BringToFront();
                }
            }

            //race condition with refresh
            if(_refreshLock.CurrentCount == 0) return;
            try {
                _refreshLock.Wait();
                if(_dutyId != 0 && !_isUpdating) {
                    //todo these calculations should happen in same thread as refresh
                    ProgressTable(_dutyResults, _dutyId);
                    if(_plugin.DutyManager.Duties[_dutyId].Structure == DutyStructure.Roulette) {
                        SummonTable(_dutyResults, _dutyId);
                    }
                }
            } finally {
                _refreshLock.Release();
            }
        }

        private void ProgressTable(List<DutyResults> dutyResults, int dutyId) {
            //determine number of chambers from duty id and whether this is doors or roulette
            var duty = _plugin.DutyManager.Duties[dutyId];
            bool isRoulette = duty.Structure == DutyStructure.Roulette;
            int numChambers = duty.ChamberCount;
            string successVerb = isRoulette ? "Complete" : "Open";
            string passiveSuccessVerb = isRoulette ? "Completed" : "Reached";
            string stageNoun = isRoulette ? "summon" : "chamber";
            string gateNoun = isRoulette ? "summon" : "gate";

            //Draw
            if(ImGui.BeginTable($"##{dutyId}-Table", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
                ImGui.TableSetupColumn("checkpoint", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if(_timeFilter.StatRange != StatRange.AllLegacy || _hasGil) {
                    ImGui.Text("Total gil earned: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_totalGil.ToString("N0")}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
                if(_timeFilter.StatRange != StatRange.SinceLastClear) {
                    ImGui.Text("Total clears: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_totalClears}");
                    ImGui.TableNextColumn();
                    if(_totalRuns > 0) {
                        ImGui.Text($"{string.Format("{0:P}%", (double)_totalClears / _totalRuns)}");
                    }
                    ImGui.TableNextColumn();
                }
                if(_timeFilter.StatRange != StatRange.AllLegacy && _plugin.Configuration.DutyConfigurations[_dutyFilter.DutyId].DisplayDeaths) {
                    ImGui.Text("Total wipes:");
                    Tooltip("Inferred from last checkpoint.");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_totalWipes}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
                ImGui.Text("Total runs:");
                ImGui.TableNextColumn();
                ImGui.Text($"{_totalRuns}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                if(_timeFilter.StatRange != StatRange.AllLegacy || _hasFloors) {
                    if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.Last) {
                        for(int i = 0; i < _endChambers.Length; i++) {
                            if(i == numChambers - 1) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                var ordinalIndex = isRoulette ? i + 2 : i + 1;
                                ImGui.Text($"Ejected at {StringHelper.AddOrdinal(ordinalIndex)} {gateNoun}:");
                                Tooltip("Also includes preceding wipes, abandons \nand timeouts.");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{_endChambers[i]}");
                            ImGui.TableNextColumn();
                            if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && i != _endChambers.Length - 1 && ((i == 0 && _totalRuns != 0) || (i != 0 && _openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)1d - _openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && _totalRuns != 0) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)_endChambers[i] / _totalRuns)}");
                                Tooltip("Calculated from total runs.");
                            }
                            ImGui.TableNextColumn();
                        }
                    } else if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.All) {
                        for(int i = 0; i < _openChambers.Length; i++) {
                            if(i == numChambers - 2) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                ImGui.Text($"{passiveSuccessVerb} {StringHelper.AddOrdinal(i + 2)} {stageNoun}:");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{_openChambers[i]}");
                            ImGui.TableNextColumn();
                            if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && ((i == 0 && _totalRuns != 0) || (i != 0 && _openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", _openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && _totalRuns != 0) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)_openChambers[i] / _totalRuns)}");
                                Tooltip("Calculated from total runs.");
                            }
                            ImGui.TableNextColumn();
                        }
                    }
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                if(_timeFilter.StatRange != StatRange.AllLegacy || _hasSequence) {
                    //todo make this a configuration variable
                    if(_plugin.Configuration.DutyConfigurations[_dutyFilter.DutyId].DisplayClearSequence) {
                        for(int i = 0; i < _clearSequence.Count; i++) {
                            if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                                ImGui.Text($"{StringHelper.AddOrdinal(i + 1)} clear:");
                                Tooltip(i == 0 ? "Runs since start." : "Runs since preceding clear.");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{_clearSequence[i].ToString().PadRight(3)}");
                            } else {
                                ImGui.Text($"{StringHelper.AddOrdinal(i + 1)} clear (total):");
                                Tooltip("Total runs at time.");
                                ImGui.TableNextColumn();
                                int clearTotal = _clearSequence[i];
                                _clearSequence.GetRange(0, i).ForEach(x => clearTotal += x);
                                ImGui.Text($"{clearTotal.ToString().PadRight(3)}");
                            }

                            if(_timeFilter.StatRange != StatRange.AllLegacy) {
                                if(ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"{_clearDuties[i].CompletionTime.ToString()}");
                                    ImGui.Text($"{_clearDuties[i].Owner}");
                                    ImGui.EndTooltip();
                                }
                            }
                            ImGui.TableNextColumn();
                            ImGui.TableNextColumn();
                        }
                    }

                    if(_totalClears > 0 && _plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                        if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                            ImGui.Text("Runs since last clear: ");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{_runsSinceLastClear}");
                            ImGui.TableNextColumn();
                            ImGui.TableNextColumn();
                        }
                    }
                }
                ImGui.EndTable();
            }
        }

        private void SummonTable(List<DutyResults> dutyResults, int dutyId) {
            if(_hasSummons) {
                if(ImGui.BeginTable($"##{dutyId}-SummonTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
                    ImGui.TableSetupColumn("summon", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                    ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                    ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Lesser summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_summonTotals[Summon.Lesser]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Greater summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_summonTotals[Summon.Greater]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Elder summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_summonTotals[Summon.Elder]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Circle shifts: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_summonTotals[Summon.Gold]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Abominations: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_summonTotals[Summon.Silver]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.EndTable();
                }
            }
        }

        private void Tooltip(string message, bool isTutorial = true) {
            if(ImGui.IsItemHovered() && (!isTutorial || _plugin.Configuration.ShowStatsWindowTooltips)) {
                ImGui.BeginTooltip();
                ImGui.Text(message);
                ImGui.EndTooltip();
            }
        }
    }
}
