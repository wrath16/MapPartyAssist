using Dalamud.Configuration;
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
using LiteDB;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Services;
using MapPartyAssist.Types;
using MapPartyAssist.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MapPartyAssist {
    public sealed class Plugin : IDalamudPlugin {
        public string Name => "Map Party Assist";
        private const string CommandName = "/mparty";
        private const string StatsCommandName = "/mpartystats";
        private const string TestCommandName = "/mpartytest";

        //Dalamud services
        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        internal DataManager DataManager { get; init; }
        internal ClientState ClientState { get; init; }
        internal DutyState DutyState { get; init; }
        private PartyList PartyList { get; init; }
        internal ChatGui ChatGui { get; init; }
        private GameGui GameGui { get; init; }
        private Framework Framework { get; init; }
        internal DutyManager DutyManager { get; init; }
        internal MapManager MapManager { get; init; }
        internal StorageManager StorageManager { get; init; }
        public Configuration Configuration { get; init; }
        internal GameFunctions Functions { get; }
        public WindowSystem WindowSystem = new("Map Party Assist");

        private MainWindow MainWindow;
        private StatsWindow StatsWindow;
        private ConfigWindow ConfigWindow;
        private TestFunctionWindow TestFunctionWindow;

        internal LiteDatabase Database { get; init; }

        public Dictionary<string, MPAMember> CurrentPartyList { get; private set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; private set; } = new();
        //public string LastMapPlayerKey { get; private set; } = "";

        //TODO delete (for testing only!)
        public Dictionary<string, MPAMember> FakePartyList { get; private set; }

        private int _lastPartySize;

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
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            DataManager = dataManager;
            ClientState = clientState;
            DutyState = dutyState;
            PartyList = partyList;
            ChatGui = chatGui;
            GameGui = gameGui;
            Framework = framework;

            PluginLog.Log("Begin Config loading");
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            PluginLog.Log("Done Config loading");


            StorageManager = new StorageManager(this, $"{PluginInterface.GetPluginConfigDirectory()}\\data.db");
            Functions = new GameFunctions();
            DutyManager = new DutyManager(this);
            MapManager = new MapManager(this);

            MainWindow = new MainWindow(this);
            //ZoneCountWindow = new ZoneCountWindow(this, MainWindow);
            ConfigWindow = new ConfigWindow(this);
            StatsWindow = new StatsWindow(this);
            WindowSystem.AddWindow(MainWindow);
            //WindowSystem.AddWindow(ZoneCountWindow);
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(StatsWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                HelpMessage = "Opens map party assist."
            });

            CommandManager.AddHandler(StatsCommandName, new CommandInfo(OnStatsCommand) {
                HelpMessage = "Opens stats window."
            });

#if DEBUG
            TestFunctionWindow = new TestFunctionWindow(this);
            WindowSystem.AddWindow(TestFunctionWindow);
            CommandManager.AddHandler("/mpartytest", new CommandInfo(OnTestCommand) {
                HelpMessage = "Opens test window."
            });
#endif

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Framework.Update += OnFrameworkUpdate;
            ChatGui.ChatMessage += OnChatMessage;
            ClientState.Login += OnLogin;
            ClientState.Logout += OnLogout;

            //build current and recent party lists
            if(ClientState.IsLoggedIn) {
                BuildCurrentPartyList();
                BuildRecentPartyList();
            } else {
                CurrentPartyList = new();
                RecentPartyList = new();
            }


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

        public IPluginConfiguration? GetPluginConfig() {
            //string pluginName = PluginInterface.InternalName;
            FileInfo configFile = PluginInterface.ConfigFile;
            if(!configFile.Exists) {
                return null;
            }
            return JsonConvert.DeserializeObject<IPluginConfiguration>(File.ReadAllText(configFile.FullName), new JsonSerializerSettings {
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.Objects
            });
        }

        public void Dispose() {
            PluginLog.Debug("disposing...");

            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(StatsCommandName);
#if DEBUG
            CommandManager.RemoveHandler("/mpartytest");
#endif

            Framework.Update -= OnFrameworkUpdate;
            ChatGui.ChatMessage -= OnChatMessage;
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;
            //ClientState.TerritoryChanged -= OnTerritoryChanged;
            //DutyState.DutyStarted -= OnDutyStart;
            //DutyState.DutyCompleted -= OnDutyCompleted;
            //DutyState.DutyWiped -= OnDutyWiped;
            //DutyState.DutyCompleted -= OnDutyRecommenced;

            MapManager.Dispose();
            DutyManager.Dispose();
            StorageManager.Dispose();

            Configuration.PruneRecentPartyList();
        }

        private void OnCommand(string command, string args) {
            MainWindow.IsOpen = true;
        }

        private void OnStatsCommand(string command, string args) {
            StatsWindow.IsOpen = true;
        }

        private void OnTestCommand(string command, string args) {
            TestFunctionWindow.IsOpen = true;
        }

        private void DrawUI() {
            WindowSystem.Draw();
        }

        public void DrawConfigUI() {
            WindowSystem.GetWindow("Map Party Assist Settings").IsOpen = true;
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

        //private void OnTerritoryChanged(object? sender, ushort territoryId) {
        //    ResetDigStatus();
        //}

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {

            //filter nuisance combat messages...
            switch((int)type) {
                case 2091:
                case 2219:
                case 2221: //you recover hp
                case 2222:
                case 2223: //you suffer from effect
                case 2224:
                case 2225: //you recover from effect
                case 2603:
                case 2729:
                case 2730: //self combat
                case 2731: //self cast
                case 2735:
                case 2858: //enemy resists
                case 2874: //you defeat enemy
                case 2857: //you crit/dh dmg enemy
                case 2863: //enemy suffers effect
                case 4139: //party member actions
                    if(message.ToString().Contains("Dig", StringComparison.OrdinalIgnoreCase) || message.ToString().Contains("Decipher", StringComparison.OrdinalIgnoreCase)) {
                        goto default;
                    }
                    break;
                case 4269: //critical hp from party member
                case 4270: //gain effect from party member
                case 4394: //party member unaffected
                case 4397:
                case 4398:
                case 4399: //party member brush with death
                case 4400:
                case 4401: //party member recovers from detrimental effect
                case 4777:
                case 4778:
                case 4783: //effect
                case 4905: //combat
                case 4906:
                case 4911:
                case 8235:
                case 8236:
                case 8746:
                case 8749: //other recover hp
                case 8750:
                case 8751: //other suffers effect
                case 8752:
                case 8753: //other recover from effect
                case 9001: //other combat
                case 10283:
                case 10409: //you dmged by enemy
                case 10410:
                case 10537:
                case 10538: //misses party member
                case 10922: //attack misses
                case 10929:
                case 11305: //companion
                case 11306: //companion
                case 12331:
                case 12457: //
                case 12585: //hits party member
                case 12841: //other combat
                case 13098:
                case 13102:
                case 13105: //enemy recovers from status
                case 13114: //enemy defeated
                case 13225: //npc hits enemy
                case 13226: //npc status
                case 13353: //companion
                case 14379: //npc uses ability
                case 15145: //npc takes damage
                case 15162: //npc defeats enemy
                case 15278: //gains effect
                case 15280: //loses effect
                case 15281: //npc recovers from status
                case 16427:
                case 16558: //gain choco regen
                case 17065:
                case 17454: //companion
                case 17456:
                case 18475:
                case 18605: //crit hp
                case 18733:
                case 19113: //enemy taking dmg
                case 19241: //crit
                case 19632: //party member companion loses beneficial effect
                case 22571: //other companion
                case 23082: //other
                case 23085: //other
                case 23086: //other
                    break;
                default:
                    PluginLog.Debug($"Message received: {type} {message} from {sender}");
                    //foreach(Payload payload in message.Payloads) {
                    //    PluginLog.Debug($"payload: {payload}");
                    //}
                    break;
            }
        }

        private void OnLogin(object? sender, EventArgs e) {
            Configuration.PruneRecentPartyList();
            BuildCurrentPartyList();
            BuildRecentPartyList();
            MapManager.CheckAndArchiveMaps(Configuration.RecentPartyList);
            //ResetDigStatus();
            //this.Configuration.Save();
        }

        private void OnLogout(object? sender, EventArgs e) {
            //remove all party members
            CurrentPartyList = new();
            //ResetDigStatus();
            Configuration.PruneRecentPartyList();
            Save();
        }

        //builds current party list, from scratch
        public void BuildCurrentPartyList() {
            string currentPlayerName = ClientState.LocalPlayer!.Name.ToString()!;
            string currentPlayerWorld = ClientState.LocalPlayer!.HomeWorld.GameData!.Name!;
            string currentPlayerKey = $"{currentPlayerName} {currentPlayerWorld}";
            CurrentPartyList = new();

            ////enable for solo player
            //if(Configuration.EnableWhileSolo && PartyList.Length <= 0) {
            //    //add yourself for initial setup
            //    if(!Configuration.RecentPartyList.ContainsKey(currentPlayerKey)) {
            //        var newPlayer = new MPAMember(currentPlayerName, currentPlayerWorld, true);
            //        Configuration.RecentPartyList.Add(currentPlayerKey, newPlayer);
            //        CurrentPartyList.Add(currentPlayerKey, newPlayer);
            //    } else {
            //        //find existing player
            //        Configuration.RecentPartyList[currentPlayerKey].LastJoined = DateTime.Now;
            //        CurrentPartyList.Add(currentPlayerKey, Configuration.RecentPartyList[currentPlayerKey]);
            //    }
            //    Configuration.RecentPartyList[currentPlayerKey].LastJoined = DateTime.Now;
            //} else {
            //    foreach(PartyMember p in PartyList) {
            //        string partyMemberName = p.Name.ToString();
            //        string partyMemberWorld = p.World.GameData.Name.ToString();
            //        var key = $"{partyMemberName} {partyMemberWorld}";
            //        bool isCurrentPlayer = partyMemberName.Equals(currentPlayerName) && partyMemberWorld.Equals(currentPlayerWorld);
            //        //if(isCurrentPlayer) continue;

            //        //new player!
            //        if(!Configuration.RecentPartyList.ContainsKey(key)) {
            //            var newPlayer = new MPAMember(partyMemberName, partyMemberWorld, isCurrentPlayer);
            //            Configuration.RecentPartyList.Add(key, newPlayer);
            //            CurrentPartyList.Add(key, newPlayer);
            //        } else {
            //            //find existing player
            //            Configuration.RecentPartyList[key].LastJoined = DateTime.Now;
            //            CurrentPartyList.Add(key, Configuration.RecentPartyList[key]);
            //        }
            //    }
            //}
            //Save();

            var allPlayers = StorageManager.GetPlayers();
            var currentPlayer = allPlayers.Query().Where(p => p.Key == currentPlayerKey).FirstOrDefault();
            //enable for solo player
            if(Configuration.EnableWhileSolo && PartyList.Length <= 0) {
                //add yourself for initial setup
                if(currentPlayer == null) {
                    var newPlayer = new MPAMember(currentPlayerName, currentPlayerWorld, true);
                    CurrentPartyList.Add(currentPlayerKey, newPlayer);
                    StorageManager.AddPlayer(newPlayer);
                } else {
                    currentPlayer.LastJoined = DateTime.Now;
                    CurrentPartyList.Add(currentPlayerKey, currentPlayer);
                    StorageManager.UpdatePlayer(currentPlayer);
                }
            } else {
                foreach(PartyMember p in PartyList) {
                    string partyMemberName = p.Name.ToString();
                    string partyMemberWorld = p.World.GameData.Name.ToString();
                    var key = $"{partyMemberName} {partyMemberWorld}";
                    bool isCurrentPlayer = partyMemberName.Equals(currentPlayerName) && partyMemberWorld.Equals(currentPlayerWorld);
                    var findPlayer = allPlayers.Query().Where(p => p.Key == key).FirstOrDefault();

                    //new player!
                    if(findPlayer == null) {
                        var newPlayer = new MPAMember(partyMemberName, partyMemberWorld, isCurrentPlayer);
                        CurrentPartyList.Add(key, newPlayer);
                        StorageManager.AddPlayer(newPlayer);
                    } else {
                        //find existing player
                        findPlayer.LastJoined = DateTime.Now;
                        findPlayer.IsSelf = isCurrentPlayer;
                        CurrentPartyList.Add(key, findPlayer);
                        StorageManager.UpdatePlayer(findPlayer);
                    }
                }
            }
            Save();
        }

        public void BuildRecentPartyList() {
            RecentPartyList = new();
            //foreach(var player in Configuration.RecentPartyList) {
            //    TimeSpan timeSpan = DateTime.Now - player.Value.LastJoined;
            //    var isRecent = timeSpan.TotalHours <= Configuration.ArchiveThresholdHours;
            //    var hasMaps = false;
            //    foreach(var map in player.Value.Maps) {
            //        if(!map.IsArchived && !map.IsDeleted) {
            //            hasMaps = true;
            //            break;
            //        }
            //    }
            //    var notCurrent = !CurrentPartyList.ContainsKey(player.Key);
            //    var notSelf = !player.Value.IsSelf;
            //    if(isRecent && hasMaps && notCurrent) {
            //        RecentPartyList.Add(player.Key, player.Value);
            //    }
            //}
            //Save();

            var allPlayers = StorageManager.GetPlayers();
            var currentMaps = StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted).ToList();
            foreach(var player in allPlayers.Query().ToList()) {
                TimeSpan timeSpan = DateTime.Now - player.LastJoined;
                var isRecent = timeSpan.TotalHours <= Configuration.ArchiveThresholdHours;
                var hasMaps = currentMaps.Where(m => m.Owner.Equals(player.Key)).Any();

                var notCurrent = !CurrentPartyList.ContainsKey(player.Key);
                var notSelf = !player.IsSelf;
                if(isRecent && hasMaps && notCurrent) {
                    RecentPartyList.Add(player.Key, player);
                }
            }
            Save();
        }

        public void ToggleEnableSolo(bool toSet) {
            Configuration.EnableWhileSolo = toSet;
            BuildCurrentPartyList();
            BuildRecentPartyList();
            Save();
        }

        public void OpenMapLink(MapLinkPayload mapLink) {
            GameGui.OpenMapWithMapLink(mapLink);
        }

        public void Save() {
            Configuration.Save();
            //this causes a small hitch
            StatsWindow.Refresh();
            MainWindow.Refresh();
        }

        public void TestFunction() {
            PluginLog.Debug($"Current duty id: {Functions.GetCurrentDutyId()}");

            //DutyManager.StartNewDuty("the hidden canals of uznair", 276, CurrentPartyList, "Test Player TestServer");
            //Configuration.Save();

            //Functions.OpenMap(19);
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
            foreach(var map in DataManager.GetExcelSheet<Map>()!) {
                PluginLog.Debug($"{map.RowId} {map.PlaceName.Value.Name}");
            }
        }

        public void TestFunction3() {
            PluginLog.Debug($"Is duty started? {DutyState.IsDutyStarted}");
            var dutyId = Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");

            var x = GameGui.GetAddonByName("_ToDoList");
            Functions.testfunc(x);
            PluginLog.Debug($"AddonToDoList ptr: {x}");
            PluginLog.Debug($"AddonToDoList heckin based!");
        }

        public void TestFunction4() {

            List<DutyResults> toRemove = new();

            //clear shifting altars
            foreach(var dr in Configuration.DutyResults) {
                if(dr.DutyId == 586) {
                    toRemove.Add(dr);
                }
            }
            PluginLog.Debug($"Removing {toRemove.Count} results");
            foreach(var dr in toRemove) {
                Configuration.DutyResults.Remove(dr);
            }

            Save();
        }

        public void TestFunction5() {

            foreach(var duty in DataManager.GetExcelSheet<ContentFinderCondition>()) {
                PluginLog.Debug($"id: {duty.RowId} name: {duty.Name}");
            }

            //DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId)
        }

        public void TestFuncAddDRToDB() {
            //var col = Database.GetCollection<DutyResults>("dutyresults");



            ////var col2 = Database.GetCollection<DutyResults>("dutyresults2", )

            ////BsonMapper.Global.RegisterType<DutyResults>(
            ////    serialize: (dr) => {

            ////        //var doc = new BsonDocument(dr);

            ////        var doc = new BsonDocument();
            ////        doc["Version"] = dr.Version;
            ////        doc["DutyId"] = dr.DutyId;
            ////        doc["DutyName"] = dr.DutyName;
            ////        doc["Time"] = dr.Time;
            ////        doc["CompletionTime"] = dr.CompletionTime;
            ////        doc["Owner"] = dr.Owner;
            ////        doc["IsComplete"] = dr.IsComplete;
            ////        doc["IsPickup"] = dr.IsPickup;

            ////        return doc;
            ////    },
            ////    deserialize: (bson) => new DutyResults()

            ////    );


            //col.InsertBulk(Configuration.DutyResults);
        }

        public void TestGetDRFromDB() {
            //var results = Database.GetCollection<DutyResults>("dutyresults").Query().Limit(10).ToList();
            //PluginLog.Debug($"{results.First().CheckpointResults.Count}");
        }

        public void TestUpdateMap() {
            MPAMap map = Configuration.RecentPartyList["Sarah Montcroix Siren"].Maps.Last();
            map.IsArchived = true;
            PluginLog.Debug($"ID: {map.Id}");
            StorageManager.UpdateMap(map);
            //PluginLog.Debug($"Map found? {result}");
        }
    }
}
