using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class MapManager : IDisposable {

        private static int _digThresholdSeconds = 3; //window within which to block subsequent dig while awaiting treasure coffer message
        private static int _digFallbackSeconds = 60 * 10; //if no treasure coffer opened after this time of dig locking in, unlock and allow digging
        private static int _portalBlockSeconds = 60; //timer to block portal from adding a duplicate map after finishing a chest
        private static int _addMapDelaySeconds = 2; //delay added onto adding a map to avoid double-counting self maps with another player using dig at same time
        private static TextInfo _textInfo = new CultureInfo("en-US", false).TextInfo;

        private Plugin Plugin;
        private string _diggerKey = "";
        private DateTime _digTime = DateTime.UnixEpoch;
        private bool _isDigLockedIn = false;
        private DateTime _portalBlockUntil = DateTime.UnixEpoch;

        public string LastMapPlayerKey { get; private set; } = "";

        public MapManager(Plugin plugin) {
            Plugin = plugin;
            Plugin.DutyState.DutyStarted += OnDutyStart;
            Plugin.ChatGui.ChatMessage += OnChatMessage;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            ResetDigStatus();
        }

        public void Dispose() {
            PluginLog.Debug("disposing map manager");
            Plugin.DutyState.DutyStarted -= OnDutyStart;
            Plugin.ChatGui.ChatMessage -= OnChatMessage;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            var dutyId = Plugin.Functions.GetCurrentDutyId();
            var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);

            //set the duty name
            //should do this in a more robust way...
            if(Regex.IsMatch(duty.Name.ToString(), @"uznair|aquapolis|lyhe ghiah|gymnasion agonon|excitatron 6000$", RegexOptions.IgnoreCase)) {
                //fallback for cases where we miss the map
                if(Plugin.CurrentPartyList.Count > 0 && !LastMapPlayerKey.IsNullOrEmpty() && (DateTime.Now - Plugin.CurrentPartyList[LastMapPlayerKey].Maps.Last().Time).TotalSeconds < _digFallbackSeconds) {
                    var lastMap = Plugin.CurrentPartyList[LastMapPlayerKey].Maps.Last();
                    lastMap.IsPortal = true;
                    lastMap.DutyName = duty.Name;
                    Plugin.StorageManager.UpdateMap(lastMap);
                }
            }
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            ResetDigStatus();
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            bool newMapFound = false;
            bool isPortal = false;
            string key = "";
            string mapType = "";

            if((int)type == 2361) {
                //party member opens portal, after block time
                if(Regex.IsMatch(message.ToString(), @"complete[s]? preparations to enter the portal.$")) {
                    if(_portalBlockUntil > DateTime.Now) {
                        //CurrentPartyList[LastMapPlayerKey].Maps.Last().IsPortal = true;
                    } else {
                        //thief's maps
                        var playerPayload = (PlayerPayload)message.Payloads.First(p => p.Type == PayloadType.Player);
                        key = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                        newMapFound = true;
                        isPortal = true;
                    }
                }
                //self map detection
            } else if((int)type == 2105 || (int)type == 2233) {
                if(Regex.IsMatch(message.ToString(), @"map crumbles into dust.$")) {
                    newMapFound = true;
                    mapType = Regex.Match(message.ToString(), @"\w* [\w']* map(?=\scrumbles into dust)").ToString();
                    key = $"{Plugin.ClientState.LocalPlayer!.Name} {Plugin.ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                    //clear dig info just in case to prevent double-adding map if another player uses dig at the same time
                    ResetDigStatus();
                } else if(Regex.IsMatch(message.ToString(), @"discover a treasure coffer!$")) {
                    if(!_diggerKey.IsNullOrEmpty()) {
                        PluginLog.Debug($"Time since dig: {(DateTime.Now - _digTime).TotalMilliseconds} ms");
                    }
                    //lock in dig only if we have a recent digger
                    _isDigLockedIn = !_diggerKey.IsNullOrEmpty() && ((DateTime.Now - _digTime).TotalSeconds < _digThresholdSeconds);
                } else if(Regex.IsMatch(message.ToString(), @"releasing a powerful musk into the air!$")) {
                    Task.Delay(_addMapDelaySeconds * 1000).ContinueWith(t => {
                        if(!_diggerKey.IsNullOrEmpty()) {
                            AddMap(Plugin.CurrentPartyList[_diggerKey], Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(Plugin.ClientState.TerritoryType)?.PlaceName.Value?.Name!);
                        }
                        //have to reset here in case you fail to defeat the treasure chest enemies -_ -
                        ResetDigStatus();
                    });
                } else if(Regex.IsMatch(message.ToString(), @"defeat all the enemies drawn by the trap!$")) {
                    ResetDigStatus();
                    //block portals from adding maps for a brief period to avoid double counting
                    _portalBlockUntil = DateTime.Now.AddSeconds(_portalBlockSeconds);
                }
            } else if((int)type == 4139) {
                //party member uses dig
                if(Regex.IsMatch(message.ToString(), @"uses Dig.$")) {
                    var playerPayload = (PlayerPayload)message.Payloads.First(p => p.Type == PayloadType.Player);
                    var newDigTime = DateTime.Now;
                    //register dig if no dig locked in or fallback time elapsed AND no digger registered or threshold time elapsed
                    if((!_isDigLockedIn || (newDigTime - _digTime).TotalSeconds > _digFallbackSeconds) && (_diggerKey.IsNullOrEmpty() || (newDigTime - _digTime).TotalSeconds > _digThresholdSeconds)) {
                        ResetDigStatus();
                        _diggerKey = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                        _digTime = newDigTime;
                    }
                }
            } else if(type == XivChatType.Party || type == XivChatType.Say) {
                //getting map link
                var mapPayload = (MapLinkPayload)message.Payloads.FirstOrDefault(p => p.Type == PayloadType.MapLink);
                var senderPayload = (PlayerPayload)sender.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player);

                if(senderPayload == null) {
                    string matchName = Regex.Match(sender.TextValue, @"[A-Za-z-']*\s[A-Za-z-']*$").ToString();
                    key = $"{matchName} {Plugin.ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                } else {
                    key = $"{senderPayload.PlayerName} {senderPayload.World.Name}";
                }
                if(mapPayload != null && Plugin.CurrentPartyList.ContainsKey(key)) {
                    //CurrentPartyList[key].MapLink = SeString.CreateMapLink(mapPayload.TerritoryType.RowId, mapPayload.Map.RowId, mapPayload.XCoord, mapPayload.YCoord);
                    Plugin.CurrentPartyList[key].MapLink = mapPayload;
                    Plugin.StorageManager.UpdatePlayer(Plugin.CurrentPartyList[key]);
                    Plugin.Save();
                }
            }

            if(newMapFound && Plugin.CurrentPartyList.Count > 0) {
                AddMap(Plugin.CurrentPartyList[key], Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(Plugin.ClientState.TerritoryType)?.PlaceName.Value?.Name!, mapType, false, isPortal);
            }
        }

        public void AddMap(MPAMember player, string zone = "", string type = "", bool isManual = false, bool isPortal = false) {
            PluginLog.Information($"Adding new map to {player.Key}");
            //zone ??= DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name!;
            //zone = _textInfo.ToTitleCase(zone);
            type = _textInfo.ToTitleCase(type);
            var newMap = new MPAMap(type, DateTime.Now, zone, isManual, isPortal);
            newMap.Owner = player.Key;
            player.Maps.Add(newMap);
            if(!isManual) {
                player.MapLink = null;
            }
            LastMapPlayerKey = player.Key;

            //add to DB
            Plugin.StorageManager.AddMap(newMap);

            //this can cause race conditions with the config file since we're saving in the other thread
            //Plugin.Save();
        }

        public void RemoveLastMap(MPAMember player) {
            var lastMap = player.Maps.Where(m => !m.IsDeleted && !m.IsArchived).Last();
            if(lastMap != null) {
                lastMap.IsDeleted = true;

                Plugin.Save();

                var lastMapStorage = Plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted && !m.IsArchived).ToList().Last();
                if(lastMapStorage != null) {
                    lastMapStorage.IsDeleted = true;
                    Plugin.StorageManager.UpdateMap(lastMapStorage);
                }
            }
            //player.Maps.Where(m => !m.IsDeleted && !m.IsArchived).Last().IsDeleted = true;
            //player.Maps.RemoveAt(player.Maps.Count - 1);

        }

        //archive all of the maps for the given list
        private void ForceArchiveAllMaps(Dictionary<string, MPAMember> list) {
            foreach(var player in list.Values) {
                foreach(var map in player.Maps) {
                    map.IsArchived = true;
                }
            }
        }

        public void ClearAllMaps() {

            PluginLog.Information("Archiving all maps...");

            //TODO: only do this for current maps

            ForceArchiveAllMaps(Plugin.Configuration.RecentPartyList);
            ForceArchiveAllMaps(Plugin.FakePartyList);
            Plugin.BuildRecentPartyList();

            var maps = Plugin.StorageManager.GetMaps().Query().ToList();
            maps.ForEach(m => m.IsArchived = true);
            Plugin.StorageManager.UpdateMaps(maps);

            //foreach(var map in maps) {
            //    map.IsArchived = true;
            //    Plugin.StorageManager.UpdateMap(map);
            //}


            //foreach(var p in Plugin.Configuration.RecentPartyList) {
            //    Plugin.StorageManager.UpdateMaps(p.Value.Maps);
            //    //foreach(var m in p.Value.Maps) {
            //    //    Plugin.StorageManager.UpdateMap(m);
            //    //}
            //}

            //Plugin.Save();
        }

        public void ArchiveMaps(IEnumerable<MPAMap> maps) {
            PluginLog.Information("Archiving maps...");
            //get from storage
            var storageMaps = Plugin.StorageManager.GetMaps().Query().Where(m => maps.Contains(m)).ToList();
            foreach(var map in storageMaps) {
                map.IsArchived = true;
            }
            Plugin.StorageManager.UpdateMaps(storageMaps);
            //Plugin.Save();

            //maps.ToList().ForEach(m => m.IsArchived = true);
            //Plugin.StorageManager.UpdateMaps(maps);
        }

        public void DeleteMaps(IEnumerable<MPAMap> maps) {
            PluginLog.Information("Deleting maps...");
            //get from storage
            //var storageMaps = Plugin.StorageManager.GetMaps().Query().Where(m => maps.Contains(m)).ToList();
            //foreach(var map in storageMaps) {
            //    map.IsDeleted = true;
            //}
            //Plugin.StorageManager.UpdateMaps(storageMaps);

            maps.ToList().ForEach(m => m.IsDeleted = true);
            Plugin.StorageManager.UpdateMaps(maps);
            //Plugin.Save();
        }



        public void CheckAndArchiveMaps(Dictionary<string, MPAMember> list) {
            DateTime currentTime = DateTime.Now;

            foreach(MPAMember player in list.Values) {
                foreach(MPAMap map in player.Maps) {
                    TimeSpan timeSpan = currentTime - map.Time;
                    if(timeSpan.TotalHours > Plugin.Configuration.ArchiveThresholdHours) {
                        map.IsArchived = true;
                        //Plugin.StorageManager.UpdateMap(map);
                    }
                }
            }
            
            var storageMaps = Plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived).ToList();
            foreach(var map in storageMaps) {
                TimeSpan timeSpan = currentTime - map.Time;
                map.IsArchived = timeSpan.TotalHours > Plugin.Configuration.ArchiveThresholdHours;
            }
            Plugin.StorageManager.UpdateMaps(storageMaps);

            Plugin.Save();
        }

        private void ResetDigStatus() {
            _diggerKey = "";
            _isDigLockedIn = false;
            _portalBlockUntil = DateTime.UnixEpoch;
            _digTime = DateTime.UnixEpoch;
        }
    }

}
