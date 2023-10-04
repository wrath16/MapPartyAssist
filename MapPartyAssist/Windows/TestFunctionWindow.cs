#pragma warning disable
#if DEBUG
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
    //for testing purposes only

    public class TestFunctionWindow : Window {

        private Plugin _plugin;

        public TestFunctionWindow(Plugin plugin) : base("MPA Test Functions") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            this._plugin = plugin;
        }

        public override void Draw() {

            if(ImGui.Button("Show all Duties")) {
                foreach(var duty in _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()) {
                    _plugin.Log.Debug($"id: {duty.RowId} name: {duty.Name}");
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

            if(ImGui.Button("Import Table")) {
                ShowImportTable();
            }


            if(ImGui.Button("Last 3 DutyResults")) {
                var dutyResults = _plugin.StorageManager.GetDutyResults().Query().OrderByDescending(dr => dr.Time).Limit(3).ToList();
                foreach(var results in dutyResults) {
                    PrintDutyResults(results);
                }
            }


            //if(ImGui.Button("Test Last Map Equality")) {
            //    TestMapEquality();
            //}

            //if(ImGui.Button("Test Last Map Contains")) {
            //    TestMapContains();
            //}

            if(ImGui.Button("Check and Archive Maps")) {
                _plugin.MapManager.CheckAndArchiveMaps();
            }


            if(ImGui.Button("Save+Refresh")) {
                _plugin.Save();
            }

            if(ImGui.Button("Import Config")) {
                _plugin.StorageManager.Import();
            }


            if(ImGui.Button("Drop Import Table")) {
                _plugin.StorageManager.GetDutyResultsImports().DeleteAll();
            }

            //if(ImGui.Button("fix record")) {
            //    //var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.DutyId == 745).OrderBy(dr => dr.Time).ToList().Last();
            //    //dr.CheckpointResults = new();
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[0], Summon.Lesser, "secret serpent", false, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[1], null, null, false, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[2], Summon.Greater, "secret porxie", true, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[3], null, null, false, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[4], Summon.Elder, "secret keeper", true, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[5], null, null, false, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[6], Summon.Greater, "greedy pixie", true, true));
            //    //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[7], null, null, false, true));
            //    //Plugin.StorageManager.UpdateDutyResults(dr);

            //    var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.DutyId == 909).OrderBy(dr => dr.Time).ToList();

            //    for(int i = 0; i < dr.Count; i++) {
            //        var res = dr[i];
            //        for(int j = 0; j < dr[i].CheckpointResults.Count; j += 2) {
            //            dr[i].CheckpointResults.Insert(j + 1, new RouletteCheckpointResults(Plugin.DutyManager.Duties[909].Checkpoints[j + 1], null, null, false, true));
            //            if(j != 0) {
            //                dr[i].CheckpointResults[j].Checkpoint = Plugin.DutyManager.Duties[909].Checkpoints[j];
            //            }
            //        }
            //    }

            //    //dr.CheckpointResults = new();
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[1], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[2], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[3], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[4], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[5], true));
            //    //dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[6], true));
            //    Plugin.StorageManager.UpdateDutyResults(dr);
            //}

            if(ImGui.Button("Set Map Status")) {
                _plugin.MapManager.Status = StatusLevel.CAUTION;
                _plugin.MapManager.StatusMessage = "Multiple map owner candidates found, verify last map ownership.";
            }

            if(ImGui.Button("Get Player Current Position")) {
                Vector2 coords = WorldPosToMapCoords(_plugin.ClientState.LocalPlayer.Position);
                _plugin.Log.Debug($"X: {_plugin.ClientState.LocalPlayer.Position.X} Y: {_plugin.ClientState.LocalPlayer.Position.Y}");
                _plugin.Log.Debug($"coordsX: {coords.X} coordsY: {coords.Y}");
                //Plugin.ClientState.LocalPlayer.Position.
            }

            if(ImGui.Button("Get Map Position")) {
                var map = _plugin.CurrentPartyList.ElementAt(0).Value.MapLink;
                _plugin.Log.Debug($"XCoord: {map.GetMapLinkPayload().XCoord}");
                _plugin.Log.Debug($"YCoord: {map.GetMapLinkPayload().YCoord}");
                _plugin.Log.Debug($"RawX: {map.RawX}");
                _plugin.Log.Debug($"RawY: {map.RawY}");
                //_plugin.Log.Debug($"X: {Plugin.ClientState.LocalPlayer.Position.X} Y: {Plugin.ClientState.LocalPlayer.Position.Y}");
            }

            if(ImGui.Button("Distance to Map Link")) {
                var distance = _plugin.MapManager.GetDistanceToMapLink(_plugin.CurrentPartyList.ElementAt(0).Value.MapLink);
                _plugin.Log.Debug($"Distance: {distance}");
            }

            if(ImGui.Button("Check closest link player")) {
                _plugin.Log.Debug($"{_plugin.MapManager.GetPlayerWithClosestMapLink(_plugin.CurrentPartyList.Values.ToList()).Key} has the closest map link");
            }


            if(ImGui.Button("Check map and member nullability")) {
                //var map = Plugin.StorageManager.GetMaps().Query().First();
                //_plugin.Log.Debug($"Checking validity of {map.Id}");
                //_plugin.Log.Debug($"Is valid? {map.IsValid()}");

                //var dr = Plugin.StorageManager.GetDutyResults().Query().ToList().Last();

                var dr = _plugin.StorageManager.GetDutyResults().Query().ToList();
                foreach(var x in dr) {
                    _plugin.Log.Debug($"Checking validity of {x.Id}....valid? {_plugin.StorageManager.ValidateDataType(x)}");
                    //_plugin.Log.Debug($"Is valid? {Plugin.StorageManager.ValidateDataType(dr)}");
                }
            }

            if(ImGui.Button("VALIDATE ALL DUTYRESULTS")) {

                var dr = _plugin.StorageManager.GetDutyResults().Query().ToList();
                foreach(var x in dr) {
                    _plugin.StorageManager.ValidateDataType(x, true);
                }
                _plugin.StorageManager.UpdateDutyResults(dr);
            }


            if(ImGui.Button("check map version")) {

                var map = _plugin.StorageManager.GetMaps().Query().Where(m => m.DutyName.Equals("the hidden canals of uznair", StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.Time).FirstOrDefault();
                _plugin.Log.Debug($"map id: {map.Id.ToString()}");
                _plugin.StorageManager.UpdateMap(map);
            }

            //if(ImGui.Button("altars lesser string")) {
            //    _plugin.Log.Debug(Plugin.DutyManager.Duties[586].GetSummonPatternString(Summon.Lesser));
            //}

            //if(ImGui.Button("altars greater string")) {
            //    _plugin.Log.Debug(Plugin.DutyManager.Duties[586].GetSummonPatternString(Summon.Greater));
            //}

            //if(ImGui.Button("altars elder string")) {
            //    _plugin.Log.Debug(Plugin.DutyManager.Duties[586].GetSummonPatternString(Summon.Elder));
            //}

        }
        private static Vector2 WorldPosToMapCoords(Vector3 pos) {
            var xInt = (int)(MathF.Round(pos.X, 3, MidpointRounding.AwayFromZero) * 1000);
            var yInt = (int)(MathF.Round(pos.Z, 3, MidpointRounding.AwayFromZero) * 1000);
            return new Vector2((int)(xInt * 0.001f * 1000f), (int)(yInt * 0.001f * 1000f));
        }

        private void ShowMapsTable() {
            var maps = _plugin.StorageManager.GetMaps().Query().ToList();
            foreach(var map in maps) {
                //_plugin.Log.Debug($"owner:{map.Owner} name: {map.Name} date: {map.Time} archived: {map.IsArchived} deleted: {map.IsDeleted}");
                _plugin.Log.Debug(String.Format("Owner: {0,-35} Name: {1,-25} Time: {2,-23} IsPortal: {3,-5} IsArchived: {4,-5} IsDeleted: {5,-5}", map.Owner, map.Name, map.Time, map.IsPortal, map.IsArchived, map.IsDeleted));
            }
        }

        private void ShowPlayerTable() {
            var players = _plugin.StorageManager.GetPlayers().Query().ToList();
            foreach(var player in players) {
                _plugin.Log.Debug(String.Format("{0,-35} isSelf: {1,-8} lastJoined: {2,-10}", player.Key, player.IsSelf, player.LastJoined));
            }
        }

        private void ShowDRTable() {
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().ToList();
            foreach(var results in dutyResults) {
                PrintDutyResults(results);
            }
        }

        private void ShowImportTable() {
            var imports = _plugin.StorageManager.GetDutyResultsImports().Query().ToList();
            foreach(var import in imports) {
                _plugin.Log.Debug(String.Format("Time: {0,-23} DutyId: {1,-4} Runs: {2,-5} Clears: {3,-5} HasGil: {4, -5} HasCheckpoints: {5, -5} HasSequence: {6, -5} HasSummons: {7, -5}", import.Time, import.DutyId, import.TotalRuns, import.TotalClears, import.TotalGil != null, import.CheckpointTotals != null, import.ClearSequence != null, import.SummonTotals != null));
            }
        }

        private void PrintDutyResults(DutyResults dr) {
            _plugin.Log.Debug(String.Format("Time: {0,-23} Owner: {5,-35} Duty: {1,-40} CheckpointCount: {2,-2} isComplete: {3,-5} isPickup: {4, -5} totalGil: {6,-10}", dr.Time, dr.DutyName, dr.CheckpointResults.Count, dr.IsComplete, dr.IsPickup, dr.Owner, dr.TotalGil));
            foreach(var checkpointResults in dr.CheckpointResults) {
                _plugin.Log.Debug(String.Format("Name: {0,-20} isReached: {1,-5} isSaved: {2,-5}, Monster: {3,-20}, Type: {4, -9}", checkpointResults.Checkpoint.Name, checkpointResults.IsReached, checkpointResults.IsSaved, checkpointResults.MonsterName, checkpointResults.SummonType));
            }
        }

        private void TestMapEquality() {
            var map = _plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last();
            var storageMap = _plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = map.Equals(storageMap);

            _plugin.Log.Debug($"{sameMap}");
        }

        private void TestMapContains() {
            var maps = new List<MPAMap> {
                _plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last()
            };

            var storageMap = _plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = maps.Contains(storageMap);

            _plugin.Log.Debug($"{sameMap}");
        }

        private void ShowLastDR() {
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().ToList().Last();
            _plugin.Log.Debug($"{dutyResults.Map.IsArchived}");
        }

        private void FixRecords() {
            ////fix ned
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner == "Ned Thedestroyer Leviathan" && dr.TotalGil == 45000 && dr.Time.Minute == 22).ToList().First();
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


            _plugin.StorageManager.UpdateDutyResults(dutyResults);
            //Plugin.StorageManager.UpdateDutyResults(dutyResults2);
            //Plugin.StorageManager.AddDutyResults(dutyResults3);


            //var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner =="Sayane Miu Midgardsormr").ToList().Last();
            //dr.Map = map;
            //Plugin.StorageManager.UpdateDutyResults(dr);

        }
    }
}
#endif
#pragma warning restore
