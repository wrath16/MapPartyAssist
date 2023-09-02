using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Windows {

    public enum StatRange {
        Current,
        PastDay,
        PastWeek,
        SinceLastClear,
        All,
        AllLegacy
    }

    internal class StatsWindow : Window, IDisposable {

        private Plugin Plugin;
        private ViewDutyResultsImportsWindow ViewDutyResultsImportsWindow;
        private StatRange _statRange = StatRange.All;
        private int _dutyId = 276;
        private int _selectedDuty = 2;
        private List<DutyResults> _dutyResults = new();
        private List<DutyResultsImport> _dutyResultsImports = new();

        public StatsWindow(Plugin plugin) : base("Treasure Dungeon Stats") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(600, 1000)
            };
            this.Plugin = plugin;
            ViewDutyResultsImportsWindow = new ViewDutyResultsImportsWindow(plugin, this);
            ViewDutyResultsImportsWindow.IsOpen = false;
            Plugin.WindowSystem.AddWindow(ViewDutyResultsImportsWindow);
        }

        public void Dispose() {
        }

        public void Refresh() {
            UpdateDutyResults();
            ViewDutyResultsImportsWindow.Refresh();
        }

        public override void OnClose() {
            ViewDutyResultsImportsWindow.IsOpen = false;
            base.OnClose();
        }

        private void UpdateDutyResults() {

            if(_statRange == StatRange.Current) {
                //_dutyResults = Plugin.DutyManager.GetRecentDutyResultsList(_dutyId);
                _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).Where(dr => dr.Map != null && !dr.Map.IsArchived && !dr.Map.IsDeleted && dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
            } else if(_statRange == StatRange.PastDay) {
                _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToEnumerable().Where(dr => (DateTime.Now - dr.Time).TotalHours < 24).ToList();
            } else if(_statRange == StatRange.PastWeek) {
                _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToEnumerable().Where(dr => (DateTime.Now - dr.Time).TotalDays < 7).ToList();
            } else if(_statRange == StatRange.All) {
                _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
            } else if(_statRange == StatRange.AllLegacy) {
                _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                _dutyResultsImports = Plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted && i.DutyId == _dutyId).OrderBy(i => i.Time).ToList();
            } else if(_statRange == StatRange.SinceLastClear) {
                var lastClear = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList().Where(dr => dr.CheckpointResults.Count == Plugin.DutyManager.Duties[_dutyId].Checkpoints.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                if(lastClear != null) {
                    _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId && dr.Time > lastClear.Time).OrderBy(dr => dr.Time).ToList();
                } else {
                    _dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == _dutyId).OrderBy(dr => dr.Time).ToList();
                }
            }

            if(Plugin.Configuration.CurrentCharacterStatsOnly) {
                _dutyResults = _dutyResults.Where(dr => dr.Players.Contains(Plugin.GetCurrentPlayer())).ToList();
            }
        }

        public override void Draw() {

            //if(ImGui.Button("Test Function")) {
            //    Plugin.TestFunction5();
            //}

            string[] duties = { "The Aquapolis", "The Lost Canals of Uznair", "The Hidden Canals of Uznair", "The Shifting Altars of Uznair", "The Dungeons of Lyhe Ghiah", "The Shifting Oubliettes of Lyhe Ghiah", "The Excitatron 6000", "The Shifting Gymnasion Agonon" };
            if(ImGui.Combo($"Duty##DutyCombo", ref _selectedDuty, duties, 8)) {
                switch(_selectedDuty) {
                    case 0:
                        _dutyId = 179;
                        break;
                    case 1:
                        _dutyId = 268;
                        break;
                    case 2:
                    default:
                        _dutyId = 276;
                        break;
                    case 3:
                        _dutyId = 586;
                        break;
                    case 4:
                        _dutyId = 688;
                        break;
                    case 5:
                        _dutyId = 745;
                        break;
                    case 6:
                        _dutyId = 819;
                        break;
                    case 7:
                        _dutyId = 909;
                        break;
                }
                UpdateDutyResults();
            }

            int statRangeToInt = (int)_statRange;
            string[] includes = { "Current", "Last Day", "Last Week", "Since last clear", "All-Time", "All-Time with imported data" };
            if(ImGui.Combo($"Data Range##includesCombo", ref statRangeToInt, includes, 6)) {
                _statRange = (StatRange)statRangeToInt;
                UpdateDutyResults();
            }

            if(_statRange == StatRange.AllLegacy) {
                if(ImGui.Button("Manage Imports")) {
                    if(!ViewDutyResultsImportsWindow.IsOpen) {
                        ViewDutyResultsImportsWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                        ViewDutyResultsImportsWindow.IsOpen = true;
                    }
                }
            }

            ProgressTable(_dutyResults, _dutyId);
            if(Plugin.DutyManager.Duties[_dutyId].Structure == DutyStructure.Roulette) {
                SummonTable(_dutyResults, _dutyId);
            }
        }

        public override void PreDraw() {
        }

        private void PrepareTable() {
        }

        private void ProgressTable(List<DutyResults> dutyResults, int dutyId) {
            //var allResults = Plugin.Configuration.DutyResults.Where(dr => {
            //    MPAMap? map = Plugin.DutyManager.FindMapForDutyResults(dr);
            //    bool isCurrent = map != null && !map.IsArchived && !map.IsDeleted;
            //    return dr.IsComplete && dr.DutyId == dutyId && (_statRange != StatRange.Current || isCurrent);
            //});

            //determine number of chambers from duty id and whether this is doors or roulette
            var duty = Plugin.DutyManager.Duties[dutyId];
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
                string finalChamberCheckpoint = duty.Checkpoints.Last().Name;
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
                if(_statRange != StatRange.AllLegacy && Plugin.Configuration.DutyConfigurations[_dutyId].DisplayDeaths) {
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
                    if(Plugin.Configuration.ProgressTableCount == ProgressTableCount.Last) {
                        for(int i = 0; i < endChambers.Length; i++) {
                            if(i == numChambers - 1) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                var ordinalIndex = isRoulette ? i + 2 : i + 1;
                                ImGui.Text($"Failed to pass {AddOrdinal(ordinalIndex)} {gateNoun}:");
                                Tooltip("Includes wipes, abandons, timeouts and \nfailed doors/invocations.");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{endChambers[i]}");
                            ImGui.TableNextColumn();
                            if(Plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && i != endChambers.Length - 1 && ((i == 0 && totalRuns != 0) || (i != 0 && openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)1d - openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(Plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && totalRuns != 0) {
                                ImGui.Text($"{string.Format("{0:P}%", (double)endChambers[i] / totalRuns)}");
                                Tooltip("Calculated from total runs.");
                            }
                            ImGui.TableNextColumn();
                        }
                    } else if(Plugin.Configuration.ProgressTableCount == ProgressTableCount.All) {
                        for(int i = 0; i < openChambers.Length; i++) {
                            if(i == numChambers - 2) {
                                ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                            } else {
                                ImGui.Text($"{passiveSuccessVerb} {AddOrdinal(i + 2)} {stageNoun}:");
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{openChambers[i]}");
                            ImGui.TableNextColumn();
                            if(Plugin.Configuration.ProgressTableRate == ProgressTableRate.Previous && ((i == 0 && totalRuns != 0) || (i != 0 && openChambers[i - 1] != 0))) {
                                ImGui.Text($"{string.Format("{0:P}%", openChambersRates[i])}");
                                Tooltip("Calculated from previous stage.");
                            } else if(Plugin.Configuration.ProgressTableRate == ProgressTableRate.Total && totalRuns != 0) {
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
                    if(Plugin.Configuration.DutyConfigurations[_dutyId].DisplayClearSequence) {
                        for(int i = 0; i < clearSequence.Count; i++) {
                            ImGui.Text($"{AddOrdinal(i + 1)} clear:");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{clearSequence[i].ToString().PadRight(3)}");
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

                    if(totalClears > 0) {
                        ImGui.Text("Runs since last clear: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{runsSinceLastClear}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
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
            if(ImGui.IsItemHovered() && (!isTutorial || Plugin.Configuration.ShowStatsWindowTooltips)) {
                ImGui.BeginTooltip();
                ImGui.Text(message);
                ImGui.EndTooltip();
            }
        }

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
