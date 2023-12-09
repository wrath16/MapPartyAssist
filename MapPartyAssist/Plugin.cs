using Dalamud;
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
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Services;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using MapPartyAssist.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public readonly ClientLanguage[] SupportedLanguages = { ClientLanguage.English, ClientLanguage.French, ClientLanguage.German, ClientLanguage.Japanese };

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
        internal bool PrintAllMessages { get; set; } = false;
        internal bool PrintPayloads { get; set; } = false;

        public Dictionary<string, MPAMember> CurrentPartyList { get; private set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; private set; } = new();
        private int _lastPartySize = 0;

        //coordinates all data sequence-sensitive operations
        internal ConcurrentQueue<Task> DataTaskQueue { get; init; } = new();
        internal SemaphoreSlim DataLock { get; init; } = new SemaphoreSlim(1, 1);

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
                if(!IsLanguageSupported()) {
                    Log.Warning("Client language unsupported, most functions will be unavailable.");
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
                    BuildPartyLists();
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
                _lastPartySize = currentPartySize;
                BuildPartyLists();
            }
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            //filter nuisance combat messages...
            switch((int)type) {
                case 2091:  //self actions
                case 4139:  //party member actions
                    if(Regex.IsMatch(message.ToString(), @"(Dig|Excavation|Ausgraben|ディグ)", RegexOptions.IgnoreCase)) {
                        goto case 2105;
                    }
                    goto default;
                case 2233:
                case 2105:  //system messages of some kind
                case 2361:
                case 62:    //self gil
                case 2110:  //self loot obtained
                case 4158:  //party loot obtained
                case 8254:  //alliance loot obtained
                case (int)XivChatType.Say:
                case (int)XivChatType.Party:
                case (int)XivChatType.SystemMessage:
                    //Log.Verbose($"Message received: {type} {message} from {sender}");
                    Log.Verbose(String.Format("type: {0,-6} sender: {1,-20} message: {2}", type, sender, message));
                    if(PrintPayloads) {
                        foreach(Payload payload in message.Payloads) {
                            Log.Verbose($"payload: {payload}");
                        }
                    }
                    break;
                default:
                    if(PrintAllMessages) {
                        goto case 2105;
                    }
                    break;
            }
        }

        private void OnLogin() {
            BuildPartyLists();
            MapManager.CheckAndArchiveMaps();
        }

        private void OnLogout() {
            //remove all party members
            CurrentPartyList = new();
            Save();
        }

        internal void BuildPartyLists() {
            BuildCurrentPartyList();
            BuildRecentPartyList();
        }

        //builds current party list from scratch
        internal void BuildCurrentPartyList() {
            AddDataTask(new(async () => {
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
                        await StorageManager.AddPlayer(newPlayer);
                    } else {
                        currentPlayer.LastJoined = DateTime.Now;
                        CurrentPartyList.Add(currentPlayerKey, currentPlayer);
                        await StorageManager.UpdatePlayer(currentPlayer);
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
                            await StorageManager.AddPlayer(newPlayer);
                        } else {
                            //find existing player
                            findPlayer.LastJoined = DateTime.Now;
                            findPlayer.IsSelf = isCurrentPlayer;
                            CurrentPartyList.Add(key, findPlayer);
                            await StorageManager.UpdatePlayer(findPlayer);
                        }
                    }
                }
                Save();
            }));
        }

        internal void BuildRecentPartyList() {
            AddDataTask(new(() => {
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
                Save();
            }));
        }

        public void OpenMapLink(MapLinkPayload mapLink) {
            GameGui.OpenMapWithMapLink(mapLink);
        }

        public int GetCurrentTerritoryId() {
            return ClientState.TerritoryType;
            //return DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType)?.PlaceName.Value?.Name;
        }

        public string GetCurrentPlayer() {
            string? currentPlayerName = ClientState.LocalPlayer?.Name?.ToString();
            string? currentPlayerWorld = ClientState.LocalPlayer?.HomeWorld?.GameData?.Name?.ToString();
            if(currentPlayerName == null || currentPlayerWorld == null) {
                //throw exception?
                //throw new InvalidOperationException("Cannot retrieve current player");
                return "";
            }

            return $"{currentPlayerName} {currentPlayerWorld}";
        }

        public void Save() {
            AddDataTask(new(() => {
                Configuration.Save();
                StatsWindow.Refresh();
                MainWindow.Refresh();
                DutyResultsWindow.Refresh();
            }));
        }

        internal void AddDataTask(Task task) {
            DataTaskQueue.Enqueue(task);
            RunNextTask();
        }

        private async void RunNextTask() {
            try {
                await DataLock.WaitAsync();
                if(DataTaskQueue.TryDequeue(out Task nextTask)) {
                    nextTask.Start();
                    await nextTask;
                } else {
                    Log.Warning($"Unable to dequeue next task. Tasks remaining: {DataTaskQueue.Count}");
                }
            } finally {
                DataLock.Release();
            }
        }

        public bool IsLanguageSupported(ClientLanguage? language = null) {
            language ??= ClientState.ClientLanguage;
            return SupportedLanguages.Contains((ClientLanguage)language);
        }

        public string TranslateBNpcName(string npcName, ClientLanguage destinationLanguage, ClientLanguage? originLanguage = null) {
            return TranslateDataTableEntry<BNpcName>(npcName, "Singular", destinationLanguage, originLanguage);
        }

        public string TranslateDataTableEntry<T>(string data, string column, ClientLanguage destinationLanguage, ClientLanguage? originLanguage = null) where T : ExcelRow {
            originLanguage ??= ClientState.ClientLanguage;
            uint? rowId = null;
            Type type = typeof(T);
            bool isPlural = column.Equals("Plural", StringComparison.OrdinalIgnoreCase);

            if(!IsLanguageSupported(destinationLanguage) || !IsLanguageSupported(originLanguage)) {
                throw new InvalidOperationException("Cannot translate to/from an unsupported client language.");
            }

            //check to make sure column is string
            var columnProperty = type.GetProperty(column) ?? throw new InvalidOperationException($"No property of name: {column} on type {type.FullName}");
            if(!columnProperty.PropertyType.IsAssignableTo(typeof(Lumina.Text.SeString))) {
                throw new InvalidOperationException($"property {column} of type {columnProperty.PropertyType.FullName} on type {type.FullName} is not assignable to a SeString!");
            }

            //iterate over table to find rowId
            foreach(var row in DataManager.GetExcelSheet<T>((ClientLanguage)originLanguage)!) {
                var rowData = columnProperty!.GetValue(row)?.ToString();

                //German declension placeholder replacement
                if(originLanguage == ClientLanguage.German && rowData != null) {
                    var pronounProperty = type.GetProperty("Pronoun");
                    if(pronounProperty != null) {
                        int pronoun = Convert.ToInt32(pronounProperty.GetValue(row))!;
                        rowData = ReplaceGermanDeclensionPlaceholders(rowData, pronoun, isPlural);
                    }
                }
                if(data.Equals(rowData, StringComparison.OrdinalIgnoreCase)) {
                    rowId = row.RowId; break;
                }
            }

            rowId = rowId ?? throw new InvalidOperationException($"'{data}' not found in table: {type.Name} for language: {originLanguage}.");

            //get data from destinationLanguage
            var translatedRow = DataManager.GetExcelSheet<T>(destinationLanguage)!.Where(r => r.RowId == rowId).FirstOrDefault();
            string? translatedString = columnProperty!.GetValue(translatedRow)?.ToString() ?? throw new InvalidOperationException($"row id {rowId} not found in table {type.Name} for language: {destinationLanguage}");

            //add German declensions. Assume nominative case
            if(destinationLanguage == ClientLanguage.German) {
                var pronounProperty = type.GetProperty("Pronoun");
                if(pronounProperty != null) {
                    int pronoun = Convert.ToInt32(pronounProperty.GetValue(translatedRow))!;
                    translatedString = ReplaceGermanDeclensionPlaceholders(translatedString, pronoun, isPlural);
                }
            }

            return translatedString;
        }

        //assumes nominative case
        //male = 0, female = 1, neuter = 2
        private string ReplaceGermanDeclensionPlaceholders(string input, int gender, bool isPlural) {
            if(isPlural) {
                input = input.Replace("[a]", "e");
            }
            switch(gender) {
                default:
                case 0: //male
                    input = input.Replace("[a]", "er").Replace("[t]", "der");
                    break;
                case 1: //female
                    input = input.Replace("[a]", "e").Replace("[t]", "die");
                    break;
                case 2: //neuter
                    input = input.Replace("[a]", "es").Replace("[t]", "das");
                    break;
            }
            //remove possessive placeholder
            input = input.Replace("[p]", "");
            return input;
        }
    }
}
