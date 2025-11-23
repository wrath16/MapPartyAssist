using Dalamud.Game;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class MigrationManager {
        private Plugin _plugin;
        public MigrationManager(Plugin plugin) {
            _plugin = plugin;
        }

        internal async Task CheckAndMigrate() {
            try {
                if(_plugin.Configuration.Version < 2) {
                    Plugin.Log.Information("Migration: Importing from configuration to DB...");
                    await ImportFromConfiguration();
                }
                if(_plugin.Configuration.Version < 3) {
                    Plugin.Log.Information("Migration: Setting map types and territory IDs...");
                    await UpdateMapTypeAndTerritoryIdFromNameAndZone();
                }
                await _plugin.Refresh();
            } catch(Exception e) {
                Plugin.Log.Error("Migration failed!");
                Plugin.Log.Error(e.Message);
                Plugin.Log.Error(e.StackTrace ?? "");
            }
        }

#pragma warning disable 612, 618
        private async Task ImportFromConfiguration() {
            Plugin.Log.Information("Importing data from config file into database...");

            List<MPAMap> maps = new();

            foreach(var player in _plugin.Configuration.RecentPartyList.Where(p => p.Value.Maps != null)) {
                foreach(var map in player.Value.Maps!) {
                    if(map.Owner.IsNullOrEmpty()) {
                        map.Owner = player.Key;
                    }
                    maps.Add(map);
                }
                player.Value.Maps = null;
                await _plugin.StorageManager.AddPlayer(player.Value);
            }
            await _plugin.StorageManager.AddMaps(maps);

            foreach(var dutyResults in _plugin.Configuration.DutyResults) {
                //find map...
                var map = _plugin.MapManager.FindMapForDutyResults(dutyResults);
                dutyResults.Map = map;
                //if(map != null) {
                //    map.DutyResults = dutyResults;
                //}
            }
            await _plugin.StorageManager.AddDutyResults(_plugin.Configuration.DutyResults);

            _plugin.Configuration.DutyResults = new();
            _plugin.Configuration.RecentPartyList = new();

            _plugin.Configuration.Version = 2;
        }
#pragma warning restore 612, 618

        private async Task UpdateMapTypeAndTerritoryIdFromNameAndZone() {
            var allMaps = _plugin.StorageManager.GetMaps().Query().Where(m => m.MapType == null || m.TerritoryId == null).ToList();
            List<MPAMap> updatedMaps = new();

            foreach(var map in allMaps) {
                //set map type
                if(map.MapType == null && !map.IsManual && !map.Name.IsNullOrEmpty()) {
                    var row = _plugin.GetRowId<EventItem>(map.Name, "Singular", GrammarCase.Nominative, ClientLanguage.English);
                    if(row != null) {
                        map.MapType = MapHelper.IdToMapTypeMap[(uint)row];
                        updatedMaps.Add(map);
                    }
                }
                //set territoryid
                if(map.TerritoryId == null && !map.Zone.IsNullOrEmpty()) {
                    var placeNameId = _plugin.GetRowId<PlaceName>(map.Zone, "Name", GrammarCase.Nominative, ClientLanguage.English);
                    //assume first territory with name is correct
                    foreach(var territory in _plugin.DataManager.GetExcelSheet<TerritoryType>()!) {
                        if(territory.PlaceName.Value!.Name.ToString().Equals(map.Zone, StringComparison.OrdinalIgnoreCase)) {
                            map.TerritoryId = (int?)territory.RowId;
                            updatedMaps.Add(map);
                            break;
                        }
                    }
                }
            }
            await _plugin.StorageManager.UpdateMaps(updatedMaps);
            _plugin.Configuration.Version = 3;
        }

        internal async Task SetClearedDutiesToComplete() {
            Plugin.Log.Information("Setting cleared duties to complete");
            var results = _plugin.StorageManager.GetDutyResults().Query().Where(m => !m.IsComplete).ToList();
            foreach(var result in results) {
                try {
                    var duty = _plugin.DutyManager.Duties[result.DutyId];
                    if(duty.Checkpoints.Count == result.CheckpointResults.Count && result.CheckpointResults.Last().IsReached) {
                        result.IsComplete = true;
                        result.CompletionTime = result.Time;
                    }
                } catch {
                    continue;
                }
            }
            await _plugin.StorageManager.UpdateDutyResults(results);
        }
    }
}
