using Dalamud.Configuration;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using MapPartyAssist.Services;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using MapPartyAssist.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist {

    public enum StatusLevel {
        OK,
        CAUTION,
        ERROR
    }

    public sealed class Plugin : IDalamudPlugin {
        public string Name => "Map Party Assist";

        private const string DatabaseName = "data.db";

        private const string CommandName = "/mparty";
        private const string ConfigCommandName = "/mpartyconfig";
        private const string StatsCommandName = "/mpartystats";
        private const string DutyResultsCommandName = "/mpartydutyresults";
        private const string TestCommandName = "/mpartytest";

        //Dalamud services
        internal DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        internal IDataManager DataManager { get; init; }
        internal IClientState ClientState { get; init; }
        internal ICondition Condition { get; init; }
        internal IDutyState DutyState { get; init; }
        private IPartyList PartyList { get; init; }
        internal IChatGui ChatGui { get; init; }
        private IGameGui GameGui { get; init; }
        private IFramework Framework { get; init; }
        internal IPluginLog Log { get; init; }

        //Custom services
        internal DutyManager DutyManager { get; init; }
        internal MapManager MapManager { get; init; }
        internal StorageManager StorageManager { get; init; }
        internal ImportManager ImportManager { get; init; }

        public Configuration Configuration { get; init; }
        internal GameFunctions Functions { get; init; }

        //UI
        internal WindowSystem WindowSystem = new("Map Party Assist");
        private MainWindow MainWindow;
        private StatsWindow StatsWindow;
        private ConfigWindow ConfigWindow;
        private DutyResultsWindow DutyResultsWindow;
#if DEBUG
        private TestFunctionWindow TestFunctionWindow;
#endif

        public Dictionary<string, MPAMember> CurrentPartyList { get; private set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; private set; } = new();
        private int _lastPartySize = 0;

        private SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] IDataManager dataManager,
            [RequiredVersion("1.0")] IClientState clientState,
            [RequiredVersion("1.0")] ICondition condition,
            [RequiredVersion("1.0")] IDutyState dutyState,
            [RequiredVersion("1.0")] IPartyList partyList,
            [RequiredVersion("1.0")] IChatGui chatGui,
            [RequiredVersion("1.0")] IGameGui gameGui,
            [RequiredVersion("1.0")] IFramework framework,
            [RequiredVersion("1.0")] IPluginLog log) {
            try {
                PluginInterface = pluginInterface;
                CommandManager = commandManager;
                DataManager = dataManager;
                ClientState = clientState;
                Condition = condition;
                DutyState = dutyState;
                PartyList = partyList;
                ChatGui = chatGui;
                GameGui = gameGui;
                Framework = framework;
                Log = log;

                Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

                Log.Information($"Client language: {ClientState.ClientLanguage}");
                Log.Verbose($"Current culture: {CultureInfo.CurrentCulture.Name}");
                if(!IsEnglishClient()) {
                    Log.Warning("Client is not English, most functions will be unavailable.");
                }

                StorageManager = new StorageManager(this, $"{PluginInterface.GetPluginConfigDirectory()}\\{DatabaseName}");
                Functions = new GameFunctions();
                DutyManager = new DutyManager(this);
                MapManager = new MapManager(this);
                ImportManager = new ImportManager(this);

                //needs DutyManager to be initialized first
                Configuration.Initialize(this);

                MainWindow = new MainWindow(this);
                WindowSystem.AddWindow(MainWindow);
                CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                    HelpMessage = "Opens map party assist."
                });

                StatsWindow = new StatsWindow(this);
                WindowSystem.AddWindow(StatsWindow);
                CommandManager.AddHandler(StatsCommandName, new CommandInfo(OnStatsCommand) {
                    HelpMessage = "Opens stats window."
                });

                ConfigWindow = new ConfigWindow(this);
                WindowSystem.AddWindow(ConfigWindow);
                CommandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand) {
                    HelpMessage = "Open settings window."
                });

                DutyResultsWindow = new DutyResultsWindow(this);
                WindowSystem.AddWindow(DutyResultsWindow);
                CommandManager.AddHandler(DutyResultsCommandName, new CommandInfo(OnDutyResultsCommand) {
                    HelpMessage = "Edit duty results (Advanced)."
                });

#if DEBUG
                TestFunctionWindow = new TestFunctionWindow(this);
                WindowSystem.AddWindow(TestFunctionWindow);
                CommandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand) {
                    HelpMessage = "Opens test functions window. (Debug)"
                });
#endif

                PluginInterface.UiBuilder.Draw += DrawUI;
                PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

                Framework.Update += OnFrameworkUpdate;
                ChatGui.ChatMessage += OnChatMessage;
                ClientState.Login += OnLogin;
                ClientState.Logout += OnLogout;

                //import data from old configuration to database
                if(Configuration.Version < 2) {
                    StorageManager.Import();
                }

                //build current and recent party lists
                if(ClientState.IsLoggedIn) {
                    BuildCurrentPartyList();
                    BuildRecentPartyList();
                } else {
                    CurrentPartyList = new();
                    RecentPartyList = new();
                }
            } catch(Exception e) {
                //remove handlers and release database if we fail to start
                Dispose();
                //it really shouldn't ever be null
                Log!.Error($"Failed to initialize plugin constructor: {e.Message}");
                //re-throw to prevent constructor from initializing
                throw;
            }
        }

        //Custom config loader. Unused
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
#if DEBUG
            Log.Debug("disposing plugin");
#endif

            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(ConfigCommandName);
            CommandManager.RemoveHandler(StatsCommandName);
            CommandManager.RemoveHandler(DutyResultsCommandName);
#if DEBUG
            CommandManager.RemoveHandler(TestCommandName);
#endif

            Framework.Update -= OnFrameworkUpdate;
            ChatGui.ChatMessage -= OnChatMessage;
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;

            if(MapManager != null) {
                MapManager.Dispose();
            }
            if(DutyManager != null) {
                DutyManager.Dispose();
            }
            if(StorageManager != null) {
                StorageManager.Dispose();
            }
        }

        private void OnCommand(string command, string args) {
            MainWindow.IsOpen = true;
        }

        private void OnStatsCommand(string command, string args) {
            StatsWindow.IsOpen = true;
        }

        private void OnConfigCommand(string command, string args) {
            DrawConfigUI();
        }

        private void OnDutyResultsCommand(string command, string args) {
            DutyResultsWindow.IsOpen = true;
        }

#if DEBUG
        private void OnTestCommand(string command, string args) {
            TestFunctionWindow.IsOpen = true;
        }
#endif

        private void DrawUI() {
            WindowSystem.Draw();
        }

        private void DrawConfigUI() {
            ConfigWindow.IsOpen = true;
        }

        private void OnFrameworkUpdate(IFramework framework) {
            var playerJob = ClientState.LocalPlayer?.ClassJob.GameData?.Abbreviation;
            var currentPartySize = PartyList.Length;

            if(!Condition[ConditionFlag.BetweenAreas] && playerJob != null && currentPartySize != _lastPartySize) {
                Log.Verbose($"Party size has changed: {_lastPartySize} to {currentPartySize}");
                BuildCurrentPartyList();
                BuildRecentPartyList();
                _lastPartySize = currentPartySize;
            }
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            //filter nuisance combat messages...
            switch((int)type) {
                case 2091:
                case 2106: //self revived
                case 2217:
                case 2218:
                case 2219:
                case 2221: //you recover hp
                case 2222:
                case 2223: //you suffer from effect
                case 2224:
                case 2225: //you recover from effect
                case 2350:
                case 2603:
                case 2729:
                case 2730: //self combat
                case 2731: //self cast
                case 2735:
                case 2858: //enemy resists
                case 2859:
                case 2861:
                case 2864:
                case 2874: //you defeat enemy
                case 2857: //you crit/dh dmg enemy
                case 2863: //enemy suffers effect
                case 4139: //party member actions
                    if(message.ToString().Contains("Dig", StringComparison.OrdinalIgnoreCase) || message.ToString().Contains("Decipher", StringComparison.OrdinalIgnoreCase)) {
                        goto default;
                    }
                    break;
                case 4154: //party member revived
                case 4266:
                case 4269: //critical hp from party member
                case 4270: //gain effect from party member
                case 4393:
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
                case 4909:
                case 4911:
                case 4922: //party member defeats enemy
                case 6187:
                case 6573:
                case 6574:
                case 6576:
                case 6577:
                case 6825:
                case 6831:
                case 6953:
                case 6959:
                case 8235:
                case 8236:
                case 8745: //takes fall dmg
                case 8746:
                case 8749: //other recover hp
                case 8750:
                case 8751: //other suffers effect
                case 8752:
                case 8753: //other recover from effect
                case 9001: //other combat
                case 9002:
                case 9007: //other combat
                case 10283:
                case 10409: //you dmged by enemy
                case 10410:
                case 10537:
                case 10538: //misses party member
                case 10665:
                case 10922: //attack misses
                case 10926: //engaged enemy gains beneficial effect
                case 10929:
                case 11305: //companion
                case 11306: //companion
                case 12331:
                case 12346:
                case 12457:
                case 12458:
                case 12461:
                case 12463:
                case 12464:
                case 12585: //hits party member
                case 12586: //attack misses party member
                case 12589:
                case 12591:
                case 12592:
                case 12713:
                case 12717:
                case 12719:
                case 12841: //other combat
                case 13098:
                case 13101:
                case 13102:
                case 13103:
                case 13104:
                case 13105: //enemy recovers from status
                case 13097:
                case 13114: //enemy defeated
                case 13225: //npc hits enemy
                case 13226: //npc status
                case 13353: //companion
                case 14379: //npc uses ability
                case 15145: //npc takes damage
                case 15146: //miss
                case 15151: //suffers effect
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
                case 18606:
                case 18733:
                case 18734:
                case 19113: //enemy taking dmg
                case 19247:
                case 19241: //crit
                case 19258:
                case 19632: //party member companion loses beneficial effect
                case 19633:
                case 19626:
                case 20523:
                case 20909:
                case 21161:
                case 22571: //other companion
                case 22697:
                case 22703:
                case 22825:
                case 22831:
                case 23081:
                case 23082: //other
                case 23085: //other
                case 23086: //other
                case 23337:
                case 23342:
                case 23985:
                case 23978:
                    break;
                default:
                    Log.Verbose($"Message received: {type} {message} from {sender}");
                    //foreach(Payload payload in message.Payloads) {
                    //    Log.Verbose($"payload: {payload}");
                    //}
                    break;
            }
        }

        private void OnLogin() {
            BuildCurrentPartyList();
            BuildRecentPartyList();
            MapManager.CheckAndArchiveMaps();
        }

        private void OnLogout() {
            //remove all party members
            CurrentPartyList = new();
            Save();
        }

        //builds current party list from scratch
        public void BuildCurrentPartyList() {
            try {
                _saveLock.Wait();
                Log.Verbose("Rebuilding current party list.");
                string currentPlayerName = ClientState.LocalPlayer!.Name.ToString()!;
                string currentPlayerWorld = ClientState.LocalPlayer!.HomeWorld.GameData!.Name!;
                string currentPlayerKey = GetCurrentPlayer();
                CurrentPartyList = new();
                var allPlayers = StorageManager.GetPlayers();
                var currentPlayer = allPlayers.Query().Where(p => p.Key == currentPlayerKey).FirstOrDefault();
                //enable for solo player
                if(PartyList.Length <= 0) {
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
                        string partyMemberWorld = p.World.GameData!.Name.ToString();
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
            } finally {
                _saveLock.Release();
            }
            Save();
        }

        public void BuildRecentPartyList() {
            try {
                _saveLock.Wait();
                Log.Verbose("Rebuilding recent party list.");
                RecentPartyList = new();
                var allPlayers = StorageManager.GetPlayers();
                var currentMaps = StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted).ToList();
                foreach(var player in allPlayers.Query().ToList()) {
                    TimeSpan timeSpan = DateTime.Now - player.LastJoined;
                    bool isRecent = timeSpan.TotalHours <= Configuration.ArchiveThresholdHours;
                    bool hasMaps = currentMaps.Where(m => !m.Owner.IsNullOrEmpty() && m.Owner.Equals(player.Key)).Any();
                    bool notCurrent = !CurrentPartyList.ContainsKey(player.Key);
                    bool notSelf = !player.IsSelf;
                    if(isRecent && hasMaps && notCurrent) {
                        RecentPartyList.Add(player.Key, player);
                    }
                }
            } finally {
                _saveLock.Release();
            }
            Save();
        }

        public void OpenMapLink(MapLinkPayload mapLink) {
            GameGui.OpenMapWithMapLink(mapLink);
        }

        public int GetCurrentTerritoryId() {
            return ClientState.TerritoryType;
            //return DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name;
        }

        public string GetCurrentPlayer() {
            string currentPlayerName = ClientState.LocalPlayer?.Name.ToString()!;
            string currentPlayerWorld = ClientState.LocalPlayer?.HomeWorld.GameData?.Name!;
            if(currentPlayerName == null || currentPlayerWorld == null) {
                //throw exception?
                //throw new InvalidOperationException("Cannot retrieve current player");
                return "";
            }

            return $"{currentPlayerName} {currentPlayerWorld}";
        }

        public bool IsEnglishClient() {
            return ClientState.ClientLanguage == Dalamud.ClientLanguage.English;
        }

        public Task Save() {
            Configuration.Save();
            //performance reasons...
            return Task.Run(() => {
                try {
                    _saveLock.Wait();
                    Task statsWindowTask = StatsWindow.Refresh();
                    Task mainWindowTask = MainWindow.Refresh();
                    Task dutyResultsWindowTask = DutyResultsWindow.Refresh();
                    Task.WaitAll(new[] { statsWindowTask, mainWindowTask, dutyResultsWindowTask });
                } finally {
                    _saveLock.Release();
                }
            });
        }
    }
}
