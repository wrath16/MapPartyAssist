using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class TestFunctionWindow : Window, IDisposable {

        private Plugin Plugin;

        public TestFunctionWindow(Plugin plugin) : base("MPA Test Functions") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {

            if(ImGui.Button("Show all Duties")) {
                foreach(var duty in Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()) {
                    PluginLog.Debug($"id: {duty.RowId} name: {duty.Name}");
                }
            }

            if(ImGui.Button("Maps Table")) {
                ShowMapsTable();
            }

            if(ImGui.Button("Player Table")) {
                ShowPlayerTable();
            }

            if(ImGui.Button("DR Table")) {
                ShowDRTable();
            }

            //if(ImGui.Button("Test Last Map Equality")) {
            //    TestMapEquality();
            //}

            //if(ImGui.Button("Test Last Map Contains")) {
            //    TestMapContains();
            //}

            if(ImGui.Button("Check and Archive Maps")) {
                Plugin.MapManager.CheckAndArchiveMaps(Plugin.CurrentPartyList);
            }


            if(ImGui.Button("Save+Refresh")) {
                Plugin.Save();
            }

            if(ImGui.Button("Import")) {
                Plugin.StorageManager.Import();
            }

            if(ImGui.Button("fix records")) {
                FixRecords();
            }

        }

        private void ShowMapsTable() {
            var maps = Plugin.StorageManager.GetMaps().Query().ToList();
            foreach(var map in maps) {
                //PluginLog.Debug($"owner:{map.Owner} name: {map.Name} date: {map.Time} archived: {map.IsArchived} deleted: {map.IsDeleted}");
                PluginLog.Debug(String.Format("Owner: {0,-35} Name: {1,-25} Time: {2,-23} IsPortal: {3,-5} IsArchived: {4,-5} IsDeleted: {5,-5}", map.Owner, map.Name, map.Time, map.IsPortal, map.IsArchived, map.IsDeleted));
            }
        }

        private void ShowPlayerTable() {
            var players = Plugin.StorageManager.GetPlayers().Query().ToList();
            foreach(var player in players) {
                PluginLog.Debug(String.Format("{0,-35} isSelf: {1,-8} lastJoined: {2,-10}", player.Key, player.IsSelf, player.LastJoined));
            }
        }

        private void ShowDRTable() {
            var dutyResults = Plugin.StorageManager.GetDutyResults().Query().ToList();
            foreach(var results in dutyResults) {
                PluginLog.Debug(String.Format("Time: {0,-23} Owner:Owner: {5,-35} Duty: {1,-40} CheckpointCount: {2,-2} isComplete: {3,-5} isPickup: {4, -5} totalGil: {6,-10}", results.Time, results.DutyName, results.CheckpointResults.Count, results.IsComplete, results.IsPickup, results.Owner, results.TotalGil));
            }
        }


        private void TestMapEquality() {
            var map = Plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last();
            var storageMap = Plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = map.Equals(storageMap);

            PluginLog.Debug($"{sameMap}");
        }

        private void TestMapContains() {
            var maps = new List<MPAMap> {
                Plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last()
            };

            var storageMap = Plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = maps.Contains(storageMap);

            PluginLog.Debug($"{sameMap}");
        }

        private void ShowLastDR() {
            var dutyResults = Plugin.StorageManager.GetDutyResults().Query().ToList().Last();
            PluginLog.Debug($"{dutyResults.Map.IsArchived}");
        }

        private void FixRecords() {
            ////fix ned
            var dutyResults = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner == "Ned Thedestroyer Leviathan" && dr.TotalGil == 45000 && dr.Time.Minute == 22).ToList().First();
            foreach(var cp in dutyResults.CheckpointResults) {
                cp.IsReached = true;
            }
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[1]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[2]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[3]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[4]));
            //dutyResults.TotalGil = 45000;
            //dutyResults.IsComplete = true;

            ////fix rurushi #2
            //var dutyResults2 = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner == "Rurushi Stella Malboro" && dr.IsPickup).ToList().First();



            ////fix sayane
            //var map = Plugin.StorageManager.GetMaps().Query().Where(m => m.Owner == "Sayane Miu Midgardsormr" && m.Time.Minute == 45).First();
            //var dutyResults3 = new DutyResults(276, "the hidden canals of uznair", Plugin.CurrentPartyList, "Sayane Miu Midgardsormr");
            ////dutyResults3.Time = map.Time;
            //dutyResults3.TotalGil = dutyResults2.TotalGil;
            //dutyResults3.CheckpointResults = dutyResults2.CheckpointResults;

            //dutyResults2.CheckpointResults = new();
            //dutyResults2.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0]));
            //dutyResults2.TotalGil = 20000;


            Plugin.StorageManager.UpdateDutyResults(dutyResults);
            //Plugin.StorageManager.UpdateDutyResults(dutyResults2);
            //Plugin.StorageManager.AddDutyResults(dutyResults3);


            //var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner =="Sayane Miu Midgardsormr").ToList().Last();
            //dr.Map = map;
            //Plugin.StorageManager.UpdateDutyResults(dr);

        }
    }
}
