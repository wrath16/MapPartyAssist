#pragma warning disable
#if DEBUG
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MapPartyAssist.Types;
using MapPartyAssist.Types.REST.Universalis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MapPartyAssist.Services;

namespace MapPartyAssist.Windows {
    //for testing purposes only

    internal class TestFunctionWindow : Window {

        private Plugin _plugin;
        private readonly string[] _languageCombo = { "Japanese", "English", "German", "French" };
        private int _originLanguage = (int)ClientLanguage.English;
        private int _destinationLanguage = (int)ClientLanguage.English;

        private readonly Type[] _dataTableCombo = [];
        private readonly string[] _dataTableDisplayCombo = [];
        private Type _selectedDataType => _dataTableCombo != null ? _dataTableCombo[_selectedDataTypeIndex] : null;
        private int _selectedDataTypeIndex;

        private PropertyInfo[] _propertyInfoCombo;
        private string[] _propertyInfoDisplayCombo;
        private PropertyInfo? _selectedProperty => _propertyInfoCombo != null ? _propertyInfoCombo[_selectedPropertyIndex] : null;
        private int _selectedPropertyIndex;

        private string _toTranslateText = "";
        private string _translateResult = "";

        private string _itemIds = "30266,25187,19981";

        public TestFunctionWindow(Plugin plugin) : base("MPA Test Functions") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(1000, 500)
            };
            this._plugin = plugin;

            //populate data table combo
            //foreach(var t in ExcelRow.)
            //var dataTableTypes = from t in Assembly.GetExecutingAssembly().GetTypes()
            //                     where t.IsSubclassOf(typeof(ExcelRow))
            //                     select t;


            //todo: fix this
            //_dataTableCombo = Assembly.GetAssembly(typeof(BNpcName)).GetTypes().Where(t => t.IsAssignableTo(typeof(IExcelRow))).ToArray();
            //_dataTableDisplayCombo = new string[_dataTableCombo.Length];
            //for(int i = 0; i < _dataTableCombo.Length; i++) {
            //    _dataTableDisplayCombo[i] = _dataTableCombo[i].Name;
            //}

            //_selectedDataTypeIndex = 0;
            ////_selectedDataType = _dataTableCombo[_selectedDataTypeIndex];
            //UpdatePropertyInfoCombo();
        }

        public override void Draw() {

            if(ImGui.BeginTabBar("TabBar", ImGuiTabBarFlags.None)) {
                if(ImGui.BeginTabItem("Misc.")) {
                    if(ImGui.Button("Show all Duties")) {
                        foreach(var duty in _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()) {
                            _plugin.Log.Debug($"id: {duty.RowId} name: {duty.Name}");
                        }
                    }

                    if(ImGui.Button("Show all Zones")) {
                        foreach(var zone in _plugin.DataManager.GetExcelSheet<TerritoryType>()) {
                            _plugin.Log.Debug($"id: {zone.RowId} name: {zone.PlaceName.Value.Name}");
                        }
                    }

                    //if(ImGui.Button("Show all Treasure Spots")) {
                    //    var row = _plugin.DataManager.GetExcelSheet<Addon>();
                    //    var x = _plugin.DataManager.GetExcelSheet<TreasureSpot>();
                    //    foreach(var spot in _plugin.DataManager.GetExcelSheet<TreasureSpot>()) {
                    //        _plugin.Log.Debug($"id: {spot.RowId}.{spot.SubrowId} zone: {spot.Location.Value.Territory.Value.PlaceName.Value.Name} x: {spot.Location.Value.X} y: {spot.Location.Value.Y} z: {spot.Location.Value.Z}");
                    //    }
                    //}

                    if(ImGui.Button("Maps Table")) {
                        ShowMapsTable();
                    }

                    if(ImGui.Button("Player Table")) {
                        ShowPlayerTable();
                    }

                    if(ImGui.Button("DR Table")) {
                        ShowDRTable();
                    }

                    if(ImGui.Button("Import Table")) {
                        ShowImportTable();
                    }

                    if(ImGui.Button("Drop Price Table")) {
                        var prices = _plugin.StorageManager.GetPrices().DeleteAll();
                        _plugin.Log.Debug($"Dropped {prices} records");
                    }

                    if(ImGui.Button("Price Table")) {
                        var prices = _plugin.StorageManager.GetPrices().Query().ToList();
                        foreach(var price in prices) {
                            _plugin.Log.Debug(String.Format("itemID: {0,-8} NQ: {1,-12} HQ: {4,-12} region: {3,-15} lastChecked: {2,-25}", price.ItemId, price.NQPrice, price.LastChecked, price.Region, price.HQPrice));
                        }
                    }

                    if(ImGui.Button("Last 3 DutyResults")) {
                        var dutyResults = _plugin.StorageManager.GetDutyResults().Query().OrderByDescending(dr => dr.Time).Limit(3).ToList();
                        foreach(var results in dutyResults) {
                            PrintDutyResults(results);
                        }
                    }


                    //if(ImGui.Button("Test Last Map Equality")) {
                    //    TestMapEquality();
                    //}

                    //if(ImGui.Button("Test Last Map Contains")) {
                    //    TestMapContains();
                    //}

                    if(ImGui.Button("Check and Archive Maps")) {
                        _plugin.MapManager.CheckAndArchiveMaps();
                    }


                    if(ImGui.Button("Save+Refresh")) {
                        _plugin.Refresh();
                    }

                    if(ImGui.Button("GetCurrentPlayer()")) {
                        _plugin.GameStateManager.GetCurrentPlayer();
                    }


                    //if(ImGui.Button("Import Config")) {
                    //    _plugin.StorageManager.Import();
                    //}


                    //if(ImGui.Button("Drop Import Table")) {
                    //    _plugin.StorageManager.GetDutyResultsImports().DeleteAll();
                    //}

                    if(ImGui.Button("fix record")) {
                        var dr = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.DutyId == 993).ToList();

                        foreach(var duty in dr) {
                            foreach(var cp in duty.CheckpointResults) {
                                cp.LootResults = cp.LootResults.Where(x => x.ItemId != 44133 || x.Quantity != 7).ToList();
                            }
                        }

                        //dr.CheckpointResults = new();
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[0], Summon.Lesser, "secret serpent", false, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[1], null, null, false, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[2], Summon.Greater, "secret porxie", true, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[3], null, null, false, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[4], Summon.Elder, "secret keeper", true, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[5], null, null, false, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[6], Summon.Greater, "greedy pixie", true, true));
                        //dr.CheckpointResults.Add(new RouletteCheckpointResults(Plugin.DutyManager.Duties[745].Checkpoints[7], null, null, false, true));
                        _plugin.StorageManager.UpdateDutyResults(dr);

                        //var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.DutyId == 909).OrderBy(dr => dr.Time).ToList();

                        //for(int i = 0; i < dr.Count; i++) {
                        //    var res = dr[i];
                        //    for(int j = 0; j < dr[i].CheckpointResults.Count; j += 2) {
                        //        dr[i].CheckpointResults.Insert(j + 1, new RouletteCheckpointResults(Plugin.DutyManager.Duties[909].Checkpoints[j + 1], null, null, false, true));
                        //        if(j != 0) {
                        //            dr[i].CheckpointResults[j].Checkpoint = Plugin.DutyManager.Duties[909].Checkpoints[j];
                        //        }
                        //    }
                        //}

                        ////dr.CheckpointResults = new();
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[1], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[2], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[3], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[4], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[5], true));
                        ////dr.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[6], true));
                        //Plugin.StorageManager.UpdateDutyResults(dr);
                    }

                    if(ImGui.Button("Set Map Status")) {
                        _plugin.MapManager.Status = StatusLevel.CAUTION;
                        _plugin.MapManager.StatusMessage = "Multiple map owner candidates found, verify last map ownership.";
                    }

                    if(ImGui.Button("Get Player Current Position")) {
                        Vector2 coords = WorldPosToMapCoords(_plugin.ClientState.LocalPlayer.Position);
                        _plugin.Log.Debug($"X: {_plugin.ClientState.LocalPlayer.Position.X} Y: {_plugin.ClientState.LocalPlayer.Position.Y}");
                        _plugin.Log.Debug($"coordsX: {coords.X} coordsY: {coords.Y}");
                        //Plugin.ClientState.LocalPlayer.Position.
                    }

                    if(ImGui.Button("Get Map Position")) {
                        var map = _plugin.GameStateManager.CurrentPartyList.ElementAt(0).Value.MapLink;
                        _plugin.Log.Debug($"XCoord: {map.GetMapLinkPayload().XCoord}");
                        _plugin.Log.Debug($"YCoord: {map.GetMapLinkPayload().YCoord}");
                        _plugin.Log.Debug($"RawX: {map.RawX}");
                        _plugin.Log.Debug($"RawY: {map.RawY}");
                        //_plugin.Log.Debug($"X: {Plugin.ClientState.LocalPlayer.Position.X} Y: {Plugin.ClientState.LocalPlayer.Position.Y}");
                    }

                    if(ImGui.Button("Distance to Map Link")) {
                        var distance = _plugin.MapManager.GetDistanceToMapLink(_plugin.GameStateManager.CurrentPartyList.ElementAt(0).Value.MapLink);
                        _plugin.Log.Debug($"Distance: {distance}");
                    }

                    if(ImGui.Button("Check closest link player")) {
                        _plugin.Log.Debug($"{_plugin.MapManager.GetPlayerWithClosestMapLink(_plugin.GameStateManager.CurrentPartyList.Values.ToList()).Key} has the closest map link");
                    }

                    if(ImGui.Button("Last Map loot")) {
                        var lastMap = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted).OrderBy(m => m.Time).ToList().Last();
                        if(lastMap != null && lastMap.LootResults != null) {
                            foreach(var lr in lastMap.LootResults) {
                                _plugin.Log.Debug($"{lr.Quantity} {(lr.IsHQ ? "HQ" : "NQ")} {lr.ItemId} {lr.Recipient}");
                            }
                        }
                    }


                    //if(ImGui.Button("Check map and member nullability")) {
                    //    //var map = Plugin.StorageManager.GetMaps().Query().First();
                    //    //_plugin.Log.Debug($"Checking validity of {map.Id}");
                    //    //_plugin.Log.Debug($"Is valid? {map.IsValid()}");

                    //    //var dr = Plugin.StorageManager.GetDutyResults().Query().ToList().Last();

                    //    var dr = _plugin.StorageManager.GetDutyResults().Query().ToList();
                    //    foreach(var x in dr) {
                    //        _plugin.Log.Debug($"Checking validity of {x.Id}....valid? {_plugin.StorageManager.ValidateDataType(x)}");
                    //        //_plugin.Log.Debug($"Is valid? {Plugin.StorageManager.ValidateDataType(dr)}");
                    //    }
                    //}

                    if(ImGui.Button("VALIDATE ALL DUTYRESULTS")) {

                        var dr = _plugin.StorageManager.GetDutyResults().Query().ToList();
                        foreach(var x in dr) {
                            _plugin.StorageManager.ValidateDataType(x, true);
                        }
                        _plugin.StorageManager.UpdateDutyResults(dr);
                    }


                    if(ImGui.Button("check map version")) {

                        var map = _plugin.StorageManager.GetMaps().Query().Where(m => m.DutyName.Equals("the hidden canals of uznair", StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.Time).FirstOrDefault();
                        _plugin.Log.Debug($"map id: {map.Id.ToString()}");
                        _plugin.StorageManager.UpdateMap(map);
                    }

                    if(ImGui.Button("get circle regex")) {
                        _plugin.Log.Debug($"{_plugin.DutyManager.CurrentDuty.CircleShiftsRegex[_plugin.ClientState.ClientLanguage].ToString()}");
                    }

                    if(ImGui.Button("test slots final summon")) {
                        var dutyResults = new DutyResults {
                            DutyId = 1060,
                            DutyName = "Vault Oneiron",
                            Players = _plugin.GameStateManager.CurrentPartyList.Keys.ToArray(),
                            Owner = "Sarah Montcroix Siren",
                        };

                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "The hypnoslot machine envisions a lesser notion! A hexapod appears!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "All enemies have been defeated!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "The hypnoslot machine envisions a greater fancy! A hexapod appears!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "All enemies have been defeated!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "The hypnoslot machine envisions an elder imagining! A hexapod appears!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "All enemies have been defeated!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "Grab 100 shining sacks in 90 seconds!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "You collected 100 shining sacks and receive a gold treasure coffer as your reward!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "The hypnoslot machine envisions its final fantasy! A hexapod appears!"));
                        _plugin.DutyManager.ProcessChatMessage(dutyResults, new Message(DateTime.Now, 2105, "All enemies have been defeated!"));
                        dutyResults.IsComplete = true;
                        _plugin.StorageManager.AddDutyResults(dutyResults);
                    }

                    if(ImGui.Button("Get Wipes")) {
                        var maps = _plugin.StorageManager.GetDutyResults().Query().Where(x => x.DutyId == 276).ToList().Where(x => x.CheckpointResults.Count % 2 == 0);
                        foreach(var map in maps) {
                            _plugin.Log.Debug(map.Time.ToString());
                        }
                    }


                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Translate")) {
                    if(ImGui.Combo($"Origin Language##OriginCombo", ref _originLanguage, _languageCombo, _languageCombo.Length)) {
                    }

                    if(ImGui.Combo($"Destination Language##DestinationCombo", ref _destinationLanguage, _languageCombo, _languageCombo.Length)) {
                    }

                    ImGui.Separator();

                    if(ImGui.Combo($"Data Type##DataTypeCombo", ref _selectedDataTypeIndex, _dataTableDisplayCombo, _dataTableDisplayCombo.Length)) {
                        //_selectedDataType = _dataTableCombo[_selectedDataTypeIndex];

                        UpdatePropertyInfoCombo();
                    }

                    if(ImGui.Combo($"Property##DataTypeCombo", ref _selectedPropertyIndex, _propertyInfoDisplayCombo, _propertyInfoDisplayCombo.Length)) {
                        //_selectedProperty = _propertyInfoCombo[_selectedPropertyIndex];
                    }

                    ImGui.Separator();

                    if(ImGui.InputText("To Translate", ref _toTranslateText, 100)) {

                    }

                    if(ImGui.Button("Translate")) {
                        try {
                            //var method = typeof(Plugin).GetMethod("TranslateDataTableEntry");
                            //var genericMethod = method.MakeGenericMethod(_selectedDataType);
                            //var results = genericMethod.Invoke(_plugin, new object[] { _toTranslateText, _selectedProperty.Name, (ClientLanguage)_destinationLanguage, (ClientLanguage)_originLanguage });
                            var rowId = (uint?)typeof(Plugin).GetMethod("GetRowId").MakeGenericMethod(_selectedDataType)
                            .Invoke(_plugin, new object[] { _toTranslateText, _selectedProperty.Name, GrammarCase.Nominative, (ClientLanguage)_originLanguage });
                            _translateResult = rowId != null ? rowId.ToString() : "";
                            _translateResult += " " + (string)typeof(Plugin).GetMethod("TranslateDataTableEntry").MakeGenericMethod(_selectedDataType)
                            .Invoke(_plugin, new object[] { _toTranslateText, _selectedProperty.Name, GrammarCase.Nominative, (ClientLanguage)_destinationLanguage, (ClientLanguage)_originLanguage });
                        } catch(Exception e) {
                            _translateResult = "Translate error!";
                            while(e.InnerException != null && e.GetType() != typeof(ArgumentException)) {
                                e = e.InnerException;
                            }
                            _translateResult += " " + e.Message;

                            throw;
                        }

                    }
                    ImGui.SetNextItemWidth(32);
                    try {
                        ImGui.PushFont(UiBuilder.IconFont);
                        if(ImGui.Button(FontAwesomeIcon.Copy.ToIconString())) {
                            ImGui.SetClipboardText(_translateResult);
                        }
                    } finally {
                        ImGui.PopFont();
                    }
                    ImGui.SameLine();

                    ImGui.Text("Result: ");
                    ImGui.SameLine();
                    ImGui.Text(_translateResult);

                    ImGui.Separator();
                    //if(ImGui.Button("test")) {
                    //    _plugin.Log.Debug($"{typeof(Plugin).GetMethod(_toTranslateText) != null}");
                    //}
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Settings")) {
                    bool printAllMessages = _plugin.PrintAllMessages;
                    if(ImGui.Checkbox("Print all messages", ref printAllMessages)) {
                        _plugin.PrintAllMessages = printAllMessages;
                        _plugin.Refresh();
                    }
                    bool printPayloads = _plugin.PrintPayloads;
                    if(ImGui.Checkbox("Print payloads", ref printPayloads)) {
                        _plugin.PrintPayloads = printPayloads;
                        _plugin.Refresh();
                    }
                    bool editMode = _plugin.AllowEdit;
                    if(ImGui.Checkbox("Edit Mode", ref editMode)) {
                        _plugin.AllowEdit = editMode;
                        _plugin.Refresh();
                    }
                    if(ImGui.Button("Enable Universalis Price Check")) {
                        _plugin.PriceHistory.EnablePolling();
                    }
                    ImGui.SameLine();
                    if(ImGui.Button("Disable Universalis Price Check")) {
                        _plugin.PriceHistory.DisablePolling();
                    }
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("API")) {
                    if(ImGui.InputText("ItemIDs", ref _itemIds, 100)) {

                    }
                    if(ImGui.Button("Get Price Data")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            string[] stringIDs = _itemIds.Trim().Split(",");
                            List<uint> intIDs = new();
                            foreach(var id in stringIDs) {
                                if(uint.TryParse(id, out uint intID)) {
                                    _plugin.PriceHistory.CheckPrice(new() { ItemId = intID });

                                    //intIDs.Add(intID);
                                    //_plugin.PriceHistory.QueryUniversalisHistory(intIDs.ToArray(), Region.NorthAmerica);
                                    ////var price = _plugin.PriceHistory.CheckPrice(intID, false);
                                    ////if(price != null) {
                                    ////    _plugin.Log.Debug($"itemID: {id} price: {price}");
                                    ////}
                                }
                            }
                            //_plugin.PriceHistory.UpdatePrices(intIDs.ToArray());
                        });
                    }

                    if(ImGui.Button("Test JSON: Sale Entry")) {
                        string sale = "{ \"hq\": true, \"pricePerUnit\": 800,\"quantity\": 2,\"buyerName\": \"Jackie Caravella\",\"onMannequin\": false,\"timestamp\": 1704498382,\"worldName\": \"Seraph\",\"worldID\": 405}";
                        SaleEntry s = JsonConvert.DeserializeObject<SaleEntry>(sale);

                        _plugin.Log.Debug($"price per unit: {s.PricePerUnit}");
                    }

                    if(ImGui.Button("Test JSON: Item History")) {
                        string history = "{\r\n      \"itemID\": 19981,\r\n      \"lastUploadTime\": 1704506463055,\r\n      \"entries\": [\r\n        {\r\n          \"hq\": true,\r\n          \"pricePerUnit\": 274,\r\n          \"quantity\": 1,\r\n          \"buyerName\": \"Johnnie Foreshin\",\r\n          \"onMannequin\": false,\r\n          \"timestamp\": 1704495551,\r\n          \"worldName\": \"Excalibur\",\r\n          \"worldID\": 93\r\n        },\r\n        {\r\n          \"hq\": false,\r\n          \"pricePerUnit\": 391,\r\n          \"quantity\": 1,\r\n          \"buyerName\": \"Rai Borel\",\r\n          \"onMannequin\": false,\r\n          \"timestamp\": 1704494227,\r\n          \"worldName\": \"Cactuar\",\r\n          \"worldID\": 79\r\n        },\r\n        {\r\n          \"hq\": true,\r\n          \"pricePerUnit\": 281,\r\n          \"quantity\": 27,\r\n          \"buyerName\": \"Bazelle Dromeda\",\r\n          \"onMannequin\": false,\r\n          \"timestamp\": 1704485002,\r\n          \"worldName\": \"Famfrit\",\r\n          \"worldID\": 35\r\n        }\r\n      ],\r\n      \"regionName\": \"North-America\",\r\n      \"stackSizeHistogram\": { \"1\": 11, \"2\": 3, \"3\": 2, \"4\": 1, \"27\": 1, \"40\": 1, \"53\": 1 },\r\n      \"stackSizeHistogramNQ\": { \"1\": 7, \"2\": 1, \"3\": 2, \"4\": 1, \"40\": 1, \"53\": 1 },\r\n      \"stackSizeHistogramHQ\": { \"1\": 4, \"2\": 2, \"27\": 1 },\r\n      \"regularSaleVelocity\": 21,\r\n      \"nqSaleVelocity\": 16,\r\n      \"hqSaleVelocity\": 5\r\n}";
                        ItemHistory s = JsonConvert.DeserializeObject<ItemHistory>(history);

                        _plugin.Log.Debug($"2nd world id: {s.Entries[1].WorldID}");
                    }

                    ImGui.EndTabItem();
                }

                using(var tab = ImRaii.TabItem("Map Window")) {
                    var p1Name = "Bozzie Baranta";
                    var p1World = "Tatooine";
                    var p2Name = "Dud Bolt";
                    var p2World = "Vulpter";
                    MPAMember p1 = new MPAMember(p1Name, p1World);
                    MPAMember p2 = new MPAMember(p2Name, p2World);

                    if(ImGui.Button("Create party members in DB")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            _plugin.StorageManager.AddPlayer(p1);
                            _plugin.StorageManager.AddPlayer(p2);
                        });
                    }

                    if(ImGui.Button("Add party members")) {
                        _plugin.GameStateManager.CurrentPartyList.Add($"{p1Name} {p1World}", p1);
                        _plugin.GameStateManager.CurrentPartyList.Add($"{p2Name} {p2World}", p2);
                    }

                    if(ImGui.Button("Add unknown Map")) {
                        _plugin.MapManager.AddMap(null);
                    }

                    ImGui.Text("Test move me!");

                    using var drag = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceAllowNullId);
                    if(drag) {
                        ImGui.Text("Moving!");
                    }
                }
            }
        }

        private void UpdatePropertyInfoCombo() {
            _propertyInfoCombo = _selectedDataType.GetProperties().Where(p => p.PropertyType.IsAssignableTo(typeof(Lumina.Text.SeString))).ToArray();
            _propertyInfoDisplayCombo = new string[_propertyInfoCombo.Length];
            for(int i = 0; i < _propertyInfoCombo.Length; i++) {
                _propertyInfoDisplayCombo[i] = _propertyInfoCombo[i].Name;
            }
            _selectedPropertyIndex = 0;
            //_selectedProperty = _propertyInfoCombo[_selectedPropertyIndex];
        }

        private static Vector2 WorldPosToMapCoords(Vector3 pos) {
            var xInt = (int)(MathF.Round(pos.X, 3, MidpointRounding.AwayFromZero) * 1000);
            var yInt = (int)(MathF.Round(pos.Z, 3, MidpointRounding.AwayFromZero) * 1000);
            return new Vector2((int)(xInt * 0.001f * 1000f), (int)(yInt * 0.001f * 1000f));
        }

        private void ShowMapsTable() {
            var maps = _plugin.StorageManager.GetMaps().Query().ToList();
            foreach(var map in maps) {
                //_plugin.Log.Debug($"owner:{map.Owner} name: {map.Name} date: {map.Time} archived: {map.IsArchived} deleted: {map.IsDeleted}");
                _plugin.Log.Debug(String.Format("Owner: {0,-35} Name: {1,-25} Time: {2,-23} IsPortal: {3,-5} IsArchived: {4,-5} IsDeleted: {5,-5}", map.Owner, map.Name, map.Time, map.IsPortal, map.IsArchived, map.IsDeleted));
            }
        }

        private void ShowPlayerTable() {
            var players = _plugin.StorageManager.GetPlayers().Query().ToList();
            foreach(var player in players) {
                _plugin.Log.Debug(String.Format("{0,-35} isSelf: {1,-8} lastJoined: {2,-10}", player.Key, player.IsSelf, player.LastJoined));
            }
        }

        private void ShowDRTable() {
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().ToList();
            foreach(var results in dutyResults) {
                PrintDutyResults(results);
            }
        }

        private void ShowImportTable() {
            var imports = _plugin.StorageManager.GetDutyResultsImports().Query().ToList();
            foreach(var import in imports) {
                _plugin.Log.Debug(String.Format("Time: {0,-23} DutyId: {1,-4} Runs: {2,-5} Clears: {3,-5} HasGil: {4, -5} HasCheckpoints: {5, -5} HasSequence: {6, -5} HasSummons: {7, -5}", import.Time, import.DutyId, import.TotalRuns, import.TotalClears, import.TotalGil != null, import.CheckpointTotals != null, import.ClearSequence != null, import.SummonTotals != null));
            }
        }

        private void PrintDutyResults(DutyResults dr) {
            _plugin.Log.Debug(String.Format("Time: {0,-23} Owner: {5,-35} Duty: {1,-40} CheckpointCount: {2,-2} isComplete: {3,-5} isPickup: {4, -5} totalGil: {6,-10}", dr.Time, dr.DutyName, dr.CheckpointResults.Count, dr.IsComplete, dr.IsPickup, dr.Owner, dr.TotalGil));
            foreach(var checkpointResults in dr.CheckpointResults) {
                _plugin.Log.Debug(String.Format("Name: {0,-20} isReached: {1,-5} isSaved: {2,-5}, Monster: {3,-20}, Type: {4, -9}", checkpointResults.Checkpoint.Name, checkpointResults.IsReached, checkpointResults.IsSaved, checkpointResults.MonsterName, checkpointResults.SummonType));
            }
        }

        private void TestMapEquality() {
            var map = _plugin.GameStateManager.CurrentPartyList.Values.FirstOrDefault().Maps.Last();
            var storageMap = _plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = map.Equals(storageMap);

            _plugin.Log.Debug($"{sameMap}");
        }

        private void TestMapContains() {
            var maps = new List<MPAMap> {
                _plugin.GameStateManager.CurrentPartyList.Values.FirstOrDefault().Maps.Last()
            };

            var storageMap = _plugin.StorageManager.GetMaps().Query().ToList().Last();

            bool sameMap = maps.Contains(storageMap);

            _plugin.Log.Debug($"{sameMap}");
        }

        private void ShowLastDR() {
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().ToList().Last();
            _plugin.Log.Debug($"{dutyResults.Map.IsArchived}");
        }

        private void FixRecords() {
            ////fix ned
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner == "Ned Thedestroyer Leviathan" && dr.TotalGil == 45000 && dr.Time.Minute == 22).ToList().First();
            foreach(var cp in dutyResults.CheckpointResults) {
                cp.IsReached = true;
            }
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[1]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[2]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[3]));
            //dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[4]));
            //dutyResults.TotalGil = 45000;
            //dutyResults.IsComplete = true;

            ////fix rurushi #2
            //var dutyResults2 = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner == "Rurushi Stella Malboro" && dr.IsPickup).ToList().First();



            ////fix sayane
            //var map = Plugin.StorageManager.GetMaps().Query().Where(m => m.Owner == "Sayane Miu Midgardsormr" && m.Time.Minute == 45).First();
            //var dutyResults3 = new DutyResults(276, "the hidden canals of uznair", Plugin.CurrentPartyList, "Sayane Miu Midgardsormr");
            ////dutyResults3.Time = map.Time;
            //dutyResults3.TotalGil = dutyResults2.TotalGil;
            //dutyResults3.CheckpointResults = dutyResults2.CheckpointResults;

            //dutyResults2.CheckpointResults = new();
            //dutyResults2.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[276].Checkpoints[0]));
            //dutyResults2.TotalGil = 20000;

            _plugin.StorageManager.UpdateDutyResults(dutyResults);
            //Plugin.StorageManager.UpdateDutyResults(dutyResults2);
            //Plugin.StorageManager.AddDutyResults(dutyResults3);


            //var dr = Plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.Owner =="Sayane Miu Midgardsormr").ToList().Last();
            //dr.Map = map;
            //Plugin.StorageManager.UpdateDutyResults(dr);

        }

        private void testRef(ref SeString message) {
            SeString messageUnref = message;
            StringBuilder sb = new();

            var x = message.ToString();
            Task.Delay(1000).ContinueWith(t => {
                _plugin.Log.Debug($"unref message: {messageUnref.ToString()}");
                _plugin.Log.Debug($"unref message string: {x}");
            });
        }
    }
}
#endif
#pragma warning restore
