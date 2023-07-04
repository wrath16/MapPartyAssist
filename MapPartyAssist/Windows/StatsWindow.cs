using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {

    public enum StatRange {
        Current,
        All,
        AllLegacy
    }


    internal class StatsWindow : Window, IDisposable {

        private Plugin Plugin;
        private bool _showRecentOnly = false;
        private StatRange _statRange = StatRange.All;

        public StatsWindow(Plugin plugin) : base("Treasure Map Stats") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(800, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {

            //if(ImGui.Checkbox("Only Recent", ref _showRecentOnly)) {

            //}

            int statRangeToInt = (int)_statRange;
            string[] includes = { "Current", "All-Time", "All-Time with imported data" };
            if(ImGui.Combo($"Data Range##includesCombo", ref statRangeToInt, includes, 3)) {
                _statRange = (StatRange)statRangeToInt;
            }

            if(ImGui.BeginTabBar("StatsTabBar", ImGuiTabBarFlags.FittingPolicyMask)) {
                if(ImGui.BeginTabItem("The Hidden Canals of Uznair")) {

                    //if(ImGui.Button("Test Function")) {
                    //    Plugin.TestFunction();
                    //}


                    var allResults = Plugin.Configuration.DutyResults.Where(dr => {
                        MPAMap? map = Plugin.DutyManager.FindMapForDutyResults(dr);
                        bool isCurrent = map != null && !map.IsArchived && !map.IsDeleted;
                        return dr.IsComplete && dr.DutyId == 276 && (_statRange != StatRange.Current || isCurrent);
                    });
                    //var allResults = Plugin.Configuration.DutyResults.Where(dr => dr.GetType() == typeof(HiddenCanalsOfUznairResults));
                    int openSecondChamber = 0;
                    int openThirdChamber = 0;
                    int openFourthChamber = 0;
                    int openFifthChamber = 0;
                    int openSixthChamber = 0;
                    int openFinalChamber = 0;
                    int totalGil = 0;
                    int mapsSinceLastClear = 0;
                    int totalClears = 0;
                    List<int> clearSequence = new();
                    foreach(var result in allResults) {
                        mapsSinceLastClear++;
                        totalGil += result.TotalGil;

                        if(result.CheckpointResults.Count > 0) {
                            var lastCheckpoint = result.CheckpointResults.Last();

                            //find the last reached door checkpoint
                            for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith("Open") || !lastCheckpoint.IsReached) && i <= result.CheckpointResults.Count; i++) {
                                lastCheckpoint = result.CheckpointResults.ElementAt(result.CheckpointResults.Count - i);
                            }

                            //did not clear any checkpoints
                            if(!lastCheckpoint.IsReached) {
                                //
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open final chamber")) {
                                openSecondChamber++;
                                openThirdChamber++;
                                openFourthChamber++;
                                openFifthChamber++;
                                openSixthChamber++;
                                openFinalChamber++;
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open 6th chamber")) {
                                openSecondChamber++;
                                openThirdChamber++;
                                openFourthChamber++;
                                openFifthChamber++;
                                openSixthChamber++;
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open 5th chamber")) {
                                openSecondChamber++;
                                openThirdChamber++;
                                openFourthChamber++;
                                openFifthChamber++;
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open 4th chamber")) {
                                openSecondChamber++;
                                openThirdChamber++;
                                openFourthChamber++;
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open 3rd chamber")) {
                                openSecondChamber++;
                                openThirdChamber++;
                            } else if(lastCheckpoint.Checkpoint.Name.Equals("Open 2nd chamber")) {
                                openSecondChamber++;
                            }

                            if(result.CheckpointResults.Last().Checkpoint.Name.Equals("Clear final chamber")) {
                                clearSequence.Add(mapsSinceLastClear);
                                mapsSinceLastClear = 0;
                                totalClears++;
                            }
                        }
                    }

                    float reachSecondRate = allResults.Count() > 0 ? (float)openSecondChamber / allResults.Count() : 0f;
                    float reachThirdRate = openSecondChamber > 0 ? (float)openThirdChamber / openSecondChamber : 0f;
                    float reachFourthRate = openThirdChamber > 0 ? (float)openFourthChamber / openThirdChamber : 0f;
                    float reachFifthRate = openFourthChamber > 0 ? (float)openFifthChamber / openFourthChamber : 0f;
                    float reachSixthRate = openFifthChamber > 0 ? (float)openSixthChamber / openFifthChamber : 0f;
                    float reachFinalRate = openSixthChamber > 0 ? (float)openFinalChamber / openSixthChamber : 0f;

                    if(ImGui.BeginTable($"##hiddenCanalsAllTime", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip)) {
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
                        ImGui.Text($"{mapsSinceLastClear}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.Text("Total runs:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{allResults.Count()}");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached 2nd chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openSecondChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachSecondRate)}");
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached 3rd chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openThirdChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachThirdRate)}");
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached 4th chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openFourthChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachFourthRate)}");
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached 5th chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openFifthChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachFifthRate)}");
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached 6th chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openSixthChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachSixthRate)}");
                        ImGui.TableNextColumn();
                        ImGui.Text("Reached final chamber:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{openFinalChamber}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{String.Format("{0:P}%", reachFinalRate)}");
                        ImGui.TableNextColumn();

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
                        ImGui.EndTabItem();
                    }
                }
                if(ImGui.BeginTabItem("The Lost Canals of Uznair")) {
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("The Shifting Altars of Uznair")) {
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        public override void PreDraw() {
        }

        //public void DoorsTable(int dutyId) {
        //    var allResults = Plugin.Configuration.DutyResults.Where(dr => {
        //        MPAMap? map = Plugin.DutyManager.FindMapForDutyResults(dr);
        //        bool isCurrent = map != null && !map.IsArchived && !map.IsDeleted;
        //        return dr.IsComplete && dr.DutyId == dutyId && (_statRange != StatRange.Current || isCurrent);
        //    });

        //    //determine number of chambers from duty id
        //    int numChambers;
        //    switch(dutyId) {
        //        case 268: //lost canals
        //        case 276: //hidden canals
        //            numChambers = 7;
        //            break;
        //        default:
        //            numChambers = 5;
        //            break;
        //    }

        //    int[] openChambers= new int[numChambers];
        //    float[] openChambersRates = new float[numChambers];

        //    int totalGil = 0;
        //    int mapsSinceLastClear = 0;
        //    int totalClears = 0;
        //    List<int> clearSequence = new();
        //    foreach(var result in allResults) {
        //        mapsSinceLastClear++;
        //        totalGil += result.TotalGil;

        //        //no checkpoint results
        //        if(result.CheckpointResults.Count <= 0) {
        //            continue;
        //        }

        //        var lastCheckpoint = result.CheckpointResults.Last();
        //        //find the last reached door checkpoint
        //        for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith("Open") || !lastCheckpoint.IsReached) && i <= result.CheckpointResults.Count; i++) {
        //            lastCheckpoint = result.CheckpointResults.ElementAt(result.CheckpointResults.Count - i);
        //        }

        //        //did not find a valid checkpoint
        //        if(lastCheckpoint == result.CheckpointResults[0] && (!lastCheckpoint.IsReached || !lastCheckpoint.Checkpoint.Name.StartsWith("Open"))) {
        //            continue;
        //        }

        //        Match chamberMatch = Regex.Match(lastCheckpoint.Checkpoint.Name, @"(?<=Open )[\d|final]+(?=(st|nd|rd|th)? chamber)", RegexOptions.IgnoreCase);
        //        int chamberNumber;
        //        if(!chamberMatch.Success) {
        //            //did not find a match
        //            continue;
        //        } else if(chamberMatch.Value.Equals("final", StringComparison.OrdinalIgnoreCase)) {
        //            for(int i = 0; i < openChambers.Length; i++) {
        //                openChambers[i]++;
        //            }
        //        } else if(int.TryParse(chamberMatch.Value, out chamberNumber)) {
        //            for(int i = 0; i < chamberNumber; i++) {
        //                openChambers[i]++;
        //            }
        //        } else {
        //            continue;
        //        }
        //    }

        //    float reachSecondRate = allResults.Count() > 0 ? (float)openSecondChamber / allResults.Count() : 0f;
        //    float reachThirdRate = openSecondChamber > 0 ? (float)openThirdChamber / openSecondChamber : 0f;
        //    float reachFourthRate = openThirdChamber > 0 ? (float)openFourthChamber / openThirdChamber : 0f;
        //    float reachFifthRate = openFourthChamber > 0 ? (float)openFifthChamber / openFourthChamber : 0f;
        //    float reachSixthRate = openFifthChamber > 0 ? (float)openSixthChamber / openFifthChamber : 0f;
        //    float reachFinalRate = openSixthChamber > 0 ? (float)openFinalChamber / openSixthChamber : 0f;

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
        //        ImGui.Text($"{mapsSinceLastClear}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Total runs:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{allResults.Count()}");
        //        ImGui.TableNextColumn();
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached 2nd chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openSecondChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachSecondRate)}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached 3rd chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openThirdChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachThirdRate)}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached 4th chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openFourthChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachFourthRate)}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached 5th chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openFifthChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachFifthRate)}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached 6th chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openSixthChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachSixthRate)}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text("Reached final chamber:");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{openFinalChamber}");
        //        ImGui.TableNextColumn();
        //        ImGui.Text($"{String.Format("{0:P}%", reachFinalRate)}");
        //        ImGui.TableNextColumn();

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
