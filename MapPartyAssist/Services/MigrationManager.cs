using Dalamud;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Services {
    internal class MigrationManager {
        private Plugin _plugin;
        public MigrationManager(Plugin plugin) {
            _plugin = plugin;
        }

        internal void CheckAndMigrate() {
            try {
                if(_plugin.Configuration.Version < 2) {
                    _plugin.Log.Information("Migration: Importing from configuration to DB...");
                    ImportFromConfiguration();
                }
                if(_plugin.Configuration.Version < 3) {
                    _plugin.Log.Information("Migration: Setting map types and territory IDs...");
                    UpdateMapTypeAndTerritoryIdFromNameAndZone();
                }
            } catch(Exception e) {
                _plugin.Log.Error("Migration failed!");
                _plugin.Log.Error(e.Message);
                _plugin.Log.Error(e.StackTrace ?? "");
            }
        }

#pragma warning disable 612, 618
        private void ImportFromConfiguration() {
            _plugin.Log.Information("Importing data from config file into database...");

            List<MPAMap> maps = new();

            foreach(var player in _plugin.Configuration.RecentPartyList.Where(p => p.Value.Maps != null)) {
                foreach(var map in player.Value.Maps!) {
                    if(map.Owner.IsNullOrEmpty()) {
                        map.Owner = player.Key;
                    }
                    maps.Add(map);
                }
                player.Value.Maps = null;
                _plugin.StorageManager.AddPlayer(player.Value);
            }
            _plugin.StorageManager.AddMaps(maps);

            foreach(var dutyResults in _plugin.Configuration.DutyResults) {
                //find map...
                var map = _plugin.MapManager.FindMapForDutyResults(dutyResults);
                dutyResults.Map = map;
                //if(map != null) {
                //    map.DutyResults = dutyResults;
                //}
            }
            _plugin.StorageManager.AddDutyResults(_plugin.Configuration.DutyResults);

            _plugin.Configuration.DutyResults = new();
            _plugin.Configuration.RecentPartyList = new();

            _plugin.Configuration.Version = 2;
            _plugin.Save();
        }
#pragma warning restore 612, 618


        private void UpdateMapTypeAndTerritoryIdFromNameAndZone() {
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
            _plugin.StorageManager.UpdateMaps(updatedMaps, false);
            _plugin.Configuration.Version = 3;
            _plugin.Save();
        }
    }
}
