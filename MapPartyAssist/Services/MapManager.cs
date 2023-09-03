using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {

    internal class MapManager : IDisposable {

        public string LastMapPlayerKey { get; private set; } = "";
        public string StatusMessage { get; set; } = "";
        public StatusLevel Status { get; set; } = StatusLevel.OK;

        private static int _digThresholdSeconds = 3; //window within which to block subsequent dig while awaiting treasure coffer message
        private static int _digFallbackSeconds = 60 * 10; //if no treasure coffer opened after this time of dig locking in, unlock and allow digging
        private static int _portalBlockSeconds = 60; //timer to block portal from adding a duplicate map after finishing a chest
        private static int _addMapDelaySeconds = 2; //delay added onto adding a map to avoid double-counting self maps with another player using dig at same time
        private static TextInfo _textInfo = new CultureInfo("en-US", false).TextInfo;

        private Plugin Plugin;
        private string _diggerKey = "";
        private int _extraDigCount = 0;
        private bool _selfDig = false;
        //private Dictionary<string, DateTime> _diggers = new();
        private DateTime _digTime = DateTime.UnixEpoch;
        private bool _isDigLockedIn = false;
        private DateTime _portalBlockUntil = DateTime.UnixEpoch;

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
            //var dutyId = Plugin.Functions.GetCurrentDutyId();
            //var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);

            ////set the duty name
            //if(Plugin.IsEnglishClient() && Plugin.DutyManager.Duties.ContainsKey(dutyId)) {
            //    var lastMap = Plugin.StorageManager.GetMaps().Query().OrderBy(dr => dr.Time).ToList().Last();
            //    //fallback for cases where we miss the map
            //    if(Plugin.CurrentPartyList.Count > 0 && !LastMapPlayerKey.IsNullOrEmpty() && (DateTime.Now - lastMap.Time).TotalSeconds < _digFallbackSeconds) {
            //        //var lastMap = Plugin.CurrentPartyList[LastMapPlayerKey].Maps.Last();
            //        lastMap.IsPortal = true;
            //        lastMap.DutyName = duty.Name;
            //        Plugin.StorageManager.UpdateMap(lastMap);
            //    }
            //}
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            ResetDigStatus();
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            bool newMapFound = false;
            bool isPortal = false;
            string key = "";
            string mapType = "";

            //refuse to process if not in English
            //if(!Plugin.IsEnglishClient()) {
            //    return;
            //}

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
                        //lock in dig only if we have a recent digger
                        _isDigLockedIn = (DateTime.Now - _digTime).TotalSeconds < _digThresholdSeconds;
                    } else if(!_selfDig) {
                        PluginLog.Warning($"No eligible map owner detected!");
                        SetStatus("Unable to determine map owner, verify and add manually.", StatusLevel.ERROR);
                    }
                } else if(Regex.IsMatch(message.ToString(), @"releasing a powerful musk into the air!$")) {
                    Task.Delay(_addMapDelaySeconds * 1000).ContinueWith(t => {
                        if(!_diggerKey.IsNullOrEmpty()) {
                            AddMap(Plugin.CurrentPartyList[_diggerKey], Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(Plugin.ClientState.TerritoryType)?.PlaceName.Value?.Name!);
                            if(_extraDigCount > 0) {
                                SetStatus("Multiple map owner candidates found, verify last map ownership.", StatusLevel.CAUTION);
                            }
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
                    var diggerKey = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                    var newDigTime = DateTime.Now;
                    //_diggers.Add(key, newDigTime);
                    //_diggers.Add(playerPayload)
                    //register dig if no dig locked in or fallback time elapsed AND no digger registered or threshold time elapsed
                    bool unlockedDig = !_isDigLockedIn || (newDigTime - _digTime).TotalSeconds > _digFallbackSeconds;
                    bool noDigger = _diggerKey.IsNullOrEmpty() || (newDigTime - _digTime).TotalSeconds > _digThresholdSeconds;
                    if(unlockedDig && noDigger) {
                        ResetDigStatus();
                        _diggerKey = diggerKey;
                        _digTime = newDigTime;
                        //other diggers who may be eligible
                    } else if(unlockedDig && !noDigger) {
                        _extraDigCount++;
                    }
                }
            } else if((int)type == 2091) {
                //need this to prevent warning on own maps
                if(Regex.IsMatch(message.ToString(), @"^You use Dig\.$", RegexOptions.IgnoreCase)) {
                    _selfDig = true;
                }
            } else if(type == XivChatType.Party || type == XivChatType.Say || type == XivChatType.Alliance) {
                //getting map link
                var mapPayload = (MapLinkPayload)message.Payloads.FirstOrDefault(p => p.Type == PayloadType.MapLink);
                var senderPayload = (PlayerPayload)sender.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player);

                if(senderPayload == null) {
                    string matchName = Regex.Match(sender.TextValue, @"[A-Za-z-']*\s[A-Za-z-']*$").ToString();
                    key = $"{matchName} {Plugin.ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                } else {
                    key = $"{senderPayload.PlayerName} {senderPayload.World.Name}";
                }
                if(mapPayload != null && Plugin.CurrentPartyList.ContainsKey(key) && (Plugin.CurrentPartyList[key].MapLink == null || !Plugin.Configuration.NoOverwriteMapLink)) {
                    //CurrentPartyList[key].MapLink = SeString.CreateMapLink(mapPayload.TerritoryType.RowId, mapPayload.Map.RowId, mapPayload.XCoord, mapPayload.YCoord);
                    Plugin.CurrentPartyList[key].MapLink = new MPAMapLink(mapPayload);
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
            //player.Maps.Add(newMap);
            player.MapLink = null;
            LastMapPlayerKey = player.Key;

            //add to DB
            Plugin.StorageManager.AddMap(newMap);
            Plugin.StorageManager.UpdatePlayer(player);

            //this can cause race conditions with the config file since we're saving in the other thread
            //Plugin.Save();

            ClearStatus();
        }

        public void RemoveLastMap(MPAMember player) {
            var lastMap = player.Maps.Where(m => !m.IsDeleted && !m.IsArchived).LastOrDefault();
            if(lastMap != null) {
                lastMap.IsDeleted = true;

                Plugin.Save();

                var lastMapStorage = Plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted && !m.IsArchived).ToList().LastOrDefault();
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
            var maps = Plugin.StorageManager.GetMaps().Query().ToList();
            maps.ForEach(m => m.IsArchived = true);
            Plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                Plugin.BuildRecentPartyList();
            });
            ClearStatus();
        }

        public void ArchiveMaps(IEnumerable<MPAMap> maps) {
            PluginLog.Information("Archiving maps...");
            maps.ToList().ForEach(m => m.IsArchived = true);
            Plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                Plugin.BuildRecentPartyList();
            });
            //Plugin.Save();
        }

        public void DeleteMaps(IEnumerable<MPAMap> maps) {
            PluginLog.Information("Deleting maps...");
            maps.ToList().ForEach(m => m.IsDeleted = true);
            Plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                Plugin.BuildRecentPartyList();
            });
            //Plugin.Save();
        }

        public void CheckAndArchiveMaps(Dictionary<string, MPAMember> list) {
            DateTime currentTime = DateTime.Now;
            var storageMaps = Plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived).ToList();
            foreach(var map in storageMaps) {
                TimeSpan timeSpan = currentTime - map.Time;
                map.IsArchived = timeSpan.TotalHours > Plugin.Configuration.ArchiveThresholdHours;
            }
            Plugin.StorageManager.UpdateMaps(storageMaps).ContinueWith(t => {
                Plugin.BuildRecentPartyList();
            });

            Plugin.Save();
        }

        public void ClearMapLink(MPAMember player) {
            player.MapLink = null;
            Plugin.StorageManager.UpdatePlayer(player);
            Plugin.Save();
        }

        public double? GetDistanceToMapLink(MPAMapLink mapLink) {
            if(Plugin.ClientState.LocalPlayer == null || Plugin.GetCurrentTerritoryId() != mapLink.TerritoryTypeId) {
                return null;
            }

            Vector2 playerPosition = WorldPosToMapCoords(Plugin.ClientState.LocalPlayer.Position);
            float xDistance = playerPosition.X - mapLink.RawX;
            float yDistance = playerPosition.Y - mapLink.RawY;

            double totalDistance = Math.Sqrt(Math.Pow(xDistance, 2) + Math.Pow(yDistance, 2));
            return totalDistance;
        }

        //credit to Pohky
        private static Vector2 WorldPosToMapCoords(Vector3 pos) {
            var xInt = (int)(MathF.Round(pos.X, 3, MidpointRounding.AwayFromZero) * 1000);
            var yInt = (int)(MathF.Round(pos.Z, 3, MidpointRounding.AwayFromZero) * 1000);
            return new Vector2((int)(xInt * 0.001f * 1000f), (int)(yInt * 0.001f * 1000f));
        }


        public MPAMember? GetPlayerWithClosestMapLink(List<MPAMember> players) {
            MPAMember? closestLinkPlayer = null;
            double? closestDistance = Double.MaxValue;

            foreach(var player in players) {
                if(player.MapLink == null) continue;
                double? distance = GetDistanceToMapLink(player.MapLink);
                if(distance == null) continue;
                if(closestLinkPlayer == null) {
                    closestLinkPlayer = player;
                    closestDistance = distance;
                    continue;
                } else if(distance < closestDistance) {
                    closestLinkPlayer = player;
                    closestDistance = distance;
                }
            }
            return closestLinkPlayer;
        }

        public MPAMap? FindMapForDutyResults(DutyResults results) {
            MPAMap? topCandidateMap = null;
            //foreach(var player in Plugin.Configuration.RecentPartyList) {
            //    foreach(var map in player.Value.Maps) {
            //        topCandidateMap ??= map;
            //        TimeSpan currentMapSpan = results.Time - map.Time;
            //        TimeSpan topCandidateMapSpan = results.Time - topCandidateMap.Time;
            //        //same duty and must happen afterwards, but not more than 20 mins as a fallback
            //        bool sameDuty = !map.DutyName.IsNullOrEmpty() && map.DutyName.Equals(results.DutyName, StringComparison.OrdinalIgnoreCase);
            //        bool validTime = currentMapSpan.TotalMilliseconds > 0 && currentMapSpan.TotalMinutes < 20;
            //        bool closerTime = currentMapSpan < topCandidateMapSpan;
            //        if(sameDuty && validTime && closerTime) {
            //            topCandidateMap = map;
            //            //clear top candidate if a closer time is found but with the wrong duty
            //        } else if(!sameDuty && validTime && closerTime) {
            //            topCandidateMap = null;
            //            //clear invalid top candidates
            //        } else if(map == topCandidateMap && (!sameDuty || !validTime)) {
            //            topCandidateMap = null;
            //        }
            //    }
            //}

            var maps = Plugin.StorageManager.GetMaps().Query().ToList();
            foreach(var map in maps) {
                topCandidateMap ??= map;
                TimeSpan currentMapSpan = results.Time - map.Time;
                TimeSpan topCandidateMapSpan = results.Time - topCandidateMap.Time;
                //same duty and must happen afterwards, but not more than 20 mins as a fallback
                bool sameDuty = !map.DutyName.IsNullOrEmpty() && map.DutyName.Equals(results.DutyName, StringComparison.OrdinalIgnoreCase);
                bool validTime = currentMapSpan.TotalMilliseconds > 0 && currentMapSpan.TotalMinutes < 20;
                bool closerTime = currentMapSpan < topCandidateMapSpan;
                if(sameDuty && validTime && closerTime) {
                    topCandidateMap = map;
                    //clear top candidate if a closer time is found but with the wrong duty
                } else if(!sameDuty && validTime && closerTime) {
                    topCandidateMap = null;
                    //clear invalid top candidates
                } else if(map == topCandidateMap && (!sameDuty || !validTime)) {
                    topCandidateMap = null;
                }
            }
            return topCandidateMap;
        }

        private void ResetDigStatus() {
            _diggerKey = "";
            _extraDigCount = 0;
            _selfDig = false;
            //_diggers = new();
            _isDigLockedIn = false;
            _portalBlockUntil = DateTime.UnixEpoch;
            _digTime = DateTime.UnixEpoch;
        }

        private void SetStatus(string message, StatusLevel level) {
            StatusMessage = message;
            Status = level;
        }

        private void ClearStatus() {
            SetStatus("", StatusLevel.OK);
        }
    }
}
