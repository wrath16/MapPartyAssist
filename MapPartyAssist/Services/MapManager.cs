using Dalamud;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    //internal service for managing treasure maps and map links
    internal class MapManager : IDisposable {
        public string LastMapPlayerKey { get; private set; } = "";
        internal MPAMap? LastMap { get; private set; }
        public string StatusMessage { get; set; } = "";
        public StatusLevel Status { get; set; } = StatusLevel.OK;

        //upper limit between dig time and treasure coffer message to consider it eligible as ownership
        private readonly int _digThresholdMS = 6000;
        //ideal time between dig and "you discover a treasure coffer"
        private readonly int _digTargetMS = 600;
        //timer to block portal from adding a duplicate map after finishing a chest
        private readonly int _portalBlockSeconds = 60;
        //delay added onto adding a map to avoid double-counting self maps with another player using dig at same time
        private readonly int _addMapDelaySeconds = 2;
        //window within last map was added to a player to suppress warning messages
        private readonly int _lastMapAddedThresholdMS = 10000;
        //for setting map name
        private readonly TextInfo _textInfo = new CultureInfo("en-US", false).TextInfo;

        private Plugin _plugin;

        private Dictionary<string, DateTime> _diggers = new();
        private string _lockedInDiggerKey = "";
        private int _candidateCount;
        private DateTime _lastMapTime = DateTime.UnixEpoch;
        private DateTime _portalBlockUntil = DateTime.UnixEpoch;
        private bool _boundByMapDuty;

        //LogMessage: 3778
        private static readonly Dictionary<ClientLanguage, Regex> EnterPortalRegex = new() {
            { ClientLanguage.English, new Regex(@"complete[s]? preparations to enter the portal.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(a|avez)? enregistré l'équipe afin de pénétrer dans une cache au trésor\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(hat|hast)? die Gruppe angemeldet, um einen geheimen Hort zu betreten\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"が、宝物庫へ突入申請しました。$", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 3766 or 4405
        private static readonly Dictionary<ClientLanguage, Regex> ConsumedMapRegex = new() {
            { ClientLanguage.English, new Regex(@"map crumbles into dust.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"carte .* tombe en poussière\.\.\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"karte zerfällt zu Staub\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"地図.*」は消失した……$", RegexOptions.IgnoreCase) }
        };

        private static readonly Dictionary<ClientLanguage, Regex> MapNameRegex = new() {
            { ClientLanguage.English, new Regex(@"\w* [\w']* map(?=\scrumbles into dust)", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(?<=(La |Le |L'))carte .*(?=\stombe en poussière)", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=(Der |Die |Das )).*(-)?(schatz)?karte(?=\szerfällt zu Staub)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"(?<=「).*地図.*(?=」は消失した……)", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 3759
        private static readonly Dictionary<ClientLanguage, Regex> DiscoverCofferRegex = new() {
            { ClientLanguage.English, new Regex(@"discover a treasure coffer!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez découvert un coffre au trésor!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Du hast eine versteckte Schatztruhe gefunden!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"隠された宝箱を発見した！$", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 3756, 9361, 9363
        private static readonly Dictionary<ClientLanguage, Regex> OpenCofferRegex = new() {
            { ClientLanguage.English, new Regex(@"releasing a powerful musk into the air!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"libérant un musc très fort", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(Eine Falle! Das wird nicht ohne Blutvergießen vonstatten gehen\.|Eine Falle wurde ausgelöst, die einen starken Lockduft versprüht!)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"魔物を誘引する臭いが立ちこめた！$", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 3765
        private static readonly Dictionary<ClientLanguage, Regex> DefeatAllRegex = new() {
            { ClientLanguage.English, new Regex(@"defeat all the enemies drawn by the trap!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez vaincu tous les monstres attirés par le piège\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Du hast alle Gegner besiegt, die an der Falle gelauert hatten!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"宝箱の罠で誘引されたすべての魔物を倒した！$", RegexOptions.IgnoreCase) }
        };

        private static readonly Dictionary<ClientLanguage, Regex> PartyMemberDigRegex = new() {
            { ClientLanguage.English, new Regex(@"uses Dig\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"utilise Excavation\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"setzt Ausgraben ein\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"の「ディグ」$", RegexOptions.IgnoreCase) }
        };

        //Japanese uses same for party member
        private static readonly Dictionary<ClientLanguage, Regex> SelfDigRegex = new() {
            { ClientLanguage.English, new Regex(@"^You use Dig\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"^Vous utilisez Excavation\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"^Du setzt Ausgraben ein\.$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"", RegexOptions.IgnoreCase) }
        };

        //Addon: 2276, 8107
        private static readonly Dictionary<ClientLanguage, Regex> TreasureHuntRegex = new() {
            { ClientLanguage.English, new Regex(@"^Treasure Hunt$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"^Chasse aux trésors$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"^Schatz-suche$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"^トレジャーハント$", RegexOptions.IgnoreCase) }
        };

        public MapManager(Plugin plugin) {
            _plugin = plugin;
            _plugin.ChatGui.ChatMessage += OnChatMessage;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            _plugin.AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "_ToDoList", CheckForTreasureHunt);


            ResetDigStatus();
        }

        public void Dispose() {
#if DEBUG
            _plugin.Log.Debug("disposing map manager");
#endif
            _plugin.ChatGui.ChatMessage -= OnChatMessage;
            _plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            _plugin.AddonLifecycle.UnregisterListener(CheckForTreasureHunt);
        }

        private void OnTerritoryChanged(ushort territoryId) {
            ResetDigStatus();
        }

        private DateTime _lastChecked;
        private unsafe void CheckForTreasureHunt(AddonEvent type, AddonArgs args) {
            //_plugin.Log.Debug("pre refresh todolist!");
            var addon = (AtkUnitBase*)args.Addon;
            if(addon == null) {
                return;
            }
            var dutyTimerNode = AtkNodeHelper.GetNodeByIDChain(addon, new uint[] { 1, 4, 5 });
            var dutyNameNode = AtkNodeHelper.GetNodeByIDChain(addon, new uint[] { 1, 4, 3 });
            var baseNode = addon->GetNodeById(4);
            if(dutyNameNode == null || baseNode == null) {
                return;
            }
            var dutyName = dutyNameNode->GetAsAtkTextNode()->NodeText.ToString();
            //var rowId = _plugin.GetRowId<Addon>(dutyNameNode->GetAsAtkTextNode()->NodeText.ToString(), "Text");

            if(baseNode->IsVisible && TreasureHuntRegex[_plugin.ClientState.ClientLanguage].IsMatch(dutyName)) {
                if(!_boundByMapDuty) {
                    _plugin.Log.Verbose($"Bound by map duty!");
                }
                _boundByMapDuty = true;
            } else if(_boundByMapDuty) {
                _plugin.Log.Verbose($"No longer bound by map duty!");
                _boundByMapDuty = false;
            }

            if((DateTime.Now - _lastChecked).TotalSeconds > 10) {
                _lastChecked = DateTime.Now;
                //_plugin.Log.Debug($"visible: {baseNode->IsVisible} dutyName: {dutyNameNode->GetAsAtkTextNode()->NodeText.ToString()} rowId: {rowId}");
            }
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            bool isChange = false;
            bool newMapFound = false;
            bool isPortal = false;
            string key = "";
            string mapType = "";
            DateTime messageTime = DateTime.Now;

            //refuse to process if not a supported language
            //if(!Plugin.IsEnglishClient()) {
            //    return;
            //}

            if((int)type == 2361) {
                //party member opens portal while not blocked
                if(EnterPortalRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    if(_portalBlockUntil <= messageTime) {
                        //thief's maps
                        var playerPayload = (PlayerPayload)message.Payloads.First(p => p.Type == PayloadType.Player);
                        key = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                        newMapFound = true;
                        isPortal = true;
                    } else {
                        //TODO compare to last map to verify ownership
                    }
                }
            } else if((int)type == 2105 || (int)type == 2233) {
                //self map detection
                if(ConsumedMapRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    newMapFound = true;
                    mapType = MapNameRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString()).ToString();
                    if(!mapType.IsNullOrEmpty()) {
                        try {
                            mapType = _plugin.TranslateDataTableEntry<EventItem>(mapType, "Singular", ClientLanguage.English);
                        } catch {
                            mapType = "";
                        }
                    }
                    key = _plugin.GetCurrentPlayer();
                    //_lastMapTime = messageTime;
                    //clear dig info just in case to prevent double-counting map if another player uses dig at the same time
                    ResetDigStatus();
                } else if(DiscoverCofferRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    //find (non-current PC) party member with the closest matching dig time and assume they are owner
                    _lockedInDiggerKey = GetLikelyMapOwner(messageTime, _plugin.GetCurrentPlayer());
                    if(_lockedInDiggerKey.IsNullOrEmpty() && !IsPlayerCandidateOwner(messageTime, _plugin.GetCurrentPlayer())) {
                        _plugin.Log.Warning($"No eligible map owner detected for discovered coffer!");
                        SetStatus("Unable to determine map owner, verify and add manually.", StatusLevel.ERROR);
                    }
                    //LogMessage: 3756, 9361, 9363
                } else if(OpenCofferRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    //add delay because this message occurs before "crumbles into dust" to avoid double-counting with self-dig
                    Task.Delay(_addMapDelaySeconds * 1000).ContinueWith(t => {
                        if(!_lockedInDiggerKey.IsNullOrEmpty()) {
                            AddMap(_plugin.CurrentPartyList[_lockedInDiggerKey]);
                            if(_candidateCount > 1) {
                                _plugin.Log.Warning($"Multiple map owner candidates detected!");
                                SetStatus("Multiple map owner candidates found, verify last map ownership.", StatusLevel.CAUTION);
                            }
                        } else if((messageTime - _lastMapTime).TotalMilliseconds > _lastMapAddedThresholdMS) {
                            //need this in case player used a false dig out of range
                            _plugin.Log.Warning($"No eligible map owner detected on opened coffer!");
                            SetStatus("Unable to determine map owner, verify and add manually.", StatusLevel.ERROR);
                        }
                        //have to reset here in case you fail to defeat the treasure chest enemies -_-
                        ResetDigStatus();
                    });
                    //LogMessage: 3765
                } else if(DefeatAllRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    ResetDigStatus();
                    //block portals from adding maps for a brief period to avoid double counting
                    //this can cause issues where someone opens a thief map immediately after, but whatever
                    _portalBlockUntil = DateTime.Now.AddSeconds(_portalBlockSeconds);
                }
            } else if((int)type == 4139) {
                //party member uses dig
                if(PartyMemberDigRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    var playerPayload = (PlayerPayload?)message.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player);
                    //no payload on Japanese self-dig or maybe other from same world...?
                    var diggerKey = playerPayload != null ? $"{playerPayload.PlayerName} {playerPayload.World.Name}" : $"{_plugin.GetCurrentPlayer()}";
                    if(_diggers.ContainsKey(diggerKey)) {
                        _diggers[diggerKey] = messageTime;
                    } else {
                        _diggers.Add(diggerKey, messageTime);
                    }
                }
            } else if((int)type == 2091) {
                //need this to prevent warnings on own maps
                if(SelfDigRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.ToString())) {
                    if(_diggers.ContainsKey(_plugin.GetCurrentPlayer())) {
                        _diggers[_plugin.GetCurrentPlayer()] = messageTime;
                    } else {
                        _diggers.Add(_plugin.GetCurrentPlayer(), messageTime);
                    }
                }
            } else if(type == XivChatType.Party || type == XivChatType.Say || type == XivChatType.Alliance) {
                //getting map links
                var mapPayload = (MapLinkPayload?)message.Payloads.FirstOrDefault(p => p.Type == PayloadType.MapLink);
                var senderPayload = (PlayerPayload?)sender.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player);

                if(senderPayload == null) {
                    //from same world as player
                    string matchName = Regex.Match(sender.TextValue, @"[A-Za-z-']*\s[A-Za-z-']*$").ToString();
                    key = $"{matchName} {_plugin.ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                } else {
                    key = $"{senderPayload.PlayerName} {senderPayload.World.Name}";
                }
                if(mapPayload != null && _plugin.CurrentPartyList.ContainsKey(key) && (_plugin.CurrentPartyList[key].MapLink == null || !_plugin.Configuration.NoOverwriteMapLink)) {
                    _plugin.CurrentPartyList[key].MapLink = new MPAMapLink(mapPayload);
                    _plugin.StorageManager.UpdatePlayer(_plugin.CurrentPartyList[key]);
                    _plugin.Save();
                }
            } else if(_boundByMapDuty) {
                //gil
                if((int)type == 62) {
                    Match m = DutyManager.GilObtainedRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString());
                    if(m.Success) {
                        string parsedGilString = m.Value.Replace(",", "").Replace(".", "").Replace(" ", "");
                        int gil = int.Parse(parsedGilString);
                        AddLootResults(1, false, gil, _plugin.GetCurrentPlayer());
                        isChange = true;
                    }
                    //self loot obtained
                } else if((int)type == 2110) {
                    Match quantityMatch = DutyManager.SelfObtainedQuantityRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString());
                    if(quantityMatch.Success) {
                        //todo make this work for all languages...
                        bool isNumber = Regex.IsMatch(quantityMatch.Value, @"\d+");
                        int quantity = isNumber ? int.Parse(quantityMatch.Value.Replace(",", "").Replace(".", "")) : 1;
                        var player = _plugin.GetCurrentPlayer();
                        var item = (ItemPayload?)message.Payloads.FirstOrDefault(m => m is ItemPayload);
                        if(item is not null) {
                            AddLootResults(item.ItemId, item.IsHQ, quantity, player);
                            isChange = true;
                            _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5} recipient: {3}", item.ItemId, item.IsHQ, quantity, player));
                        } else {
                            //tomestones...
                            Match itemMatch = DutyManager.SelfObtainedItemRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString());
                            if(itemMatch.Success) {
                                var rowId = quantity != 1 ? _plugin.GetRowId<Item>(itemMatch.Value, "Plural") : _plugin.GetRowId<Item>(itemMatch.Value, "Singular");
                                if(rowId is not null) {
                                    AddLootResults((uint)rowId, false, quantity, player);
                                    isChange = true;
                                } else {
                                    _plugin.Log.Warning($"Cannot find rowId for {itemMatch.Value}");
                                }
                            }
                        }
                    }
                    //party member loot obtained
                } else if((int)type == 8254) {
                    Match m = DutyManager.PartyMemberObtainedRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString());
                    if(m.Success) {
                        //todo make this work for all languages...
                        bool isNumber = Regex.IsMatch(m.Value, @"\d+");
                        int quantity = isNumber ? int.Parse(m.Value.Replace(",", "").Replace(".", "")) : 1;
                        var player = (PlayerPayload?)message.Payloads.FirstOrDefault(m => m is PlayerPayload);
                        var playerKey = $"{player.PlayerName} {player.World.Name}";
                        var item = (ItemPayload?)message.Payloads.FirstOrDefault(m => m is ItemPayload);
                        if(item is not null) {
                            AddLootResults(item.ItemId, item.IsHQ, quantity, playerKey);
                            isChange = true;
                            _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5} recipient: {3}", item.ItemId, item.IsHQ, quantity, player));
                        }
                    }

                    //check for loot list
                } else if(type == XivChatType.SystemMessage) {
                    Match m = DutyManager.LootListRegex[_plugin.ClientState.ClientLanguage].Match(message.ToString());
                    if(m.Success) {
                        //todo make this work for all languages...
                        bool isNumber = Regex.IsMatch(m.Value, @"\d+");
                        int quantity = isNumber ? int.Parse(m.Value) : 1;
                        var item = (ItemPayload?)message.Payloads.FirstOrDefault(m => m is ItemPayload);
                        AddLootResults(item.ItemId, item.IsHQ, quantity);
                        isChange = true;
                        _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5}", item.ItemId, item.IsHQ, quantity));
                    }
                }
                if(isChange && LastMap != null) {
                    //very first map...?
                    _plugin.StorageManager.UpdateMap(LastMap);
                }
            }

            if(newMapFound && _plugin.CurrentPartyList.Count > 0) {
                AddMap(_plugin.CurrentPartyList[key], null, mapType, false, isPortal);
            }
        }

        public void AddMap(MPAMember player, string? zone = null, string? mapType = null, bool isManual = false, bool isPortal = false) {
            _plugin.Log.Information(string.Format("Adding new{0} map for {1}", isManual ? " manual" : "", player.Key));
            DateTime currentTime = DateTime.Now;

            if(_plugin.IsLanguageSupported()) {
                //have to do lookup on PlaceName sheet otherwise will not translate properly
                var placeNameId = _plugin.DataManager.GetExcelSheet<TerritoryType>(ClientLanguage.English)?.GetRow(_plugin.ClientState.TerritoryType)?.PlaceName.Row;
                zone ??= placeNameId != null ? _plugin.DataManager.GetExcelSheet<PlaceName>(ClientLanguage.English)!.GetRow((uint)placeNameId)!.Name : "";
            } else {
                zone ??= "";
            }

            mapType ??= "";
            mapType = _textInfo.ToTitleCase(mapType);

            MPAMap newMap = new() {
                Name = mapType,
                Time = currentTime,
                Owner = player.Key,
                Zone = zone,
                IsManual = isManual,
                IsPortal = isPortal,
                LootResults = new(),
                Players = _plugin.CurrentPartyList.Keys.ToArray(),
            };
            player.MapLink = null;
            LastMapPlayerKey = player.Key;
            _lastMapTime = currentTime;

            //add to DB
            _plugin.StorageManager.AddMap(newMap);
            _plugin.StorageManager.UpdatePlayer(player);
            LastMap = newMap;
            //Plugin.Save();

            ClearStatus();
        }

        public void ClearAllMaps() {
            _plugin.Log.Information("Archiving all maps...");
            var maps = _plugin.StorageManager.GetMaps().Query().ToList();
            maps.ForEach(m => m.IsArchived = true);
            _plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                _plugin.BuildRecentPartyList();
            });
            ClearStatus();
        }

        public void ArchiveMaps(IEnumerable<MPAMap> maps) {
            _plugin.Log.Information("Archiving maps...");
            maps.ToList().ForEach(m => m.IsArchived = true);
            _plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                _plugin.BuildRecentPartyList();
            });
            //Plugin.Save();
        }

        public void DeleteMaps(IEnumerable<MPAMap> maps) {
            _plugin.Log.Information("Deleting maps...");
            maps.ToList().ForEach(m => m.IsDeleted = true);
            _plugin.StorageManager.UpdateMaps(maps).ContinueWith(t => {
                _plugin.BuildRecentPartyList();
            });
            //Plugin.Save();
        }

        public void CheckAndArchiveMaps() {
            _plugin.Log.Information("Checking and archiving old maps...");
            DateTime currentTime = DateTime.Now;
            var storageMaps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived).ToList();
            foreach(var map in storageMaps) {
                TimeSpan timeSpan = currentTime - map.Time;
                map.IsArchived = timeSpan.TotalHours > _plugin.Configuration.ArchiveThresholdHours;
            }
            _plugin.StorageManager.UpdateMaps(storageMaps).ContinueWith(t => {
                _plugin.BuildRecentPartyList();
            });
            _plugin.Save();
        }

        public void ClearMapLink(MPAMember player) {
            player.MapLink = null;
            _plugin.StorageManager.UpdatePlayer(player);
            _plugin.Save();
        }

        //returns map coords
        public double? GetDistanceToMapLink(MPAMapLink mapLink) {
            if(_plugin.ClientState.LocalPlayer == null || _plugin.GetCurrentTerritoryId() != mapLink.TerritoryTypeId) {
                return null;
            }

            Vector2 playerPosition = WorldPosToMapCoords(_plugin.ClientState.LocalPlayer.Position);
            float xDistance = playerPosition.X - mapLink.RawX;
            float yDistance = playerPosition.Y - mapLink.RawY;

            double totalDistance = Math.Sqrt(Math.Pow(xDistance, 2) + Math.Pow(yDistance, 2));
            return totalDistance;
        }

        //credit to Pohky on discord
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
            var maps = _plugin.StorageManager.GetMaps().Query().ToList();
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
            _diggers = new();
            _lockedInDiggerKey = "";
            _candidateCount = 0;
            _portalBlockUntil = DateTime.UnixEpoch;
        }

        //closest dig to 650 ms
        //ignores player
        private string GetLikelyMapOwner(DateTime cofferTime, params string[] ignorePlayers) {
            string closestKey = "";
            double closestTimeMS = _digThresholdMS;
            foreach(var digger in _diggers) {
                bool foundIgnore = false;
                foreach(var ignorePlayer in ignorePlayers) {
                    if(ignorePlayer.Equals(digger.Key)) {
                        foundIgnore = true;
                        break;
                    }
                }
                if(foundIgnore) {
                    continue;
                }
                double timeDiffMS = (cofferTime - digger.Value).TotalMilliseconds;
                double diffFromIdealMS = Math.Abs(timeDiffMS - _digTargetMS);
#if DEBUG
                _plugin.Log.Debug($"digger: {digger.Key} timediffMS: {timeDiffMS} diffFromIdeal: {diffFromIdealMS}");
#endif
                if(timeDiffMS < _digThresholdMS) {
                    _candidateCount++;
                    if(diffFromIdealMS < closestTimeMS) {
                        closestKey = digger.Key;
                        closestTimeMS = diffFromIdealMS;
                    }
                }
            }
            return closestKey;
        }

        private bool IsPlayerCandidateOwner(DateTime cofferTime, string playerKey) {
            if(!_diggers.ContainsKey(playerKey)) {
                return false;
            }

            return (cofferTime - _diggers[playerKey]).TotalMilliseconds < _digThresholdMS;
        }

        private void SetStatus(string message, StatusLevel level) {
            StatusMessage = message;
            Status = level;
        }

        private void ClearStatus() {
            SetStatus("", StatusLevel.OK);
        }

        private void AddLootResults(uint itemId, bool isHQ, int quantity, string? recipient = null) {
            //this is bad
            if(LastMap is null) {
                var lastMap = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted).OrderBy(m => m.Time).ToList().Last();
                if(lastMap is null) {
                    return;
                }
                LastMap = lastMap;
            }

            if(LastMap.LootResults is null) {
                throw new InvalidOperationException("Unable to add loot result to map!");
                //20 minute fallback
            } else if((DateTime.Now - LastMap.Time).TotalMinutes > 20) {
                //throw new InvalidOperationException("");
                _plugin.Log.Warning("Last map time exceeded loot threshold window.");
                return;
            }

            var matchingLootResults = LastMap.GetMatchingLootResult(itemId, isHQ, quantity);
            if(matchingLootResults is null) {
                LootResult lootResult = new() {
                    Time = DateTime.Now,
                    ItemId = itemId,
                    IsHQ = isHQ,
                    Quantity = quantity,
                    Recipient = recipient,
                };
                LastMap.LootResults.Add(lootResult);
            } else {
                matchingLootResults.Recipient = recipient;
            }
        }
    }
}
