using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;
using MapPartyAssist.Types;

namespace MapPartyAssist.Windows {
    internal class StatsWindow : Window, IDisposable {

        private Plugin Plugin;

        public StatsWindow(Plugin plugin) : base("Treasure Map Stats") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {

            if(ImGui.BeginTabBar("StatsTabBar", ImGuiTabBarFlags.None)) {
                if(ImGui.BeginTabItem("The Hidden Canals of Uznair")) {

                    //if(ImGui.Button("Test Function")) {
                    //    Plugin.TestFunction();
                    //}


                    var allResults = Plugin.Configuration.DutyResults.Where(dr => {
                        //only include maps with checkpoint results
                        //saving as DutyResults exactly...need to change that?

                        //return dr.GetType() == typeof(HiddenCanalsOfUznairResults) && dr.CheckpointResults.Count > 0;
                        return dr.DutyId == 276;// && dr.CheckpointResults.Count > 0;
                    });
                    //var allResults = Plugin.Configuration.DutyResults.Where(dr => dr.GetType() == typeof(HiddenCanalsOfUznairResults));
                    int openSecondChamber = 0;
                    int openThirdChamber = 0;
                    int openFourthChamber = 0;
                    int openFifthChamber = 0;
                    int openSixthChamber = 0;
                    int openFinalChamber = 0;
                    int totalGil = 0;
                    foreach(var result in allResults) {
                        totalGil += result.TotalGil;

                        if(result.CheckpointResults.Count > 0) {
                            var lastCheckpoint = result.CheckpointResults.Last();

                            //find the last reached door checkpoint
                            for(int i = 1; (!lastCheckpoint.Checkpoint.Name.StartsWith("Open") || !lastCheckpoint.IsReached) && i < result.CheckpointResults.Count; i++) {
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
                        } 
                    }


                    ImGui.Text("All-time:");

                    ImGui.Text("Total Runs:");
                    ImGui.SameLine();
                    ImGui.Text($"{allResults.Count()}");
                    ImGui.Text("Reached 2nd Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openSecondChamber}");
                    ImGui.Text("Reached 3rd Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openThirdChamber}");
                    ImGui.Text("Reached 4th Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openFourthChamber}");
                    ImGui.Text("Reached 5th Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openFifthChamber}");
                    ImGui.Text("Reached 6th Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openSixthChamber}");
                    ImGui.Text("Reached final Chamber:");
                    ImGui.SameLine();
                    ImGui.Text($"{openFinalChamber}");
                    ImGui.Text("Total Gil Earned: ");
                    ImGui.SameLine();
                    ImGui.Text($"{totalGil.ToString("N0")}");
                }
            }
        }

        public override void PreDraw() {
        }
    }
}
