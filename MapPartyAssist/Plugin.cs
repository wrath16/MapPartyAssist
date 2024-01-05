using Dalamud;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Services;
using MapPartyAssist.Settings;
using MapPartyAssist.Windows;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist {

    public enum StatusLevel {
        OK,
        CAUTION,
        ERROR
    }

    public enum GrammarCase {
        Nominative,
        Accusative,
        Dative,
        Genitive
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
        private const string EditCommandName = "/mpartyedit";

        //Dalamud services
        internal DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        internal IDataManager DataManager { get; init; }
        internal IClientState ClientState { get; init; }
        internal ICondition Condition { get; init; }
        internal IDutyState DutyState { get; init; }
        internal IPartyList PartyList { get; init; }
        internal IChatGui ChatGui { get; init; }
        internal IGameGui GameGui { get; init; }
        internal IFramework Framework { get; init; }
        internal IAddonLifecycle AddonLifecycle { get; init; }
        internal IPluginLog Log { get; init; }

        //Custom services
        internal GameStateManager GameStateManager { get; init; }
        internal DutyManager DutyManager { get; init; }
        internal MapManager MapManager { get; init; }
        internal StorageManager StorageManager { get; init; }
        internal ImportManager ImportManager { get; init; }
        internal DataQueueService DataQueue { get; init; }

        public Configuration Configuration { get; init; }
        internal GameFunctions Functions { get; init; }

        //UI
        internal WindowSystem WindowSystem = new("Map Party Assist");
        internal MainWindow MainWindow;
        internal StatsWindow StatsWindow;
        internal ConfigWindow ConfigWindow;
#if DEBUG
        internal TestFunctionWindow TestFunctionWindow;
#endif
        //non-persistent configuration options
        internal bool PrintAllMessages { get; set; } = false;
        internal bool PrintPayloads { get; set; } = false;
        internal bool AllowEdit { get; set; } = false;

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
            [RequiredVersion("1.0")] IAddonLifecycle addonLifecycle,
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
                AddonLifecycle = addonLifecycle;
                Log = log;

                AtkNodeHelper.Log = Log;

                Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

                Log.Information($"Client language: {ClientState.ClientLanguage}");
                Log.Verbose($"Current culture: {CultureInfo.CurrentCulture.Name}");
                if(!IsLanguageSupported()) {
                    Log.Warning("Client language unsupported, most functions will be unavailable.");
                }

                DataQueue = new(this);
                StorageManager = new(this, $"{PluginInterface.GetPluginConfigDirectory()}\\{DatabaseName}");
                Functions = new();
                DutyManager = new(this);
                MapManager = new(this);
                ImportManager = new(this);

                //needs DutyManager to be initialized first
                Configuration.Initialize(this);
                GameStateManager = new(this);

                MainWindow = new MainWindow(this);
                WindowSystem.AddWindow(MainWindow);
                CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
                    HelpMessage = "Opens map tracker console."
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

#if DEBUG
                TestFunctionWindow = new TestFunctionWindow(this);
                WindowSystem.AddWindow(TestFunctionWindow);
                CommandManager.AddHandler(TestCommandName, new CommandInfo(OnTestCommand) {
                    HelpMessage = "Opens test functions window. (Debug)"
                });
#endif

                CommandManager.AddHandler(EditCommandName, new CommandInfo(OnEditCommand) {
                    HelpMessage = "Toggle editing of maps/duty results."
                });


                PluginInterface.UiBuilder.Draw += DrawUI;
                PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

                ChatGui.ChatMessage += OnChatMessage;

                //import data from old configuration to database
                if(Configuration.Version < 2) {
                    StorageManager.Import();
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
            CommandManager.RemoveHandler(EditCommandName);

#if DEBUG
            CommandManager.RemoveHandler(TestCommandName);
#endif

            ChatGui.ChatMessage -= OnChatMessage;

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            if(MapManager != null) {
                MapManager.Dispose();
            }
            if(DutyManager != null) {
                DutyManager.Dispose();
            }
            if(StorageManager != null) {
                StorageManager.Dispose();
            }
            if(GameStateManager != null) {
                GameStateManager.Dispose();
            }
            if(DataQueue != null) {
                DataQueue.Dispose();
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

#if DEBUG
        private void OnTestCommand(string command, string args) {
            TestFunctionWindow.IsOpen = true;
        }
#endif

        private void OnEditCommand(string command, string args) {
            AllowEdit = !AllowEdit;
            ChatGui.Print($"Map Party Assist Edit Mode: {(AllowEdit ? "ON" : "OFF")}");
        }

        private void DrawUI() {
            WindowSystem.Draw();
        }

        private void DrawConfigUI() {
            ConfigWindow.IsOpen = true;
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

        public void OpenMapLink(MapLinkPayload mapLink) {
            GameGui.OpenMapWithMapLink(mapLink);
        }

        public void Save() {
            Configuration.Save();
            StatsWindow.Refresh();
            MainWindow.Refresh();
        }

        public bool IsLanguageSupported(ClientLanguage? language = null) {
            language ??= ClientState.ClientLanguage;
            return SupportedLanguages.Contains((ClientLanguage)language);
        }

        public string TranslateBNpcName(string npcName, ClientLanguage destinationLanguage, ClientLanguage? originLanguage = null) {
            return TranslateDataTableEntry<BNpcName>(npcName, "Singular", GrammarCase.Nominative, destinationLanguage, originLanguage);
        }

        public string TranslateDataTableEntry<T>(string data, string column, GrammarCase gramCase, ClientLanguage destinationLanguage, ClientLanguage? originLanguage = null) where T : ExcelRow {
            originLanguage ??= ClientState.ClientLanguage;
            uint? rowId = null;
            Type type = typeof(T);
            bool isPlural = column.Equals("Plural", StringComparison.OrdinalIgnoreCase);

            if(!IsLanguageSupported(destinationLanguage) || !IsLanguageSupported(originLanguage)) {
                throw new ArgumentException("Cannot translate to/from an unsupported client language.");
            }

            //check to make sure column is string
            var columnProperty = type.GetProperty(column) ?? throw new ArgumentException($"No property of name: {column} on type {type.FullName}");
            if(!columnProperty.PropertyType.IsAssignableTo(typeof(Lumina.Text.SeString))) {
                throw new ArgumentException($"property {column} of type {columnProperty.PropertyType.FullName} on type {type.FullName} is not assignable to a SeString!");
            }

            //iterate over table to find rowId
            foreach(var row in DataManager.GetExcelSheet<T>((ClientLanguage)originLanguage)!) {
                var rowData = columnProperty!.GetValue(row)?.ToString();

                //German declension placeholder replacement
                if(originLanguage == ClientLanguage.German && rowData != null) {
                    var pronounProperty = type.GetProperty("Pronoun");
                    if(pronounProperty != null) {
                        int pronoun = Convert.ToInt32(pronounProperty.GetValue(row))!;
                        rowData = ReplaceGermanDeclensionPlaceholders(rowData, pronoun, isPlural, gramCase);
                    }
                }
                if(data.Equals(rowData, StringComparison.OrdinalIgnoreCase)) {
                    rowId = row.RowId; break;
                }
            }

            rowId = rowId ?? throw new ArgumentException($"'{data}' not found in table: {type.Name} for language: {originLanguage}.");

            //get data from destinationLanguage
            var translatedRow = DataManager.GetExcelSheet<T>(destinationLanguage)!.Where(r => r.RowId == rowId).FirstOrDefault();
            string? translatedString = columnProperty!.GetValue(translatedRow)?.ToString() ?? throw new ArgumentException($"row id {rowId} not found in table {type.Name} for language: {destinationLanguage}");

            //add German declensions. Assume nominative case
            if(destinationLanguage == ClientLanguage.German) {
                var pronounProperty = type.GetProperty("Pronoun");
                if(pronounProperty != null) {
                    int pronoun = Convert.ToInt32(pronounProperty.GetValue(translatedRow))!;
                    translatedString = ReplaceGermanDeclensionPlaceholders(translatedString, pronoun, isPlural, gramCase);
                }
            }

            return translatedString;
        }

        //male = 0, female = 1, neuter = 2
        private static string ReplaceGermanDeclensionPlaceholders(string input, int gender, bool isPlural, GrammarCase gramCase) {
            if(isPlural) {
                switch(gramCase) {
                    case GrammarCase.Nominative:
                    case GrammarCase.Accusative:
                    default:
                        input = input.Replace("[a]", "e").Replace("[t]", "die");
                        break;
                    case GrammarCase.Dative:
                        input = input.Replace("[a]", "en").Replace("[t]", "den");
                        break;
                    case GrammarCase.Genitive:
                        input = input.Replace("[a]", "er").Replace("[t]", "der");
                        break;
                }
            }
            switch(gender) {
                default:
                case 0: //male
                    switch(gramCase) {
                        case GrammarCase.Nominative:
                        default:
                            input = input.Replace("[a]", "er").Replace("[t]", "der");
                            break;
                        case GrammarCase.Accusative:
                            input = input.Replace("[a]", "en").Replace("[t]", "den");
                            break;
                        case GrammarCase.Dative:
                            input = input.Replace("[a]", "em").Replace("[t]", "dem");
                            break;
                        case GrammarCase.Genitive:
                            input = input.Replace("[a]", "es").Replace("[t]", "des");
                            break;
                    }
                    break;
                case 1: //female
                    switch(gramCase) {
                        case GrammarCase.Nominative:
                        case GrammarCase.Accusative:
                        default:
                            input = input.Replace("[a]", "e").Replace("[t]", "die");
                            break;
                        case GrammarCase.Dative:
                        case GrammarCase.Genitive:
                            input = input.Replace("[a]", "er").Replace("[t]", "der");
                            break;
                    }
                    break;
                case 2: //neuter
                    switch(gramCase) {
                        case GrammarCase.Nominative:
                        case GrammarCase.Accusative:
                        default:
                            input = input.Replace("[a]", "es").Replace("[t]", "das");
                            break;
                        case GrammarCase.Dative:
                            input = input.Replace("[a]", "em").Replace("[t]", "dem");
                            break;
                        case GrammarCase.Genitive:
                            input = input.Replace("[a]", "es").Replace("[t]", "des");
                            break;
                    }
                    break;
            }
            //remove possessive placeholder
            input = input.Replace("[p]", "");
            return input;
        }

        public uint? GetRowId<T>(string data, string column, GrammarCase gramCase, ClientLanguage? language = null) where T : ExcelRow {
            language ??= ClientState.ClientLanguage;
            Type type = typeof(T);
            bool isPlural = column.Equals("Plural", StringComparison.OrdinalIgnoreCase);

            if(!IsLanguageSupported(language)) {
                throw new ArgumentException($"Unsupported language: {language}");
            }

            //check to make sure column is string
            var columnProperty = type.GetProperty(column) ?? throw new ArgumentException($"No property of name: {column} on type {type.FullName}");
            if(!columnProperty.PropertyType.IsAssignableTo(typeof(Lumina.Text.SeString))) {
                throw new ArgumentException($"property {column} of type {columnProperty.PropertyType.FullName} on type {type.FullName} is not assignable to a SeString!");
            }

            //iterate over table to find rowId
            foreach(var row in DataManager.GetExcelSheet<T>((ClientLanguage)language)!) {
                var rowData = columnProperty!.GetValue(row)?.ToString();

                //German declension placeholder replacement
                if(language == ClientLanguage.German && rowData != null) {
                    var pronounProperty = type.GetProperty("Pronoun");
                    if(pronounProperty != null) {
                        int pronoun = Convert.ToInt32(pronounProperty.GetValue(row))!;
                        rowData = ReplaceGermanDeclensionPlaceholders(rowData, pronoun, isPlural, gramCase);
                    }
                }
                if(data.Equals(rowData, StringComparison.OrdinalIgnoreCase)) {
                    return row.RowId;
                }
            }
            return null;
        }
    }
}
