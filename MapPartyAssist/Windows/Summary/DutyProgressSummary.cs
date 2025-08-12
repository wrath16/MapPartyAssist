using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using MapPartyAssist.Helper;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using MapPartyAssist.Windows.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using static Dalamud.Interface.Windowing.Window;

namespace MapPartyAssist.Windows.Summary {
    internal class DutyProgressSummary {

        private class StatsSummary {
            public int TotalGil, TotalClears, TotalWipes, TotalRuns, RunsSinceLastClear, SaveCount;
            public List<int> ClearSequence = new();
            public List<DutyResults> ClearDuties = new();
            public int[] OpenChambers = new int[0], EndChambers = new int[0];
            public float[] OpenChambersRates = new float[0];
            public Dictionary<Summon, int> SummonTotals = new() {
            { Summon.Lesser, 0 },
            { Summon.Greater, 0 },
            { Summon.Elder, 0 },
            { Summon.Gold, 0 },
            { Summon.Silver, 0 } };
            public bool HasGil = true, HasSequence = true, HasFloors = true, HasSummons = true;
        }

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        //native filters
        TimeFilter _timeFilter;
        SingleDutyFilter _dutyFilter;
        private Dictionary<int, StatsSummary> _dutyStats = new();

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

        public void Refresh(List<DutyResults> dutyResults, List<DutyResultsImport> imports) {
            UpdateDutyFilter();
            UpdateTimeFilter();

            Dictionary<int, StatsSummary> dutyStats = new();

            //find number of unique duties
            foreach(var dr in dutyResults) {
                if(!dutyStats.ContainsKey(dr.DutyId)) {
                    dutyStats.Add(dr.DutyId, new());
                }
            }
            dutyStats = dutyStats.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach(var kvp in dutyStats) {
                //calculate stats
                var dutyStat = dutyStats[kvp.Key];
                var onlyDutyResults = dutyResults.Where(dr => dr.DutyId == kvp.Key).ToList();
                var onlyImports = imports.Where(dr => dr.DutyId == kvp.Key).ToList();
                var duty = _plugin.DutyManager.Duties[kvp.Key];
                bool isRoulette = duty.Structure == DutyStructure.Roulette || duty.Structure == DutyStructure.Slots;
                int numChambers = duty.ChamberCount;
                string successVerb = isRoulette ? "Complete" : "Open";
                string passiveSuccessVerb = isRoulette ? "Completed" : "Reached";
                string fightVerb = isRoulette ? "Defeat" : "Clear";
                string stageNoun = isRoulette ? "summon" : "chamber";
                string gateNoun = isRoulette ? "summon" : "gate";
                string chamberPattern = @"(?<=(Open|Complete) )[\d|final]+(?=(st|nd|rd|th)? (chamber|summon|trial))";

                dutyStat.OpenChambers = new int[numChambers - 1];
                dutyStat.OpenChambersRates = new float[numChambers - 1];
                dutyStat.EndChambers = new int[numChambers];
                dutyStat.TotalRuns = onlyDutyResults.Count;

                //import specific stuff
                int currentImportIndex = 0;

                var processImport = (DutyResultsImport import) => {
                    dutyStat.TotalRuns += (int)import.TotalRuns;
                    dutyStat.TotalClears += (int)import.TotalClears;
                    //check for gil
                    if(import.TotalGil != null) {
                        dutyStat.TotalGil += (int)import.TotalGil!;
                    } else {
                        dutyStat.HasGil = false;
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
                                    dutyStat.OpenChambers[importChamberNumber - 2] += (int)currentCheckpointTotal;
                                } else if(importChamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
                                    dutyStat.OpenChambers[dutyStat.OpenChambers.Length - 1] += (int)currentCheckpointTotal;
                                }
                            }

                            //check for wipes
                            if(duty.Checkpoints![i].Name.Contains(fightVerb, StringComparison.OrdinalIgnoreCase)) {
                                uint wipes = 0;
                                if(i == 0) {
                                    wipes = import.TotalRuns - currentCheckpointTotal;
                                } else {
                                    wipes = import.CheckpointTotals[i - 1] - currentCheckpointTotal;
                                }
                                dutyStat.TotalWipes += (int)wipes;
                            }
                        }
                    } else {
                        dutyStat.HasFloors = false;
                    }
                    //check for clear sequence
                    if(import.TotalClears == 0) {
                        dutyStat.RunsSinceLastClear += (int)import.TotalRuns;
                    } else if(import.ClearSequence != null) {
                        for(int i = 0; i < import.ClearSequence!.Count; i++) {
                            int curSequenceValue = (int)import.ClearSequence![i];
                            if(i == 0) {
                                dutyStat.ClearSequence.Add(dutyStat.RunsSinceLastClear + curSequenceValue);
                                dutyStat.RunsSinceLastClear = 0;
                            } else {
                                dutyStat.ClearSequence.Add(curSequenceValue);
                            }
                            dutyStat.ClearDuties.Add(new DutyResults() {
                                CompletionTime = import.Time,
                                Owner = "Imported clear"
                            });
                        }
                        dutyStat.RunsSinceLastClear += (int)import.RunsSinceLastClear!;
                    } else {
                        dutyStat.HasSequence = false;
                    }
                };

                foreach(var result in onlyDutyResults) {
                    //add import data
                    while(currentImportIndex < onlyImports.Count && onlyImports[currentImportIndex].Time < result.Time) {
                        processImport(onlyImports[currentImportIndex]);
                        currentImportIndex++;
                    }

                    dutyStat.RunsSinceLastClear++;
                    dutyStat.TotalGil += result.TotalGil;

                    //no checkpoint results
                    if(result.CheckpointResults.Count <= 0) {
                        dutyStat.TotalWipes++;
                        continue;
                    }

                    var lastCheckpoint = result.CheckpointResults.Last();

                    //check for clear
                    //string finalChamberCheckpoint = isRoulette ? "Defeat final summon" : "Clear final chamber";
                    string? finalChamberCheckpoint = duty.Checkpoints?.LastOrDefault()?.Name;
                    if(lastCheckpoint.Checkpoint.Name.Equals(finalChamberCheckpoint, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                        dutyStat.ClearSequence.Add(dutyStat.RunsSinceLastClear);
                        dutyStat.ClearDuties.Add(result);
                        dutyStat.RunsSinceLastClear = 0;
                        dutyStat.TotalClears++;
                    }

                    //check for death/abandon
                    if(lastCheckpoint.Checkpoint.Name.StartsWith(successVerb, StringComparison.OrdinalIgnoreCase) && lastCheckpoint.IsReached) {
                        dutyStat.TotalWipes++;
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
                        for(int i = 0; i < dutyStat.OpenChambers.Length; i++) {
                            dutyStat.OpenChambers[i]++;
                        }
                    } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
                        //endChambers[chamberNumber - 1]++;
                        for(int i = 0; i < chamberNumber - 1; i++) {
                            dutyStat.OpenChambers[i]++;
                        }
                    } else {
                        continue;
                    }
                }
                //check for remaining imports
                if(currentImportIndex != onlyImports.Count) {
                    while(currentImportIndex < onlyImports.Count) {
                        processImport(onlyImports[currentImportIndex]);
                        currentImportIndex++;
                    }
                }

                //calculate chamber rates
                for(int i = 0; i < numChambers - 1; i++) {
                    if(i == 0) {
                        dutyStat.OpenChambersRates[i] = dutyStat.TotalRuns > 0 ? (float)dutyStat.OpenChambers[i] / dutyStat.TotalRuns : 0f;
                    } else {
                        dutyStat.OpenChambersRates[i] = dutyStat.OpenChambers[i - 1] > 0 ? (float)dutyStat.OpenChambers[i] / dutyStat.OpenChambers[i - 1] : 0f;
                    }
                }

                //calculate endChambers
                for(int i = 0; i < dutyStat.EndChambers.Length; i++) {
                    if(i == 0) {
                        dutyStat.EndChambers[i] = dutyStat.TotalRuns - dutyStat.OpenChambers[i];
                    } else if(i == dutyStat.EndChambers.Length - 1) {
                        dutyStat.EndChambers[i] = dutyStat.OpenChambers[i - 1];
                    } else {
                        dutyStat.EndChambers[i] = dutyStat.OpenChambers[i - 1] - dutyStat.OpenChambers[i];
                    }
                }

                //summon data
                if(duty.Structure == DutyStructure.Roulette || duty.Structure == DutyStructure.Slots) {
                    //check import data
                    foreach(var import in onlyImports) {
                        if(import.SummonTotals != null) {
                            foreach(var summonTotal in import.SummonTotals) {
                                dutyStat.SummonTotals[summonTotal.Key] += (int)import.SummonTotals[summonTotal.Key];
                            }
                        } else {
                            dutyStat.HasSummons = false;
                        }
                    }

                    foreach(var result in onlyDutyResults) {
                        foreach(var checkpoint in result.CheckpointResults.Where(c => c.IsReached)) {
                            if(checkpoint.SummonType is null) {
                                continue;
                            }
                            dutyStat.SummonTotals[(Summon)checkpoint.SummonType!]++;
                            if(checkpoint.IsSaved) {
                                dutyStat.SaveCount++;
                            }
                        }
                    }
                }
            }
            //_dutyResults = dutyResults;
            //_dutyResultsImports = imports;
            _dutyStats = dutyStats;
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
            _statsWindow.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(_dutyStats.Count > 1 ? 575 : 300, _statsWindow.SizeConstraints!.Value.MinimumSize.Y),
                MaximumSize = _statsWindow.SizeConstraints!.Value.MaximumSize,
            };
            if(_dutyStats.Count == 0) {
                ImGui.TextDisabled("No duty results for given filters.");
            }
            using(var table = ImRaii.Table("statsTable", 2, ImGuiTableFlags.NoClip | ImGuiTableFlags.NoKeepColumnsVisible)) {
                for(int i = 0; i < _dutyStats.Count; i++) {
                    var duty = _dutyStats.ElementAt(i);
                    ImGui.TableNextColumn();
                    if(i > 1) {
                        ImGui.Separator();
                    }
                    ImGui.TextColored(ImGuiColors.DalamudViolet, _plugin.DutyManager.Duties[duty.Key].GetDisplayName());
                    //ImGui.TextColored(ImGuiColors.DalamudWhite, TimeFilter.RangeToString(_timeFilter.StatRange).ToUpper());
                    ProgressTable(duty.Key);
                    if(_plugin.DutyManager.Duties[duty.Key].Structure == DutyStructure.Roulette || _plugin.DutyManager.Duties[duty.Key].Structure == DutyStructure.Slots) {
                        SummonTable(duty.Key);
                    }
                }
            }
        }

        private void ProgressTable(int dutyId) {
            //determine number of chambers from duty id and whether this is doors or roulette
            var duty = _plugin.DutyManager.Duties[dutyId];
            var dutyStat = _dutyStats[dutyId];
            bool isRoulette = duty.Structure == DutyStructure.Roulette;
            int numChambers = duty.ChamberCount;
            string successVerb = isRoulette ? "Complete" : "Open";
            string passiveSuccessVerb = isRoulette ? "Completed" : "Reached";
            string stageNoun = isRoulette ? "summon" : "chamber";
            string gateNoun = isRoulette ? "summon" : "gate";

            //Draw
            using(var table = ImRaii.Table($"##{dutyId}-Table", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip | ImGuiTableFlags.NoKeepColumnsVisible)) {
                if(table) {
                    ImGui.TableSetupColumn("checkpoint", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                    ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                    ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if(dutyStat.HasGil) {
                        ImGui.Text("Total gil earned: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.TotalGil.ToString("N0")}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
                    if(_timeFilter.StatRange != StatRange.SinceLastClear) {
                        ImGui.Text("Total clears: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.TotalClears}");
                        ImGui.TableNextColumn();
                        if(dutyStat.TotalRuns > 0) {
                            ImGui.Text($"{string.Format("{0:P}", (double)dutyStat.TotalClears / dutyStat.TotalRuns)}");
                        }
                        ImGui.TableNextColumn();
                    }
                    if(_plugin.Configuration.DutyConfigurations[dutyId].DisplayDeaths) {
                        ImGui.Text("Total wipes:");
                        Tooltip("Inferred from last checkpoint.");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.TotalWipes}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
                    ImGui.Text("Total runs:");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{dutyStat.TotalRuns}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();

                    if(dutyStat.HasFloors) {
                        if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.Last) {
                            for(int i = 0; i < dutyStat.EndChambers.Length; i++) {
                                if(i == numChambers - 1) {
                                    ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                                } else {
                                    var ordinalIndex = isRoulette ? i + 2 : i + 1;
                                    ImGui.Text($"Ejected at {StringHelper.AddOrdinal(ordinalIndex)} {gateNoun}:");
                                    Tooltip("Also includes preceding wipes, abandons \nand timeouts.");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{dutyStat.EndChambers[i]}");
                                ImGui.TableNextColumn();
                                if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous
                                    && i != dutyStat.EndChambers.Length - 1 && ((i == 0 && dutyStat.TotalRuns != 0) || (i != 0 && dutyStat.OpenChambers[i - 1] != 0))) {
                                    ImGui.Text($"{string.Format("{0:P}", (double)1d - dutyStat.OpenChambersRates[i])}");
                                    Tooltip("Calculated from previous stage.");
                                } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && dutyStat.TotalRuns != 0) {
                                    ImGui.Text($"{string.Format("{0:P}", (double)dutyStat.EndChambers[i] / dutyStat.TotalRuns)}");
                                    Tooltip("Calculated from total runs.");
                                }
                                ImGui.TableNextColumn();
                            }
                        } else if(_plugin.Configuration.ProgressTableCount == ProgressTableCount.All) {
                            for(int i = 0; i < dutyStat.OpenChambers.Length; i++) {
                                if(i == numChambers - 2) {
                                    ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                                } else {
                                    ImGui.Text($"{passiveSuccessVerb} {StringHelper.AddOrdinal(i + 2)} {stageNoun}:");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{dutyStat.OpenChambers[i]}");
                                ImGui.TableNextColumn();
                                if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && ((i == 0 && dutyStat.TotalRuns != 0) || (i != 0 && dutyStat.OpenChambers[i - 1] != 0))) {
                                    ImGui.Text($"{string.Format("{0:P}", dutyStat.OpenChambersRates[i])}");
                                    Tooltip("Calculated from previous stage.");
                                } else if(_plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && dutyStat.TotalRuns != 0) {
                                    ImGui.Text($"{string.Format("{0:P}", (double)dutyStat.OpenChambers[i] / dutyStat.TotalRuns)}");
                                    Tooltip("Calculated from total runs.");
                                }
                                ImGui.TableNextColumn();
                            }
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();

                    if(dutyStat.HasSequence) {
                        //todo make this a configuration variable
                        if(_plugin.Configuration.DutyConfigurations[dutyId].DisplayClearSequence) {
                            for(int i = 0; i < dutyStat.ClearSequence.Count; i++) {
                                if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                                    ImGui.Text($"{StringHelper.AddOrdinal(i + 1)} clear:");
                                    Tooltip(i == 0 ? "Runs since start." : "Runs since preceding clear.");
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{dutyStat.ClearSequence[i].ToString().PadRight(3)}");
                                } else {
                                    ImGui.Text($"{StringHelper.AddOrdinal(i + 1)} clear (total):");
                                    Tooltip("Total runs at time.");
                                    ImGui.TableNextColumn();
                                    int clearTotal = dutyStat.ClearSequence[i];
                                    dutyStat.ClearSequence.GetRange(0, i).ForEach(x => clearTotal += x);
                                    ImGui.Text($"{clearTotal.ToString().PadRight(3)}");
                                }
                                if(ImGui.IsItemHovered()) {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"{dutyStat.ClearDuties[i].CompletionTime.ToString()}");
                                    ImGui.Text($"{dutyStat.ClearDuties[i].Owner}");
                                    ImGui.EndTooltip();
                                }
                                ImGui.TableNextColumn();
                                ImGui.TableNextColumn();
                            }
                        }

                        if(dutyStat.TotalClears > 0 && _plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                            if(_plugin.Configuration.ClearSequenceCount == ClearSequenceCount.Last) {
                                ImGui.Text("Runs since last clear: ");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{dutyStat.RunsSinceLastClear}");
                                ImGui.TableNextColumn();
                                ImGui.TableNextColumn();
                            }
                        }
                    }
                }
            }
        }

        private void SummonTable(int dutyId) {
            var dutyStat = _dutyStats[dutyId];
            var structure = _plugin.DutyManager.Duties[dutyId].Structure;
            if(dutyStat.HasSummons) {
                using(var table = ImRaii.Table($"##{dutyId}-SummonTable", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip | ImGuiTableFlags.NoKeepColumnsVisible)) {
                    if(table) {
                        ImGui.TableSetupColumn("summon", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                        ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                        ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text("Lesser summons: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.SummonTotals[Summon.Lesser]}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.Text("Greater summons: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.SummonTotals[Summon.Greater]}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.Text("Elder summons: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.SummonTotals[Summon.Elder]}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        if(structure == DutyStructure.Roulette) {
                            ImGui.Text("Circle shifts: ");
                        } else if(structure == DutyStructure.Slots) {
                            ImGui.Text("Final summons: ");
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.SummonTotals[Summon.Gold]}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        if(structure == DutyStructure.Roulette) {
                            ImGui.Text("Abominations: ");
                        } else if(structure == DutyStructure.Slots) {
                            ImGui.Text("Fever dreams: ");
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dutyStat.SummonTotals[Summon.Silver]}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
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
