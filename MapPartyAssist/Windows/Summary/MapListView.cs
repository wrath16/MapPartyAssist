using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;
using LiteDB;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static MapPartyAssist.Windows.Summary.LootSummary;

namespace MapPartyAssist.Windows.Summary {
    internal class MapListView {

        private const int _maxPageSize = 100;

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private List<MPAMap> _maps = new();
        private List<MPAMap> _mapsPage = new();
        private Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> _lootResults = new();
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
                foreach(var lootResult in m.LootResults) {
                    var key = new LootResultKey { ItemId = lootResult.ItemId, IsHQ = lootResult.IsHQ };
                    bool selfObtained = lootResult.Recipient is not null && selfPlayers.Contains(lootResult.Recipient);
                    int obtainedQuantity = selfObtained ? lootResult.Quantity : 0;
                    if(newLootResults.ContainsKey(key)) {
                        newLootResults[key].ObtainedQuantity += obtainedQuantity;
                        newLootResults[key].DroppedQuantity += lootResult.Quantity;
                    } else {
                        var row = _plugin.DataManager.GetExcelSheet<Item>().GetRow(lootResult.ItemId);
                        newLootResults.Add(key, new LootResultValue {
                            DroppedQuantity = lootResult.Quantity,
                            ObtainedQuantity = obtainedQuantity,
                            Rarity = row.Rarity,
                            ItemName = row.Name,
                            Category = row.ItemUICategory.Value.Name,
                        });
                    }
                }
                newLootResults = newLootResults.OrderByDescending(lr => lr.Value.DroppedQuantity).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                lootResults.Add(m.Id, newLootResults);
            }
            try {
                _refreshLock.Wait();
                _maps = maps;
                _lootResults = lootResults;
            } finally {
                _refreshLock.Release();
            }
            GoToPage();
        }

        private void GoToPage(int? page = null) {
            //null = stay on same page
            page ??= _currentPage;
            _currentPage = (int)page;
            _mapsPage = _maps.OrderByDescending(m => m.Time).Skip(_currentPage * _maxPageSize).Take(_maxPageSize).ToList();
            CSV = "";
            //foreach(var dutyResult in _dutyResultsPage.OrderBy(dr => dr.Time)) {
            //    //no checks
            //    float checkpoint = dutyResult.CheckpointResults.Count / 2f;
            //    if(_plugin.DutyManager.Duties[dutyResult.DutyId].Structure == DutyStructure.Doors) {
            //        checkpoint += 0.5f;
            //    }
            //    CSV = CSV + checkpoint.ToString() + ",";
            //}
        }

        public void Draw() {
            if(!_refreshLock.Wait(0)) {
                return;
            }

            try {
                if(_plugin.AllowEdit) {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    ImGui.PopFont();
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"EDIT MODE ENABLED");
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    ImGui.PopFont();
                }

                if(_plugin.AllowEdit && ImGui.Button("Save")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        _plugin.StorageManager.UpdateMaps(_mapsPage.Where(m => m.IsEdited));
                        //_plugin.Save();
                    });
                }

                //ImGui.SameLine();
                //if(ImGui.Button("Copy CSV")) {
                //    Task.Run(() => {
                //        ImGui.SetClipboardText(CSV);
                //    });
                //}
                //if(ImGui.IsItemHovered()) {
                //    ImGui.BeginTooltip();
                //    ImGui.Text($"Creates a sequential comma-separated list of the last checkpoint reached to the clipboard.");
                //    ImGui.EndTooltip();
                //}

                ImGui.SameLine();
                if(ImGui.Button("Collapse All")) {
                    _collapseAll = true;
                }

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
                    if(ImGui.Button($"Next {_maxPageSize}")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            GoToPage(_currentPage + 1);
                        });
                    }
                }

                ImGui.Text($"Total maps: {_maps.Count} Total portals: {_portalCount}");

                ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);
                foreach(var map in _mapsPage) {
                    if(_collapseAll) {
                        ImGui.SetNextItemOpen(false);
                    }

                    float targetWidth1 = 150f * ImGuiHelpers.GlobalScale;
                    float targetWidth2 = 200f * ImGuiHelpers.GlobalScale;
                    float targetWidth3 = 215f * ImGuiHelpers.GlobalScale;
                    var text1 = map.Time.ToString();
                    var text2 = map.Zone;
                    var text3 = map.DutyName ?? "";
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
                ImGui.EndChild();
                _collapseAll = false;
            } finally {
                _refreshLock.Release();
            }
        }

        private void DrawMap(MPAMap map) {
            if(ImGui.BeginTable($"##{map.Id}--MainTable", 2, ImGuiTableFlags.NoClip)) {
                ImGui.TableNextColumn();
                if(ImGui.BeginTable($"##{map.Id}--SubTable1", 2)) {
                    ImGui.TableSetupColumn("propName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 95f);
                    ImGui.TableSetupColumn("propVal", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Deleted: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{map.IsDeleted}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Archived: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{map.IsArchived}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Manual: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{map.IsManual}");

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
                    string portalString = map.IsPortal ? map.DutyName! : "No";
                    ImGui.Text($"{portalString}");

                    ImGui.EndTable();
                }
                ImGui.TableNextColumn();
                if(ImGui.BeginTable($"##{map.Id}--SubTable2", 2)) {
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
                    ImGui.EndTable();
                }
                ImGui.EndTable();
            }

            if(map.LootResults != null) {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Loot: ");
                ImGui.BeginTable($"loottable", 4, ImGuiTableFlags.None);
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
                    ImGui.Text($"{lootResult.Value.DroppedQuantity}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{lootResult.Value.ObtainedQuantity}");
                }
                ImGui.EndTable();
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
