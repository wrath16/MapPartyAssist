using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Windows {

    public enum StatRange {
        Current,
        All,
        AllLegacy
    }

    internal class StatsWindow : Window, IDisposable {

        private Plugin Plugin;
        private StatRange _statRange = StatRange.All;
        private int _dutyId;
        private List<DutyResults> _dutyResults = new();

        public StatsWindow(Plugin plugin) : base("Treasure Map Stats") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(800, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public void Refresh() {
            UpdateDutyResults();
        }

        private void UpdateDutyResults() {
            _dutyResults = Plugin.Configuration.DutyResults.Where(dr => {
                MPAMap? map = Plugin.DutyManager.FindMapForDutyResults(dr);
                bool isCurrent = map != null && !map.IsArchived && !map.IsDeleted;
                return dr.IsComplete && dr.DutyId == _dutyId && (_statRange != StatRange.Current || isCurrent);
            }).ToList();
        }

        public override void Draw() {

            //if(ImGui.Checkbox("Only Recent", ref _showRecentOnly)) {

            //}

            int statRangeToInt = (int)_statRange;
            string[] includes = { "Current", "All-Time", "All-Time with imported data" };
            if(ImGui.Combo($"Data Range##includesCombo", ref statRangeToInt, includes, 3)) {
                _statRange = (StatRange)statRangeToInt;
                UpdateDutyResults();
            }

            if(ImGui.BeginTabBar("StatsTabBar", ImGuiTabBarFlags.FittingPolicyMask)) {
                if(ImGui.BeginTabItem("The Hidden Canals of Uznair")) {
                    if(_dutyId != 276) {
                        _dutyId = 276;
                        UpdateDutyResults();
                    }
                    ProgressTable(_dutyResults, _dutyId);
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("The Lost Canals of Uznair")) {
                    if(_dutyId != 268) {
                        _dutyId = 268;
                        UpdateDutyResults();
                    }
                    ProgressTable(_dutyResults, _dutyId);
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("The Shifting Altars of Uznair")) {
                    if(_dutyId != 586) {
                        _dutyId = 586;
                        UpdateDutyResults();
                    }
                    ProgressTable(_dutyResults, _dutyId);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
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
            bool isRoulette = false;
            int numChambers;
            switch(dutyId) {
                case 268: //lost canals
                case 276: //hidden canals
                    numChambers = 7;
                    break;
                case 586: //shifting altars
                    isRoulette = true;
                    numChambers = 5;
                    break;
                default:
                    numChambers = 5;
                    break;
            }
            string successVerb = isRoulette ? "Complete" : "Open";
            string passiveSuccessVerb = isRoulette ? "Completed" : "Reached";
            string stageNoun = isRoulette ? "summon" : "chamber";

            int[] openChambers = new int[numChambers - 1];
            float[] openChambersRates = new float[numChambers - 1];

            int totalGil = 0;
            int runsSinceLastClear = 0;
            int totalClears = 0;
            List<int> clearSequence = new();
            foreach(var result in dutyResults) {
                runsSinceLastClear++;
                totalGil += result.TotalGil;

                //no checkpoint results
                if(result.CheckpointResults.Count <= 0) {
                    continue;
                }

                var lastCheckpoint = result.CheckpointResults.Last();

                //check for clear
                string finalChamberCheckpoint = isRoulette ? "Defeat final summon" : "Clear final chamber";
                if(lastCheckpoint.Checkpoint.Name.Equals(finalChamberCheckpoint, StringComparison.OrdinalIgnoreCase)) {
                    clearSequence.Add(runsSinceLastClear);
                    runsSinceLastClear = 0;
                    totalClears++;
                }

                //find the last reached door checkpoint
                for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith(successVerb) || !lastCheckpoint.IsReached) && i <= result.CheckpointResults.Count; i++) {
                    lastCheckpoint = result.CheckpointResults.ElementAt(result.CheckpointResults.Count - i);
                }

                //did not find a valid checkpoint
                if(lastCheckpoint == result.CheckpointResults[0] && (!lastCheckpoint.IsReached || !lastCheckpoint.Checkpoint.Name.StartsWith(successVerb))) {
                    continue;
                }

                Match chamberMatch = Regex.Match(lastCheckpoint.Checkpoint.Name, @"(?<=(Open|Complete) )[\d|final]+(?=(st|nd|rd|th)? (chamber|summon))", RegexOptions.IgnoreCase);
                int chamberNumber;
                if(!chamberMatch.Success) {
                    //did not find a match
                    continue;
                } else if(chamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
                    for(int i = 0; i < openChambers.Length; i++) {
                        openChambers[i]++;
                    }
                } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
                    for(int i = 0; i < chamberNumber - 1; i++) {
                        openChambers[i]++;
                    }
                } else {
                    continue;
                }
            }

            for(int i = 0; i < numChambers - 1; i++) {
                if(i == 0) {
                    openChambersRates[i] = dutyResults.Count() > 0 ? (float)openChambers[i] / dutyResults.Count() : 0f;
                } else {
                    openChambersRates[i] = openChambers[i - 1] > 0 ? (float)openChambers[i] / openChambers[i - 1] : 0f;
                }
            }

            if(ImGui.BeginTable($"##{dutyId}-Table", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
                ImGui.TableSetupColumn("checkpoint", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text("Total clears: ");
                ImGui.TableNextColumn();
                ImGui.Text($"{totalClears}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total gil earned: ");
                ImGui.TableNextColumn();
                ImGui.Text($"{totalGil.ToString("N0")}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Runs since last clear: ");
                ImGui.TableNextColumn();
                ImGui.Text($"{runsSinceLastClear}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total runs:");
                ImGui.TableNextColumn();
                ImGui.Text($"{dutyResults.Count()}");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                for(int i = 0; i < openChambers.Length; i++) {
                    if(i == numChambers - 2) {
                        ImGui.Text($"{passiveSuccessVerb} final {stageNoun}:");
                    } else {
                        ImGui.Text($"{passiveSuccessVerb} {AddOrdinal(i + 2)} {stageNoun}:");
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text($"{openChambers[i]}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{string.Format("{0:P}%", openChambersRates[i])}");
                    ImGui.TableNextColumn();
                }

                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                for(int i = 0; i < clearSequence.Count; i++) {
                    ImGui.Text($"{AddOrdinal(i + 1)} clear:");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{clearSequence[i]}");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                }
                ImGui.EndTable();
            }
        }

        //private void RouletteTable(int dutyId) {
        //    var allResults = Plugin.Configuration.DutyResults.Where(dr => {
        //        MPAMap? map = Plugin.DutyManager.FindMapForDutyResults(dr);
        //        bool isCurrent = map != null && !map.IsArchived && !map.IsDeleted;
        //        return dr.IsComplete && dr.DutyId == dutyId && (_statRange != StatRange.Current || isCurrent);
        //    });

        //    //determine number of spins from duty id
        //    int numSummons;
        //    switch(dutyId) {
        //        default:
        //            numSummons = 5;
        //            break;
        //    }

        //    int[] completedSummons = new int[numSummons];
        //    float[] openChambersRates = new float[numSummons];

        //    int totalGil = 0;
        //    int runsSinceLastClear = 0;
        //    int totalClears = 0;
        //    List<int> clearSequence = new();
        //    foreach(var result in allResults) {
        //        runsSinceLastClear++;
        //        totalGil += result.TotalGil;

        //        //no checkpoint results
        //        if(result.CheckpointResults.Count <= 0) {
        //            continue;
        //        }

        //        var lastCheckpoint = result.CheckpointResults.Last();

        //        //check for clear
        //        if(lastCheckpoint.Checkpoint.Name.Equals("Defeat final Summon", StringComparison.OrdinalIgnoreCase)) {
        //            clearSequence.Add(runsSinceLastClear);
        //            runsSinceLastClear = 0;
        //            totalClears++;
        //        }

        //        //find the last reached door checkpoint
        //        for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith("Open") || !lastCheckpoint.IsReached) && i <= result.CheckpointResults.Count; i++) {
        //            lastCheckpoint = result.CheckpointResults.ElementAt(result.CheckpointResults.Count - i);
        //        }

        //        //did not find a valid checkpoint
        //        if(lastCheckpoint == result.CheckpointResults[0] && (!lastCheckpoint.IsReached || !lastCheckpoint.Checkpoint.Name.StartsWith("Open"))) {
        //            continue;
        //        }

        //        Match chamberMatch = Regex.Match(lastCheckpoint.Checkpoint.Name, @"(?<=Complete )[\d|final]+(?=(st|nd|rd|th)? summon)", RegexOptions.IgnoreCase);
        //        int chamberNumber;
        //        if(!chamberMatch.Success) {
        //            //did not find a match
        //            continue;
        //        } else if(chamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
        //            for(int i = 0; i < completedSummons.Length; i++) {
        //                completedSummons[i]++;
        //            }
        //        } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
        //            for(int i = 0; i < chamberNumber; i++) {
        //                completedSummons[i]++;
        //            }
        //        } else {
        //            continue;
        //        }
        //    }

        //    for(int i = 0; i < numSummons; i++) {
        //        if(i == 0) {
        //            openChambersRates[i] = allResults.Count() > 0 ? (float)completedSummons[i] / allResults.Count() : 0f;
        //        } else {
        //            openChambersRates[i] = completedSummons[i - 1] > 0 ? (float)completedSummons[i] / completedSummons[i - 1] : 0f;
        //        }
        //    }

        //    if(ImGui.BeginTable($"##{dutyId}-Table", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
        //        ImGui.TableSetupColumn("checkpoint", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
        //        ImGui.TableSetupColumn($"rawNumber", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
        //        ImGui.TableSetupColumn($"rate", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);

        //        ImGui.TableNextRow();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Total clears: ");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{totalClears}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Total gil earned: ");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{totalGil.ToString("N0")}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Runs since last clear: ");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{runsSinceLastClear}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Total runs:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{allResults.Count()}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();

        //        for(int i = 1; i < numSummons; i++) {
        //            if(i == numSummons - 1) {
        //                ImGui.Text($"Completed final summon:");
        //            } else {
        //                ImGui.Text($"Completed {AddOrdinal(i + 1)} summon:");
        //            }
        //            ImGui.TableNextColumn();
        //            ImGui.Text($"{completedSummons[i]}");
        //            ImGui.TableNextColumn();
        //            ImGui.Text($"{string.Format("{0:P}%", openChambersRates[i])}");
        //            ImGui.TableNextColumn();
        //        }

        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();

        //        for(int i = 0; i < clearSequence.Count; i++) {
        //            ImGui.Text($"{AddOrdinal(i + 1)} clear:");
        //            ImGui.TableNextColumn();
        //            ImGui.Text($"{clearSequence[i]}");
        //            ImGui.TableNextColumn();
        //            ImGui.TableNextColumn();
        //        }
        //        ImGui.EndTable();
        //    }
        //}

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
