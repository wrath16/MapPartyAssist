using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows {

    public enum StatRange {
        Current,
        PastDay,
        PastWeek,
        SinceLastClear,
        All,
        AllLegacy
    }

    internal class StatsWindow : Window {

        private Plugin _plugin;
        private ViewDutyResultsImportsWindow _viewImportsWindow;
        private LootResultsWindow _lootResultsWindow;

        private StatRange _statRange = StatRange.All;
        private readonly string[] _rangeCombo = { "Current", "Last Day", "Last Week", "Since last clear", "All-Time", "All-Time with imported data" };
        private int _dutyId = 276;
        private int _selectedDuty = 2;
        private readonly int[] _dutyIdCombo = { 179, 268, 276, 586, 688, 745, 819, 909 };
        private readonly string[] _dutyNameCombo;
        private List<DutyResults> _dutyResults = new();
        private List<DutyResultsImport> _dutyResultsImports = new();
        private string _partyMemberFilter = "";
        private string _ownerFilter = "";

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        internal StatsWindow(Plugin plugin) : base("Treasure Dungeon Stats") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(600, 1000)
            };
            _plugin = plugin;
            _viewImportsWindow = new ViewDutyResultsImportsWindow(plugin, this);
            _viewImportsWindow.IsOpen = false;
            _plugin.WindowSystem.AddWindow(_viewImportsWindow);

            _lootResultsWindow = new LootResultsWindow(plugin);
            _lootResultsWindow.IsOpen = false;
            _plugin.WindowSystem.AddWindow(_lootResultsWindow);

            //setup duty name combo
            _dutyNameCombo = new string[_dutyIdCombo.Length];
            for(int i = 0; i < _dutyIdCombo.Length; i++) {
                _dutyNameCombo[i] = _plugin.DutyManager.Duties[_dutyIdCombo[i]].GetDisplayName();
            }
        }

        public Task Refresh() {
            return Task.Run(async () => {
                try {
                    await _refreshLock.WaitAsync();
                    if(_statRange == StatRange.Current) {
                        //_dutyResults = Plugin.DutyManager.GetRecentDutyResultsList(_dutyId);
                        _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).Where(dr => dr.Map != null && !dr.Map.IsArchived && !dr.Map.IsDeleted && dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                    } else if(_statRange == StatRange.PastDay) {
                        _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToEnumerable().Where(dr => (DateTime.Now - dr.Time).TotalHours < 24).ToList();
                    } else if(_statRange == StatRange.PastWeek) {
                        _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToEnumerable().Where(dr => (DateTime.Now - dr.Time).TotalDays < 7).ToList();
                    } else if(_statRange == StatRange.All) {
                        _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                    } else if(_statRange == StatRange.AllLegacy) {
                        _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                        _dutyResultsImports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted && i.DutyId == _dutyId).OrderBy(i => i.Time).ToList();
                    } else if(_statRange == StatRange.SinceLastClear) {
                        var lastClear = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList().Where(dr => dr.CheckpointResults.Count == _plugin.DutyManager.Duties[_dutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                        if(lastClear != null) {
                            _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId && dr.Time > lastClear.Time).OrderBy(dr => dr.Time).ToList();
                        } else {
                            _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                        }
                    }

                    if(_plugin.Configuration.CurrentCharacterStatsOnly && !_plugin.GetCurrentPlayer().IsNullOrEmpty()) {
                        _dutyResults = _dutyResults.Where(dr => dr.Players.Contains(_plugin.GetCurrentPlayer())).ToList();
                    }

                    if(_plugin.Configuration.DutyConfigurations[_dutyId].OmitZeroCheckpoints) {
                        _dutyResults = _dutyResults.Where(dr => dr.CheckpointResults.Count > 0).ToList();
                    }

                    if(_plugin.Configuration.ShowAdvancedFilters) {
                        //this is duplicated from DutyResultsWindow...
                        string[] partyMemberFilters = _partyMemberFilter.Split(",");
                        _dutyResults = _dutyResults.Where(dr => dr.Owner.Contains(_ownerFilter, StringComparison.OrdinalIgnoreCase)).Where(dr => {
                            bool allMatch = true;
                            foreach(string partyMemberFilter in partyMemberFilters) {
                                bool matchFound = false;
                                string partyMemberFilterTrimmed = partyMemberFilter.Trim();
                                foreach(string partyMember in dr.Players) {
                                    if(partyMember.Contains(partyMemberFilterTrimmed, StringComparison.OrdinalIgnoreCase)) {
                                        matchFound = true;
                                        break;
                                    }
                                }
                                allMatch = allMatch && matchFound;
                                if(!allMatch) {
                                    return false;
                                }
                            }
                            return allMatch;
                        }).ToList();
                    }
                    await _viewImportsWindow.Refresh();
                    await _lootResultsWindow.Refresh(_dutyResults);
                } finally {
                    _refreshLock.Release();
                }
            });
        }

        public override void OnClose() {
            _viewImportsWindow.IsOpen = false;
            _lootResultsWindow.IsOpen = false;
            base.OnClose();
        }

        public override void Draw() {
            if(ImGui.Combo($"Duty##DutyCombo", ref _selectedDuty, _dutyNameCombo, _dutyNameCombo.Length)) {
                _dutyId = _dutyIdCombo[_selectedDuty];
                //UpdateDutyResults();
                _plugin.Save();
            }
            int statRangeToInt = (int)_statRange;
            if(ImGui.Combo($"Data Range##includesCombo", ref statRangeToInt, _rangeCombo, _rangeCombo.Length)) {
                _statRange = (StatRange)statRangeToInt;
                //UpdateDutyResults();
                _plugin.Save();
            }
            if(_plugin.Configuration.ShowAdvancedFilters) {
                if(ImGui.InputText($"Map Owner", ref _ownerFilter, 50)) {
                    _plugin.Save();
                }
                if(ImGui.InputText($"Party Members", ref _partyMemberFilter, 100)) {
                    _plugin.Save();
                }
            }

            if(_statRange == StatRange.AllLegacy) {
                if(ImGui.Button("Manage Imports")) {
                    if(!_viewImportsWindow.IsOpen) {
                        _viewImportsWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                        _viewImportsWindow.IsOpen = true;
                    }
                    _viewImportsWindow.BringToFront();
                }
            }

            //todo these calculations should happen in same thread as refresh
            ProgressTable(_dutyResults, _dutyId);
            if(_plugin.DutyManager.Duties[_dutyId].Structure == DutyStructure.Roulette) {
                SummonTable(_dutyResults, _dutyId);
            }

            if(ImGui.Button("Loot")) {
                if(!_lootResultsWindow.IsOpen) {
                    _lootResultsWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                    _lootResultsWindow.IsOpen = true;
                }
                _lootResultsWindow.BringToFront();
            }
        }

        public override void PreDraw() {
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
            string chamberPattern = @"(?<=(Open|Complete) )[\d|final]+(?=(st|nd|rd|th)? (chamber|summon|trial))";

            int[] openChambers = new int[numChambers - 1];
            float[] openChambersRates = new float[numChambers - 1];
            int[] endChambers = new int[numChambers];

            int totalGil = 0;
            int runsSinceLastClear = 0;
            int totalClears = 0;
            int totalRuns = dutyResults.Count();
            int totalDeaths = 0;

            List<int> clearSequence = new();
            List<DutyResults> clearDuties = new();

            //import specific stuff
            int currentImportIndex = 0;
            bool hasSequence = true;
            bool hasFloors = true;
            bool hasGil = true;

            var processImport = (DutyResultsImport import) => {
                totalRuns += (int)import.TotalRuns;
                totalClears += (int)import.TotalClears;
                //check for gil
                if(import.TotalGil != null) {
                    totalGil += (int)import.TotalGil!;
                } else {
                    hasGil = false;
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
                                openChambers[importChamberNumber - 2] += (int)currentCheckpointTotal;
                            } else if(importChamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
                                openChambers[openChambers.Length - 1] += (int)currentCheckpointTotal;
                            }
                        }
                    }
                } else {
                    hasFloors = false;
                }
                //check for clear sequence
                if(import.TotalClears == 0) {
                    runsSinceLastClear += (int)import.TotalRuns;
                } else if(import.ClearSequence != null) {
                    for(int i = 0; i < import.ClearSequence!.Count; i++) {
                        int curSequenceValue = (int)import.ClearSequence![i];
                        if(i == 0) {
                            clearSequence.Add(runsSinceLastClear + curSequenceValue);
                            runsSinceLastClear = 0;
                        } else {
                            clearSequence.Add(curSequenceValue);
                        }
                    }
                    runsSinceLastClear += (int)import.RunsSinceLastClear!;
                } else {
                    hasSequence = false;
                }
            };

            foreach(var result in dutyResults) {
                //add import data
                while(_statRange == StatRange.AllLegacy && currentImportIndex < _dutyResultsImports.Count && _dutyResultsImports[currentImportIndex].Time < result.Time) {
                    processImport(_dutyResultsImports[currentImportIndex]);
                    currentImportIndex++;
                }

                runsSinceLastClear++;
                totalGil += result.TotalGil;

                //no checkpoint results
                if(result.CheckpointResults.Count <= 0) {
                    totalDeaths++;
                    continue;
                }

                var lastCheckpoint = result.CheckpointResults.Last();

                //check for clear
                //string finalChamberCheckpoint = isRoulette ? "Defeat final summon" : "Clear final chamber";
                string? finalChamberCheckpoint = duty.Checkpoints?.LastOrDefault()?.Name;
                if(lastCheckpoint.Checkpoint.Name.Equals(finalChamberCheckpoint, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                    clearSequence.Add(runsSinceLastClear);
                    clearDuties.Add(result);
                    runsSinceLastClear = 0;
                    totalClears++;
                }

                //check for death/abandon
                if(lastCheckpoint.Checkpoint.Name.StartsWith(successVerb, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                    totalDeaths++;
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
                    for(int i = 0; i < openChambers.Length; i++) {
                        openChambers[i]++;
                    }
                } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
                    //endChambers[chamberNumber - 1]++;
                    for(int i = 0; i < chamberNumber - 1; i++) {
                        openChambers[i]++;
                    }
                } else {
                    continue;
                }
            }
            //check for remaining imports
            if(_statRange == StatRange.AllLegacy && currentImportIndex != _dutyResultsImports.Count) {
                while(currentImportIndex < _dutyResultsImports.Count) {
                    processImport(_dutyResultsImports[currentImportIndex]);
                    currentImportIndex++;
                }
            }

            //calculate chamber rates
            for(int i = 0; i < numChambers - 1; i++) {
                if(i == 0) {
                    openChambersRates[i] = totalRuns > 0 ? (float)openChambers[i] / totalRuns : 0f;
                } else {
                    openChambersRates[i] = openChambers[i - 1] > 0 ? (float)openChambers[i] / openChambers[i - 1] : 0f;
                }
            }

            //calculate endChmabers
            for(int i = 0; i < endChambers.Length; i++) {
                if(i == 0) {
                    endChambers[i] = totalRuns - openChambers[i];
                } else if(i == endChambers.Length - 1) {
                    endChambers[i] = openChambers[i - 1];
                } else {
                    endChambers[i] = openChambers[i - 1] - openChambers[i];
                }
            }

            //Draw
            if(ImGui.BeginTable($"##{dutyId}-Table", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
                ImGui.TableSetupColumn("checkpoint", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if(_statRange != StatRange.AllLegacy || hasGil) {
                    ImGui.Text("Total gil earned: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{totalGil.ToString("N0")}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
                if(_statRange != StatRange.SinceLastClear) {
                    ImGui.Text("Total clears: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{totalClears}");
                    ImGui.TableNextColumn();
                    if(totalRuns > 0) {
                        ImGui.Text($"{string.Format("{0:P}%", (double)totalClears / totalRuns)}");
                    }
                    ImGui.TableNextColumn();
                }
                if(_statRange != StatRange.AllLegacy && _plugin.Configuration.DutyConfigurations[_dutyId].DisplayDeaths) {
                    ImGui.Text("Total wipes:");
                    Tooltip("Inferred from last checkpoint.");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{totalDeaths}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
                ImGui.Text("Total runs:");
                ImGui.TableNextColumn();
                ImGui.Text($"{totalRuns}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                if(_statRange != StatRange.AllLegacy || hasFloors) {
                    if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.Last) {
                        for(int i = 0; i < endChambers.Length; i++) {
                            if(i == numChambers - 1) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                var ordinalIndex = isRoulette ? i + 2 : i + 1;
                                ImGui.Text($"Ejected at {AddOrdinal(ordinalIndex)} {gateNoun}:");
                                Tooltip("Also includes preceding wipes, abandons \nand timeouts.");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{endChambers[i]}");
                            ImGui.TableNextColumn();
                            if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && i != endChambers.Length - 1 && ((i == 0 && totalRuns != 0) || (i != 0 && openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)1d - openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && totalRuns != 0) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)endChambers[i] / totalRuns)}");
                                Tooltip("Calculated from total runs.");
                            }
                            ImGui.TableNextColumn();
                        }
                    } else if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.All) {
                        for(int i = 0; i < openChambers.Length; i++) {
                            if(i == numChambers - 2) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                ImGui.Text($"{passiveSuccessVerb} {AddOrdinal(i + 2)} {stageNoun}:");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{openChambers[i]}");
                            ImGui.TableNextColumn();
                            if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && ((i == 0 && totalRuns != 0) || (i != 0 && openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && totalRuns != 0) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)openChambers[i] / totalRuns)}");
                                Tooltip("Calculated from total runs.");
                            }
                            ImGui.TableNextColumn();
                        }
                    }
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                if(_statRange != StatRange.AllLegacy || hasSequence) {
                    //todo make this a configuration variable
                    if(_plugin.Configuration.DutyConfigurations[_dutyId].DisplayClearSequence) {
                        for(int i = 0; i < clearSequence.Count; i++) {
                            //ImGui.Text($"{AddOrdinal(i + 1)} clear:");
                            //if(Plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                            //    Tooltip(i == 0 ? "Runs since start." : "Runs since preceding clear.");
                            //}


                            //ImGui.TableNextColumn();

                            if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                                ImGui.Text($"{AddOrdinal(i + 1)} clear:");
                                Tooltip(i == 0 ? "Runs since start." : "Runs since preceding clear.");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{clearSequence[i].ToString().PadRight(3)}");
                            } else {
                                ImGui.Text($"{AddOrdinal(i + 1)} clear (total):");
                                Tooltip("Total runs at time.");
                                ImGui.TableNextColumn();
                                int clearTotal = clearSequence[i];
                                clearSequence.GetRange(0, i).ForEach(x => clearTotal += x);
                                ImGui.Text($"{clearTotal.ToString().PadRight(3)}");
                            }

                            if(_statRange != StatRange.AllLegacy) {
                                if(ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"{clearDuties[i].CompletionTime.ToString()}");
                                    ImGui.Text($"{clearDuties[i].Owner}");
                                    ImGui.EndTooltip();
                                }
                            }
                            ImGui.TableNextColumn();
                            ImGui.TableNextColumn();
                        }
                    }

                    if(totalClears > 0 && _plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                        if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                            ImGui.Text("Runs since last clear: ");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{runsSinceLastClear}");
                            ImGui.TableNextColumn();
                            ImGui.TableNextColumn();
                        }
                    }
                }
                ImGui.EndTable();
            }
        }

        private void SummonTable(List<DutyResults> dutyResults, int dutyId) {
            uint lesserCount = 0;
            uint greaterCount = 0;
            uint elderCount = 0;
            uint circleCount = 0;
            uint abominationCount = 0;
            uint saveCount = 0;
            bool hasSummons = true;

            //check import data
            if(_statRange == StatRange.AllLegacy) {
                foreach(var import in _dutyResultsImports) {
                    if(import.SummonTotals != null) {
                        lesserCount += import.SummonTotals[Summon.Lesser];
                        greaterCount += import.SummonTotals[Summon.Greater];
                        elderCount += import.SummonTotals[Summon.Elder];
                        circleCount += import.SummonTotals[Summon.Gold];
                        abominationCount += import.SummonTotals[Summon.Silver];
                    } else {
                        hasSummons = false;
                    }
                }
            }

            foreach(var result in dutyResults) {
                foreach(var checkpoint in result.CheckpointResults.Where(c => c.IsReached)) {
                    switch(checkpoint.SummonType) {
                        case Summon.Lesser:
                            lesserCount++;
                            break;
                        case Summon.Greater:
                            greaterCount++;
                            break;
                        case Summon.Elder:
                            elderCount++;
                            break;
                        case Summon.Gold:
                            circleCount++;
                            break;
                        case Summon.Silver:
                            abominationCount++;
                            break;
                        default:
                            break;
                    }
                    if(checkpoint.IsSaved) {
                        saveCount++;
                    }
                }
            }

            if(hasSummons) {
                if(ImGui.BeginTable($"##{dutyId}-SummonTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
                    ImGui.TableSetupColumn("summon", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                    ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                    ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Lesser summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{lesserCount}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Greater summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{greaterCount}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Elder summons: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{elderCount}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Circle shifts: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{circleCount}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Abominations: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{abominationCount}");
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

        //duplicated code...
        public static string AddOrdinal(int num) {
            if(num <= 0) return num.ToString();

            switch(num % 100) {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch(num % 10) {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }
    }
}
