using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using LiteDB;
using Lumina.Excel.Sheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static Dalamud.Interface.Windowing.Window;

namespace MapPartyAssist.Windows.Summary {
    internal class MapListView {

        private const int _maxPageSize = 100;

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private List<MPAMap> _maps = new();
        private List<MPAMap> _mapsPage = new();
        private Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> _lootResults = new();
        private Dictionary<ObjectId, int> _totalGilValue = new();
        private int _portalCount;

        private int _currentPage = 0;
        private bool _collapseAll = false;
        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public string CSV { get; private set; } = "";

        internal MapListView(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
        }

        public void Refresh(List<MPAMap> maps) {
            Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> lootResults = new();
            Dictionary<ObjectId, int> totalGilValue = new();
            _portalCount = 0;
            //calculate loot results (this is largely duplicated from lootsummary)
            List<string> selfPlayers = new();
            _plugin.StorageManager.GetPlayers().Query().Where(p => p.IsSelf).ToList().ForEach(p => {
                selfPlayers.Add(p.Key);
            });

            foreach(var m in maps) {
                if(m.IsPortal) {
                    _portalCount++;
                }
                if(m.LootResults == null) {
                    continue;
                }
                Dictionary<LootResultKey, LootResultValue> newLootResults = new();
                int newTotalGil = 0;
                foreach(var lootResult in m.LootResults.Where(x => x.Recipient != null)) {
                    var key = new LootResultKey { ItemId = lootResult.ItemId, IsHQ = lootResult.IsHQ };
                    bool selfObtained = lootResult.Recipient is not null && selfPlayers.Contains(lootResult.Recipient);
                    var price = _plugin.PriceHistory.CheckPrice(key);
                    int obtainedQuantity = selfObtained ? lootResult.Quantity : 0;
                    if(newLootResults.ContainsKey(key)) {
                        newLootResults[key].ObtainedQuantity += obtainedQuantity;
                        newLootResults[key].DroppedQuantity += lootResult.Quantity;
                    } else {
                        var row = _plugin.DataManager.GetExcelSheet<Item>()?.GetRow(lootResult.ItemId);
                        if(row is not null) {
                            newLootResults.Add(key, new LootResultValue {
                                DroppedQuantity = lootResult.Quantity,
                                ObtainedQuantity = obtainedQuantity,
                                Rarity = row.Value.Rarity,
                                ItemName = row.Value.Name.ToString(),
                                Category = row.Value.ItemUICategory.Value.Name.ToString(),
                                AveragePrice = price,
                                DroppedValue = price * lootResult.Quantity,
                                ObtainedValue = price * obtainedQuantity,
                            });
                        }
                    }
                    //multiply gil by total partymembers
                    if(lootResult.ItemId == 1) {
                        newTotalGil += m.Players?.Length * lootResult.Quantity ?? lootResult.Quantity;
                    } else {
                        newTotalGil += price * lootResult.Quantity ?? 0;
                    }
                }
                newLootResults = newLootResults.OrderByDescending(lr => lr.Value.DroppedQuantity).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                lootResults.Add(m.Id, newLootResults);
                totalGilValue.Add(m.Id, newTotalGil);
            }
            try {
                _refreshLock.Wait();
                _maps = maps;
                _lootResults = lootResults;
                _totalGilValue = totalGilValue;
            } finally {
                _refreshLock.Release();
            }
            if(_currentPage * _maxPageSize > _maps.Count) {
                _currentPage = 0;
            }
            GoToPage();
        }

        private void GoToPage(int? page = null) {
            //null = stay on same page
            page ??= _currentPage;
            _currentPage = (int)page;
            _mapsPage = _maps.OrderByDescending(m => m.Time).Skip(_currentPage * _maxPageSize).Take(_maxPageSize).ToList();
            CSV = "";
        }

        public void Draw() {
            if(!_refreshLock.Wait(0)) {
                return;
            }
            try {
                _statsWindow.SizeConstraints = new WindowSizeConstraints {
                    MinimumSize = new Vector2(400, _statsWindow.SizeConstraints!.Value.MinimumSize.Y),
                    MaximumSize = _statsWindow.SizeConstraints!.Value.MaximumSize,
                };
                if(_plugin.AllowEdit) {
                    try {
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    } finally {
                        ImGui.PopFont();
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"EDIT MODE ENABLED");
                    ImGui.SameLine();
                    try {
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    } finally {
                        ImGui.PopFont();
                    }
                    if(ImGui.Button("Save")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            _plugin.StorageManager.UpdateMaps(_mapsPage.Where(m => m.IsEdited));
                            //_plugin.Save();
                        });
                    }

                    ImGui.SameLine();
                    if(ImGui.Button("Cancel")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            _plugin.AllowEdit = false;
                            _statsWindow.Refresh();
                        });
                    }
                }

                //ImGui.SameLine();
                if(ImGui.Button("Collapse All")) {
                    _collapseAll = true;
                }

                ImGui.Text($"Total maps: {_maps.Count} Total portals: {_portalCount}");

                using(var child = ImRaii.Child("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true)) {
                    if(child) {
                        foreach(var map in _mapsPage) {
                            if(_collapseAll) {
                                ImGui.SetNextItemOpen(false);
                            }

                            float targetWidth1 = 150f * ImGuiHelpers.GlobalScale;
                            float targetWidth2 = 200f * ImGuiHelpers.GlobalScale;
                            float targetWidth3 = 215f * ImGuiHelpers.GlobalScale;
                            var text1 = map.Time.ToString();
                            var text2 = map.Zone;
                            _plugin.DutyManager.Duties.TryGetValue(map.DutyId ?? 0, out var duty);
                            var text3 = duty?.GetDisplayName() ?? map.DutyName ?? "";
                            while(ImGui.CalcTextSize(text1).X < targetWidth1) {
                                text1 += " ";
                            }
                            while(ImGui.CalcTextSize(text2).X < targetWidth2) {
                                text2 += " ";
                            }
                            while(ImGui.CalcTextSize(text3).X < targetWidth3) {
                                text3 += " ";
                            }

                            if(ImGui.CollapsingHeader(string.Format("{0} {1} {2} {3}", text1, text2, text3, map.Id.ToString()))) {

                                if(_plugin.AllowEdit) {
                                    DrawMapEditable(map);
                                } else {
                                    DrawMap(map);
                                }
                            }
                        }
                    }
                }

                ImGui.Text("");
                ImGui.SameLine();

                if(_currentPage > 0) {
                    ImGui.SameLine();
                    if(ImGui.Button($"Previous {_maxPageSize}")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            GoToPage(_currentPage - 1);
                        });
                    }
                }

                if(_mapsPage.Count >= _maxPageSize) {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 65f * ImGuiHelpers.GlobalScale);
                    if(ImGui.Button($"Next {_maxPageSize}")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            GoToPage(_currentPage + 1);
                        });
                    }
                }
                _collapseAll = false;
            } finally {
                _refreshLock.Release();
            }
        }

        private void DrawMap(MPAMap map) {
            using(var table = ImRaii.Table($"##{map.Id}--MainTable", 2, ImGuiTableFlags.NoClip)) {
                if(table) {
                    ImGui.TableNextColumn();
                    using(var subtable1 = ImRaii.Table($"##{map.Id}--SubTable1", 2, ImGuiTableFlags.NoClip)) {
                        ImGui.TableSetupColumn("propName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 95f);
                        ImGui.TableSetupColumn("propVal", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Deleted: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(map.IsDeleted ? "Yes" : "No")}");

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Archived: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(map.IsArchived ? "Yes" : "No")}");

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Method: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{(map.IsManual ? "Manual" : "Auto")}");

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Owner: ");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{map.Owner}");

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Type: ");
                        ImGui.TableNextColumn();
                        if(map.MapType != null) {
                            ImGui.Text($"{MapHelper.GetMapName((TreasureMap)map.MapType)}");
                        }

                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Portal: ");
                        ImGui.TableNextColumn();
                        _plugin.DutyManager.Duties.TryGetValue(map.DutyId ?? 0, out var duty);
                        string portalString = map.IsPortal ? duty?.GetDisplayName() ?? map.DutyName ?? "???" : "No";

                        ImGui.Text($"{portalString}");
                    }
                    ImGui.TableNextColumn();
                    using(var subtable2 = ImRaii.Table($"##{map.Id}--SubTable2", 2)) {
                        ImGui.TableSetupColumn("propName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 95f);
                        ImGui.TableSetupColumn("propVal", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableNextColumn();
                        if(map.Players != null) {
                            ImGui.TextColored(ImGuiColors.DalamudGrey, "Party Members: ");
                            ImGui.TableNextColumn();
                            foreach(var partyMember in map.Players) {
                                ImGui.Text($"{partyMember}");
                            }
                        }
                    }
                }
            }

            if(map.LootResults != null) {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Loot: ");
                if(_totalGilValue.ContainsKey(map.Id)) {
                    ImGui.SameLine();
                    string text = $"Total Gil Value: {_totalGilValue[map.Id].ToString("N0")} (?)";
                    ImGuiHelper.RightAlignCursor(text);
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Total Gil Value: ");
                    ImGui.SameLine();
                    ImGui.Text($"{_totalGilValue[map.Id].ToString("N0")}");
                    ImGui.SameLine();
                    ImGuiHelper.HelpMarker("Total market value of all drops plus gil multiplied by number of players.");
                }

                using(var table = ImRaii.Table($"loottable", 4, ImGuiTableFlags.None)) {
                    if(table) {
                        //ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f);
                        ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200f);
                        ImGui.TableSetupColumn("Dropped", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 65f);
                        ImGui.TableSetupColumn("Obtained", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 70f);
                        ImGui.TableNextColumn();
                        ImGui.Text("Quality");
                        ImGui.TableNextColumn();
                        ImGui.Text("Name");
                        ImGui.TableNextColumn();
                        ImGui.Text("Dropped");
                        ImGui.TableNextColumn();
                        ImGui.Text("Obtained");

                        foreach(var lootResult in _lootResults[map.Id]) {
                            //ImGui.TableNextColumn();
                            //ImGui.Text($"{lootResult.Value.Category}");
                            ImGui.TableNextColumn();
                            var qualityText = lootResult.Key.IsHQ ? "HQ" : "";
                            ImGuiHelper.CenterAlignCursor(qualityText);
                            ImGui.Text($"{qualityText}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.ItemName}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.DroppedQuantity.ToString("N0")}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.ObtainedQuantity.ToString("N0")}");
                        }
                    }
                }
            }
        }

        private void DrawMapEditable(MPAMap map) {
            bool isDeleted = map.IsDeleted;
            bool isArchived = map.IsArchived;
            string owner = map.Owner ?? "";
            //MapType type = map.MapType;

            if(ImGui.Checkbox($"Deleted##{map.Id}--Deleted", ref isDeleted)) {
                map.IsEdited = true;
                map.IsDeleted = isDeleted;
            }

            if(ImGui.Checkbox($"Archived##{map.Id}--Archived", ref isArchived)) {
                map.IsEdited = true;
                map.IsArchived = isArchived;
            }

            if(ImGui.InputText($"Owner##{map.Id}--Owner", ref owner, 50, ImGuiInputTextFlags.AutoSelectAll)) {
                map.IsEdited = true;
                map.Owner = owner;
            }
        }
    }
}
