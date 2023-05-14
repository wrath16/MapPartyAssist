using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using MapPartyAssist.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapPartyAssist {
    public sealed class Plugin : IDalamudPlugin {
        public string Name => "Map Party Assist";
        private const string CommandName = "/mparty";

        //Dalamud services
        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        private DataManager DataManager { get; init; }
        private ClientState ClientState { get; init; }

        private DutyState DutyState { get; init; }
        private PartyList PartyList { get; init; }
        private ChatGui ChatGui { get; init; }
        private GameGui GameGui { get; init; }
        private Framework Framework { get; init; }

        public Configuration Configuration { get; init; }
        internal GameFunctions Functions { get; }

        public WindowSystem WindowSystem = new("Map Party Assist");


        private Dictionary<uint, World> _worlds = new();
        //private string _playerName;
        //private string _playerWorld;
        private int _lastPartySize;
        //public string _lastMapPlayerKey = "";

        private string _diggerKey = "";
        private DateTime _digTime = DateTime.UnixEpoch;
        private bool _isDigLockedIn = false;
        private DateTime _portalBlockUntil = DateTime.UnixEpoch;

        private static int _digThresholdSeconds = 8; //window within which to block subsequent dig while awaiting treasure coffer message
        private static int _digFallbackSeconds = 60 * 10; //if no treasure coffer opened after this time of dig locking in, unlock and allow digging
        private static int _portalBlockSeconds = 60; //timer to block portal from adding a duplicate map after finishing a chest
        private static int _addMapDelaySeconds = 5; //delay added onto adding a map to avoid double-counting self maps with another player using dig at same time

        public Dictionary<string, MPAMember> CurrentPartyList { get; set; }
        public Dictionary<string, MPAMember> RecentPartyList { get; set; }
        public string LastMapPlayerKey { get; private set; } = "";
        //    get {
        //        Dictionary<string, MPAMember> newList = new();
        //        foreach(var player in this.Configuration.RecentPartyList) {
        //            TimeSpan timeSpan = DateTime.Now - player.Value.LastJoined;
        //            var isRecent = timeSpan.TotalHours <= Configuration.ArchiveThresholdHours;
        //            var hasMaps = false;
        //            foreach(var map in player.Value.Maps) {
        //                if(!map.IsArchived && !map.IsDeleted) {
        //                    hasMaps = true;
        //                    break;
        //                }
        //            }
        //            var notCurrent = !CurrentPartyList.ContainsKey(player.Key);
        //            var notSelf = !player.Value.IsSelf;
        //            if(isRecent && hasMaps && notCurrent && notSelf) {
        //                newList.Add(player.Key, player.Value);
        //            }
        //        }
        //        return newList;
        //    }
        //}

        //TODO delete (for testing only!)
        public Dictionary<string, MPAMember> FakePartyList { get; set; }
        //public Dictionary<string, MPAMember> FakePartyList {
        //    get {
        //        return Configuration.RecentPartyList.Where(p => p.Value.Maps.Count > 0 || p.Value.IsSelf).Take(8).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        //    }
        //    set {
        //        FakePartyList = value;
        //    }
        //}

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] DataManager dataManager,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] DutyState dutyState,
            [RequiredVersion("1.0")] PartyList partyList,
            [RequiredVersion("1.0")] ChatGui chatGui,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] Framework framework) {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.DataManager = dataManager;
            this.ClientState = clientState;
            this.DutyState = dutyState;
            this.PartyList = partyList;
            this.ChatGui = chatGui;
            this.GameGui = gameGui;
            this.Framework = framework;

            this.Functions = new GameFunctions();

            PluginLog.Log("Begin Config loading");
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            PluginLog.Log("Done Config loading");

            WindowSystem.AddWindow(new MainWindow(this));

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                HelpMessage = "Opens map party assist."
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            this.Framework.Update += this.OnFrameworkUpdate;
            this.ChatGui.ChatMessage += this.OnChatMessage;
            this.ClientState.Login += this.OnLogin;
            this.ClientState.Logout += this.OnLogout;
            this.ClientState.TerritoryChanged += this.OnTerritoryChanged;
            this.DutyState.DutyStarted += this.OnDutyStart;

            foreach(var world in this.DataManager.GetExcelSheet<World>()!) {
                this._worlds[world.RowId] = world;
            }

            //build current and recent party lists
            if(ClientState.IsLoggedIn) {
                BuildCurrentPartyList();
                BuildRecentPartyList();
            } else {
                CurrentPartyList = new();
                RecentPartyList = new();
            }
            ResetDigStatus();

            //setup fake party list!
            FakePartyList = new();
            FakePartyList.Add("Test Party1 Siren", new MPAMember("Test Party1", "Siren"));
            FakePartyList.Add("Sarah Montcroix Siren", new MPAMember("Sarah Montcroix", "Siren", true));
            FakePartyList.Add("Test Party2 Gilgamesh", new MPAMember("Test Party2", "Gilgamesh"));
            FakePartyList.Add("KillerDog Matmaster Coeurl", new MPAMember("Killerdog Matmaster", "Coeurl"));
            FakePartyList.Add("Test Party4 Siren", new MPAMember("Test Party4", "Siren"));
            FakePartyList.Add("Test Party5 Cactuar", new MPAMember("Test Party5", "Cactuar"));
            FakePartyList.Add("Test Party6 Lamia", new MPAMember("Test Party6", "Lamia"));
            FakePartyList.Add("Test Party7 Balmung", new MPAMember("Test Party7", "Balmung"));
            //FakePartyList["Test Party1 Siren"].MapLink = new MapLinkPayload(23, 23, 22f, 22f);
            var mapLinkPayload = SeString.CreateMapLink("Upper La Noscea", 22f, 24f).Payloads.ElementAt(0) as MapLinkPayload;
            FakePartyList["Test Party1 Siren"].MapLink = mapLinkPayload;
            FakePartyList["Test Party1 Siren"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 12, 8, 30, 11), "The Ruby Sea"));
            FakePartyList["Test Party1 Siren"].Maps.Add(new MPAMap("", new DateTime(2023, 4, 12, 9, 30, 11)));
            FakePartyList["Test Party1 Siren"].Maps.Add(new MPAMap("", new DateTime(2023, 4, 12, 10, 30, 11)));
            FakePartyList["Test Party1 Siren"].Maps.Add(new MPAMap("", new DateTime(2023, 4, 12, 11, 30, 11)));
            FakePartyList["Sarah Montcroix Siren"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 1, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 2, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 3, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 4, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 5, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", DateTime.Now));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 7, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 8, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 9, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 10, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 11, 11, 30, 11)));
            FakePartyList["Test Party2 Gilgamesh"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 4, 12, 11, 30, 11)));
            FakePartyList["Test Party6 Lamia"].Maps.Add(new MPAMap("unknown", new DateTime(2023, 3, 12, 13, 30, 11)));

        }

        public void Dispose() {
            PluginLog.Debug("disposing...");

            this.WindowSystem.RemoveAllWindows();
            this.CommandManager.RemoveHandler(CommandName);

            this.Framework.Update -= this.OnFrameworkUpdate;
            this.ChatGui.ChatMessage -= this.OnChatMessage;

            this.ClientState.Login -= this.OnLogin;
            this.ClientState.Logout -= this.OnLogout;
            this.ClientState.TerritoryChanged -= this.OnTerritoryChanged;

            this.DutyState.DutyStarted -= this.OnDutyStart;

            Configuration.PruneRecentPartyList();

            //this.PartyList.Length.
        }

        private void OnCommand(string command, string args) {
            // in response to the slash command, just display our main ui
            WindowSystem.GetWindow("Map Party Assist").IsOpen = true;
        }

        private void DrawUI() {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI() {
            WindowSystem.GetWindow("Map Party Assist").IsOpen = true;
        }

        private void OnFrameworkUpdate(Framework framework) {
            var playerJob = ClientState.LocalPlayer?.ClassJob.GameData?.Abbreviation;
            var currentTerritory = ClientState.TerritoryType;

            var currentPartySize = PartyList.Length;

            if(playerJob != null && currentPartySize != _lastPartySize) {
                PluginLog.Debug($"Party size has changed.");
                BuildCurrentPartyList();
                BuildRecentPartyList();
                //this.Configuration.Save();
                _lastPartySize = currentPartySize;
            }
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            ResetDigStatus();
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {

            //PluginLog.Debug($"Message received: {type} {message} from {sender}");
            //foreach(Payload payload in message.Payloads) {
            //    PluginLog.Debug($"payload: {payload}");
            //}

            bool newMapFound = false;
            string key = "";
            string mapType = "";

            if((int)type == 2361) {
                //party member opens portal, after block time
                if(_portalBlockUntil < DateTime.Now && Regex.IsMatch(message.ToString(), @"complete[s]? preparations to enter the portal.$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    var playerPayload = (PlayerPayload)message.Payloads.First(p => p.Type == PayloadType.Player);
                    key = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                    newMapFound = true;
                    PluginLog.Debug($"_diggerKey: {_diggerKey} _digTime: {_digTime}  _isDigLockedIn: {_isDigLockedIn} _portalBlockUntil: {_portalBlockUntil} ");
                }
                //self map detection
            } else if((int)type == 2105 || (int)type == 2233) {
                if(Regex.IsMatch(message.ToString(), @"map crumbles into dust.$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    newMapFound = true;
                    mapType = Regex.Match(message.ToString(), @"\w* [\w']* map(?=\scrumbles into dust)").ToString();
                    key = $"{ClientState.LocalPlayer!.Name} {ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                    //clear dig info just in case to prevent double-adding map if another player uses dig at the same time
                    ResetDigStatus();
                    PluginLog.Debug($"_diggerKey: {_diggerKey} _digTime: {_digTime}  _isDigLockedIn: {_isDigLockedIn} _portalBlockUntil: {_portalBlockUntil} ");
                } else if(Regex.IsMatch(message.ToString(), @"discover a treasure coffer!$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    if(!_diggerKey.IsNullOrEmpty()) {
                        PluginLog.Debug($"Time since dig: {(DateTime.Now - _digTime).TotalMilliseconds} ms");
                    }
                    //lock in dig only if we have a recent digger
                    _isDigLockedIn = !_diggerKey.IsNullOrEmpty() && ((DateTime.Now - _digTime).TotalSeconds < _digThresholdSeconds);
                    PluginLog.Debug($"_diggerKey: {_diggerKey} _digTime: {_digTime}  _isDigLockedIn: {_isDigLockedIn} _portalBlockUntil: {_portalBlockUntil} ");
                } else if(Regex.IsMatch(message.ToString(), @"releasing a powerful musk into the air!$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    //delay registering the new map to avoid double counting with self maps if someone uses dig at same time
                    //Thread.Sleep(_addMapDelaySeconds * 1000);
                    Task.Delay(_addMapDelaySeconds * 1000).ContinueWith(t => {
                        AddMap(CurrentPartyList[_diggerKey], DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name!);
                        //have to reset here in case you fail to defeat the treasure chest enemies -_ -
                        ResetDigStatus();
                    });

                    //if(_isDigLockedIn) {
                    //    newMapFound = true;
                    //    key = _diggerKey;
                    //}

                } else if(Regex.IsMatch(message.ToString(), @"defeat all the enemies drawn by the trap!$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    ResetDigStatus();
                    //block portals from adding maps for a brief period to avoid double counting
                    _portalBlockUntil = DateTime.Now.AddSeconds(_portalBlockSeconds);
                }
                //Regex toMatch = new Regex(@"map crumbles into dust.$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                //MatchCollection matchCollection = toMatch.Matches(message.ToString());
                //newMapFound = matchCollection.Count > 0;
                //mapType = Regex.Match(message.ToString(), @"\w* [\w']* map(?=\scrumbles into dust)").ToString();
                //key = $"{ClientState.LocalPlayer.Name} {ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
            } else if((int)type == 4139) {
                //party member uses dig
                if(Regex.IsMatch(message.ToString(), @"uses Dig.$")) {
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    foreach(Payload payload in message.Payloads) {
                        PluginLog.Debug($"payload: {payload}");
                    }
                    PluginLog.Debug($"_diggerKey: {_diggerKey} _digTime: {_digTime}  _isDigLockedIn: {_isDigLockedIn} _portalBlockUntil: {_portalBlockUntil} ");
                    var playerPayload = (PlayerPayload)message.Payloads.First(p => p.Type == PayloadType.Player);
                    var newDigTime = DateTime.Now;
                    //register dig if no dig locked in or fallback time elapsed AND no digger registered or threshold time elapsed
                    if((!_isDigLockedIn || (newDigTime - _digTime).TotalSeconds > _digFallbackSeconds) && (_diggerKey.IsNullOrEmpty() || (newDigTime - _digTime).TotalSeconds > _digThresholdSeconds)) {
                        PluginLog.Debug("Registering new digger!");
                        ResetDigStatus();
                        _diggerKey = $"{playerPayload.PlayerName} {playerPayload.World.Name}";
                        _digTime = newDigTime;
                    }
                    PluginLog.Debug($"_diggerKey: {_diggerKey} _digTime: {_digTime}  _isDigLockedIn: {_isDigLockedIn} _portalBlockUntil: {_portalBlockUntil} ");
                }
            } else if(type == XivChatType.Party) {
                //getting map link

                //foreach(Payload payload in sender.Payloads) {
                //    PluginLog.Debug($"sender payload: {payload}");
                //}

                var mapPayload = (MapLinkPayload)message.Payloads.FirstOrDefault(p => p.Type == PayloadType.MapLink);
                var senderPayload = (PlayerPayload)sender.Payloads.FirstOrDefault(p => p.Type == PayloadType.Player);

                if(senderPayload == null) {
                    //PluginLog.Debug("senderPayload null!");
                    //regex
                    string matchName = Regex.Match(sender.TextValue, @"[A-Za-z-']*\s[A-Za-z-']*$").ToString();
                    key = $"{matchName} {ClientState.LocalPlayer!.HomeWorld.GameData!.Name}";
                } else {
                    //PluginLog.Debug("senderPayload not null!");
                    key = $"{senderPayload.PlayerName} {senderPayload.World.Name}";
                }
                if(mapPayload != null) {
                    //PluginLog.Debug("map payload found!");
                    //CurrentPartyList[key].MapLink = SeString.CreateMapLink(mapPayload.TerritoryType.RowId, mapPayload.Map.RowId, mapPayload.XCoord, mapPayload.YCoord);
                    CurrentPartyList[key].MapLink = mapPayload;
                    Configuration.Save();
                }
            }

            if(newMapFound && CurrentPartyList.Count > 0) {
                AddMap(CurrentPartyList[key], DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name!, mapType);
                //var newMap = new MPAMap(mapType, DateTime.Now, DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name);
                //PluginLog.Debug($"Adding new map to {key}");
                //CurrentPartyList[key].Maps.Add(newMap);
                //CurrentPartyList[key].MapLink = null;
                //this.Configuration.Save();
            }
        }

        private void OnLogin(object? sender, EventArgs e) {
            Configuration.PruneRecentPartyList();
            BuildCurrentPartyList();
            BuildRecentPartyList();
            CheckAndArchiveMaps(Configuration.RecentPartyList);
            ResetDigStatus();
            //this.Configuration.Save();
        }

        private void OnLogout(object? sender, EventArgs e) {
            //remove all party members
            this.CurrentPartyList = new();
            ResetDigStatus();
            Configuration.PruneRecentPartyList();
        }

        //only works on instanced duties...hmmm....
        private void OnDutyStart(object? sender, ushort territoryId) {
            //PluginLog.Debug($"Duty has started");
            PluginLog.Debug($"Duty has started with territory id: {territoryId} name: {DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");
        }

        //builds current party list, from scratch
        private void BuildCurrentPartyList() {
            string currentPlayerName = ClientState.LocalPlayer!.Name.ToString()!;
            string currentPlayerWorld = ClientState.LocalPlayer!.HomeWorld.GameData!.Name!;
            this.CurrentPartyList = new();

            foreach(PartyMember p in this.PartyList) {
                string partyMemberName = p.Name.ToString();
                string partyMemberWorld = p.World.GameData.Name.ToString();
                string key = $"{partyMemberName} {partyMemberWorld}";
                bool isCurrentPlayer = partyMemberName.Equals(currentPlayerName) && partyMemberWorld.Equals(currentPlayerWorld);

                //new player!
                if(!this.Configuration.RecentPartyList.ContainsKey(key)) {
                    var newPlayer = new MPAMember(partyMemberName, partyMemberWorld, isCurrentPlayer);
                    Configuration.RecentPartyList.Add(key, newPlayer);
                    this.CurrentPartyList.Add(key, newPlayer);
                } else {
                    //find existing player
                    this.Configuration.RecentPartyList[key].LastJoined = DateTime.Now;
                    this.CurrentPartyList.Add(key, this.Configuration.RecentPartyList[key]);
                }
            }
            this.Configuration.Save();
        }

        private void BuildRecentPartyList() {
            this.RecentPartyList = new();
            foreach(var player in this.Configuration.RecentPartyList) {
                TimeSpan timeSpan = DateTime.Now - player.Value.LastJoined;
                var isRecent = timeSpan.TotalHours <= Configuration.ArchiveThresholdHours;
                var hasMaps = false;
                foreach(var map in player.Value.Maps) {
                    if(!map.IsArchived && !map.IsDeleted) {
                        hasMaps = true;
                        break;
                    }
                }
                var notCurrent = !CurrentPartyList.ContainsKey(player.Key);
                var notSelf = !player.Value.IsSelf;
                if(isRecent && hasMaps && notCurrent) {
                    this.RecentPartyList.Add(player.Key, player.Value);
                }
            }
        }

        private void CheckAndArchiveMaps(Dictionary<string, MPAMember> list) {
            DateTime currentTime = DateTime.Now;
            foreach(MPAMember player in list.Values) {
                foreach(MPAMap map in player.Maps) {
                    TimeSpan timeSpan = currentTime - map.Time;
                    if(timeSpan.TotalHours > Configuration.ArchiveThresholdHours) {
                        map.IsArchived = true;
                    }
                }
            }
            this.Configuration.Save();
        }

        //archive all of the maps for the given list
        private void ForceArchiveAllMaps(Dictionary<string, MPAMember> list) {
            foreach(var player in list.Values) {
                foreach(var map in player.Maps) {
                    map.IsArchived = true;
                }
            }
        }

        private void ResetDigStatus() {
            _diggerKey = "";
            _isDigLockedIn = false;
            _portalBlockUntil = DateTime.UnixEpoch;
            _digTime = DateTime.UnixEpoch;
        }

        public void ClearAllMaps() {
            ForceArchiveAllMaps(Configuration.RecentPartyList);
            ForceArchiveAllMaps(FakePartyList);
            BuildRecentPartyList();
            Configuration.Save();
        }

        public void AddMap(MPAMember player, string zone = "", string type = "", bool isManual = false) {
            //zone ??= DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name!;
            PluginLog.Debug($"Adding new map to {player.Name} {player.HomeWorld}");
            var newMap = new MPAMap(type, DateTime.Now, zone, false, isManual);
            player.Maps.Add(newMap);
            if(!isManual) {
                player.MapLink = null;
            }
            LastMapPlayerKey = player.Key;
            Configuration.Save();
        }

        public void RemoveLastMap(MPAMember player) {
            player.Maps.RemoveAt(player.Maps.Count - 1);
        }

        public void TestFunction() {
            Functions.OpenMap(19);
            //FFXIVClientStructs.FFXIV.Application.OpenMap();
            //AgentMap* agont = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
            //FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.MemberFunctionPointers.OpenMapByMapId(19);
            //PluginLog.Debug($"Current party members: {PartyList.Length}");
            //foreach(var player in PartyList) {
            //    PluginLog.Debug($"Party Member: {player.Name} {player.World.GameData.Name}");
            //}

            //PluginLog.Debug($"{ClientState.LocalPlayer.Name} {ClientState.LocalPlayer!.HomeWorld.GameData!.Name}");
        }

        public void TestFunction2() {
            //foreach(var world in _worlds) {
            //    PluginLog.Debug($"World: {world.Key} {world.Value.Name}");
            //}
            foreach(var map in this.DataManager.GetExcelSheet<Map>()!) {
                PluginLog.Debug($"{map.RowId} {map.PlaceName.Value.Name}");
            }
        }

        public void TestFunction3() {
            PluginLog.Debug($"Is duty started? {DutyState.IsDutyStarted}");
            var dutyId = Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");

            //var x = GameGui.GetAddonByName("_ToDoList");
            //Functions.testfunc(x);
            //PluginLog.Debug($"AddonToDoList ptr: {x}");
            //PluginLog.Debug($"AddonToDoList heckin based!");
        }

        public void OpenMapLink(MapLinkPayload mapLink) {
            GameGui.OpenMapWithMapLink(mapLink);
            //var mapPayload = (MapLinkPayload)mapLink.Payloads.FirstOrDefault(p => p.Type == PayloadType.MapLink);
            //Functions.SetFlagMarkers(mapPayload.TerritoryType.RowId, mapPayload.Map.RowId, mapPayload.XCoord, mapPayload.YCoord);
            //Functions.OpenMap(mapPayload.Map.RowId);
            //FFXIVClientStructs.FFXIV.Application.OpenMap();
        }
    }
}

//partial class PartyList {

//}
