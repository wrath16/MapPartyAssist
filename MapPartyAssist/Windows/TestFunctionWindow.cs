using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Lumina.Data.Parsing.Layer.LayerCommon;

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
            //if(ImGui.Button("Add DR to DB")) {
            //    Plugin.TestFuncAddDRToDB();
            //}

            //if(ImGui.Button("Check DR in DB")) {
            //    Plugin.TestGetDRFromDB();
            //}

            //if(ImGui.Button("Find and Update Map")) {
            //    Plugin.TestUpdateMap();
            //}

            if(ImGui.Button("Maps Table")) {
                ShowMapsTable();
            }

            if(ImGui.Button("Player Table")) {
                ShowPlayerTable();
            }

            //if(ImGui.Button("Test Last Map Equality")) {
            //    TestMapEquality();
            //}

            //if(ImGui.Button("Test Last Map Contains")) {
            //    TestMapContains();
            //}

            if(ImGui.Button("Check and Archive Maps")) {
                Plugin.StorageManager.Import();
            }


            if(ImGui.Button("Save+Refresh")) {
                Plugin.Save();
            }

            if(ImGui.Button("Import")) {
                Plugin.StorageManager.Import();
            }

        }


        public void ShowMapsTable() {
            var maps = Plugin.StorageManager.GetMaps().Query().ToList();
            foreach(var map in maps) {
                //PluginLog.Debug($"owner:{map.Owner} name: {map.Name} date: {map.Time} archived: {map.IsArchived} deleted: {map.IsDeleted}");
                PluginLog.Debug(String.Format("Owner: {0,-35} Name: {1,-25} Time: {2,-23} IsPortal: {3,-5} IsArchived: {4,-5} IsDeleted: {5,-5}", map.Owner, map.Name, map.Time, map.IsPortal, map.IsArchived, map.IsDeleted));
            }
        }

        public void ShowPlayerTable() {
            var players = Plugin.StorageManager.GetPlayers().Query().ToList();
            foreach(var player in players) {
                PluginLog.Debug(String.Format("{0,-35} isSelf: {1,-8} lastJoined: {2,-10}", player.Key, player.IsSelf, player.LastJoined));
                //PluginLog.Debug($"{player.Key} isSelf: {player.IsSelf} lastJoined: {player.LastJoined}");
            }
        }

        public void TestMapEquality() {
            var map = Plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last();
            var storageMap = Plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = map.Equals(storageMap);

            PluginLog.Debug($"{sameMap}");
        }

        public void TestMapContains() {
            var maps = new List<MPAMap> {
                Plugin.CurrentPartyList.Values.FirstOrDefault().Maps.Last()
            };

            var storageMap = Plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = maps.Contains(storageMap);

            PluginLog.Debug($"{sameMap}");
        }
    }
}
