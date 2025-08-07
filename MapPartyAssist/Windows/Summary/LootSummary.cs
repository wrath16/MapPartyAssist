using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;

namespace MapPartyAssist.Windows.Summary {
    public class LootSummary {

        enum SortableColumn {
            Name,
            DroppedQuantity,
            ObtainedQuantity,
            IsHQ,
            Category,
            UnitPrice,
            DroppedValue,
            ObtainedValue,
        }

        private const int _maxPageSize = 100;

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private int _lootEligibleRuns = 0;
        private int _lootEligibleMaps = 0;
        private int _totalGilValueObtained = 0;
        private int _totalGilValueDropped = 0;
        private Dictionary<LootResultKey, LootResultValue> _lootResults = new();
        private Dictionary<LootResultKey, LootResultValue> _lootResultsPage = new();
        //private List<LootResultKey> _pins = new();
        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private bool _triggerSort;
        private int _currentPage = 0;
        private bool _includePins = true;
        public string LootCSV { get; private set; } = "";

        internal LootSummary(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
        }

        public void Refresh(List<DutyResults> dutyResults, List<MPAMap> maps) {
            Dictionary<LootResultKey, LootResultValue> newLootResults = new();
            int newLootEligibleRuns = 0;
            int newLootEligibleMaps = 0;
            int newTotalGilValueObtained = 0;
            int newTotalGilValueDropped = 0;
            string newLootCSV = "Category,Quality,Name,Dropped,Obtained,Unit Price\n";

            List<string> selfPlayers = new();
            _plugin.StorageManager.GetPlayers().Query().Where(p => p.IsSelf).ToList().ForEach(p => {
                selfPlayers.Add(p.Key);
            });

            var addLootResult = (LootResult lootResult, int playerCount) => {
                var key = new LootResultKey { ItemId = lootResult.ItemId, IsHQ = lootResult.IsHQ };
                bool selfObtained = lootResult.Recipient is not null && selfPlayers.Contains(lootResult.Recipient);
                var price = _plugin.PriceHistory.CheckPrice(key);
                int obtainedQuantity = selfObtained ? lootResult.Quantity : 0;
                int droppedQuantity = lootResult.ItemId == 1 ? lootResult.Quantity * playerCount : lootResult.Quantity;
                if(newLootResults.ContainsKey(key)) {
                    newLootResults[key].ObtainedQuantity += obtainedQuantity;
                    newLootResults[key].DroppedQuantity += droppedQuantity;
                    newLootResults[key].DroppedValue += droppedQuantity * price;
                    newLootResults[key].ObtainedValue += obtainedQuantity * price;
                } else {
                    var row = _plugin.DataManager.GetExcelSheet<Item>()?.GetRow(lootResult.ItemId);
                    if(row is not null) {
                        newLootResults.Add(key, new LootResultValue {
                            DroppedQuantity = droppedQuantity,
                            ObtainedQuantity = obtainedQuantity,
                            Rarity = row.Value.Rarity,
                            Category = row.Value.ItemUICategory.Value.Name.ToString(),
                            ItemName = row.Value.Name.ToString(),
                            AveragePrice = price,
                            DroppedValue = price * droppedQuantity,
                            ObtainedValue = price * obtainedQuantity,
                        });
                    }
                }
            };

            foreach(var dutyResult in dutyResults) {
                if(dutyResult.HasLootResults()) {
                    newLootEligibleRuns++;
                    foreach(var checkpointResult in dutyResult.CheckpointResults) {
                        if(checkpointResult.LootResults != null) {
                            foreach(var lootResult in checkpointResult.LootResults.Where(x => x.Recipient != null)) {
                                addLootResult(lootResult, dutyResult.Players.Length);
                            }
                        }
                    }
                }
            }

            foreach(var map in maps) {
                if(map.LootResults is null) {
                    continue;
                }
                newLootEligibleMaps++;
                foreach(var lootResult in map.LootResults) {
                    addLootResult(lootResult, map.Players?.Length ?? 1);
                }
            }

            //=set CSV and check for changes
            bool hasChange = newLootResults.Count != _lootResults.Count;
            foreach(var lootResult in newLootResults) {
                bool isPlural = lootResult.Value.DroppedQuantity != 1;
                //var row = _plugin.DataManager.GetExcelSheet<Item>()?.First(r => r.RowId == lootResult.Key.ItemId);
                //lootResult.Value.ItemName = row is null ? "" : (isPlural ? row.Plural : row.Singular);
                //lootResult.Value.ItemName = row is null ? "" : row.Name;
                newLootCSV += $"{lootResult.Value.Category},{(lootResult.Key.IsHQ ? "HQ" : "")},{lootResult.Value.ItemName},{lootResult.Value.DroppedQuantity},{lootResult.Value.ObtainedQuantity},{lootResult.Value.AveragePrice}\n";
                newTotalGilValueObtained += lootResult.Value.ObtainedValue ?? 0;
                newTotalGilValueDropped += lootResult.Value.DroppedValue ?? 0;
                if(!_lootResults.ContainsKey(lootResult.Key)) {
                    hasChange = true;
                } else if(!lootResult.Value.Equals(_lootResults[lootResult.Key])) {
                    hasChange = true;
                }
            }
            if(hasChange) {
#if DEBUG
                _plugin.Log.Verbose($"loot changes detected!");
#endif
                _lootResults = newLootResults;
                LootCSV = newLootCSV;
                _triggerSort = true;
                GoToPage();
            }

            _lootEligibleRuns = newLootEligibleRuns;
            _lootEligibleMaps = newLootEligibleMaps;
            _totalGilValueObtained = newTotalGilValueObtained;
            _totalGilValueDropped = newTotalGilValueDropped;
        }

        private void GoToPage(int? page = null) {
            //null = stay on same page
            page ??= _currentPage;
            if(page * _maxPageSize >= _lootResults.Count) {
                page = 0;
            }
            _currentPage = (int)page;
            _lootResultsPage = _lootResults.Skip(_currentPage * _maxPageSize).Take(_maxPageSize).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public void Draw() {
            _statsWindow.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, _statsWindow.SizeConstraints!.Value.MinimumSize.Y),
                MaximumSize = _statsWindow.SizeConstraints!.Value.MaximumSize,
            };
            if(ImGui.Button("Copy CSV")) {
                Task.Run(() => {
                    ImGui.SetClipboardText(LootCSV);
                });
            }
            ImGui.SameLine();
            //if(ImGui.Button("Unpin All")) {
            //    //only unpin visible?
            //    //_plugin.Configuration.LootPins = new();
            //    //_plugin.Configuration.Save();
            //    //SortByColumn((SortableColumn)sortSpecs.Specs.ColumnUserID, sortSpecs.Specs.SortDirection);
            //}
            ImGui.Text("Use Pins: ");
            ImGui.SameLine();
            if(ImGuiComponents.ToggleButton("##includePins", ref _includePins)) {
                _triggerSort = true;
            }
            ImGui.Text($"Eligible maps: {_lootEligibleMaps} Eligible duties: {_lootEligibleRuns}");
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Loot tracking introduced in version 2.0.0.0. Legacy maps/duties are not counted.");
            ImGui.Text($"Estimated total gil value dropped: {string.Format(_totalGilValueDropped.ToString("N0"))}");
            ImGui.SameLine();
            ImGui.Text($"Estimated total gil value obtained: {string.Format(_totalGilValueObtained.ToString("N0"))}");
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Enable market board pricing in settings window to see pricing data.\n\nRight-click table header to add more columns.");

            using(ImRaii.Child("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), false)) {
                using(var table = ImRaii.Table($"loottable", 8, ImGuiTableFlags.Sortable | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable
                    | ImGuiTableFlags.ScrollY, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) {
                    if(table) {
                        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 55f, (uint)SortableColumn.Category);
                        ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 55f, (uint)SortableColumn.IsHQ);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200f, (uint)SortableColumn.Name);
                        ImGui.TableSetupColumn("Dropped", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 65f, (uint)SortableColumn.DroppedQuantity);
                        ImGui.TableSetupColumn("Obtained", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.ObtainedQuantity);
                        ImGui.TableSetupColumn("Unit Price", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.UnitPrice);
                        ImGui.TableSetupColumn("Dropped Value", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.DroppedValue);
                        ImGui.TableSetupColumn("Obtained Value", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.ObtainedValue);

                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();

                        //column sorting
                        ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
                        if(sortSpecs.SpecsDirty || _triggerSort) {
                            _triggerSort = false;
                            var columnIdDeRef = (SortableColumn)sortSpecs.Specs.ColumnUserID;
                            var sortDirectionDeRef = sortSpecs.Specs.SortDirection;
                            _plugin.DataQueue.QueueDataOperation(() => {
                                SortByColumn(columnIdDeRef, sortDirectionDeRef);
                            });
                            sortSpecs.SpecsDirty = false;
                        }

                        ImGui.TableNextRow();
                        foreach(var lootResult in _lootResultsPage) {
                            bool isPinned = _plugin.Configuration.LootPins.Contains(lootResult.Key);
                            ImGui.TableNextColumn();
                            if(isPinned && _includePins) {
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiColors.DalamudYellow - new Vector4(0f, 0f, 0f, 0.7f)));
                            }
                            ImGui.Text($"{lootResult.Value.Category}");
                            ImGui.TableNextColumn();
                            //ImGui.AlignTextToFramePadding();
                            //ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f);
                            var qualityText = lootResult.Key.IsHQ ? "HQ" : "";
                            ImGuiHelper.CenterAlignCursor(qualityText);
                            ImGui.Text($"{qualityText}");
                            ImGui.TableNextColumn();
                            var textColor = ImGuiColors.DalamudWhite;
                            switch(lootResult.Value.Rarity) {
                                default:
                                    textColor = ImGuiColors.DalamudWhite; break;
                                case 2: //green
                                    textColor = ImGuiColors.HealerGreen; break;
                                case 3: //blue
                                    textColor = ImGuiColors.TankBlue; break;
                                case 4: //purple
                                    textColor = ImGuiColors.ParsedPurple; break;
                                case 7: //pink
                                    textColor = ImGuiColors.ParsedPink; break;
                            }
                            ImGui.TextColored(textColor, $"{lootResult.Value.ItemName.PadRight(20)}");
                            using(var popup = ImRaii.ContextPopupItem($"##{lootResult.Key.ItemId}{lootResult.Key.IsHQ}--ContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                                if(popup) {
                                    if(ImGui.MenuItem($"Pin item##{lootResult.Key.ItemId}{lootResult.Key.IsHQ}", string.Empty, isPinned)) {
                                        if(!isPinned) {
                                            _plugin.Log.Verbose($"pinning: {lootResult.Value.ItemName}");
                                            //_pins.Add(lootResult.Key);
                                            _plugin.Configuration.LootPins.Add(lootResult.Key);
                                            _plugin.Configuration.Save();
                                        } else {
                                            //_pins.Remove(lootResult.Key);
                                            _plugin.Configuration.LootPins.Remove(lootResult.Key);
                                            _plugin.Configuration.Save();
                                        }
                                        _plugin.DataQueue.QueueDataOperation(() => {
                                            SortByColumn((SortableColumn)sortSpecs.Specs.ColumnUserID, sortSpecs.Specs.SortDirection);
                                        });
                                    }
                                }
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.DroppedQuantity.ToString("N0")}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.ObtainedQuantity.ToString("N0")}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.AveragePrice?.ToString("N0")}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.DroppedValue?.ToString("N0")}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{lootResult.Value.ObtainedValue?.ToString("N0")}");
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

            if((_currentPage + 1) * _maxPageSize <= _lootResults.Count) {
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 65f * ImGuiHelpers.GlobalScale);
                if(ImGui.Button($"Next {_maxPageSize}")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        GoToPage(_currentPage + 1);
                    });
                }
            }
        }

        private void SortByColumn(SortableColumn column, ImGuiSortDirection direction) {
            Func<KeyValuePair<LootResultKey, LootResultValue>, bool> pinComparator = (r) => _plugin.Configuration.LootPins.Contains(r.Key);
            Func<KeyValuePair<LootResultKey, LootResultValue>, object> comparator = (r) => 0;

            switch(column) {
                case SortableColumn.Name:
                    comparator = (r) => r.Value.ItemName;
                    break;
                case SortableColumn.IsHQ:
                    comparator = (r) => r.Key.IsHQ;
                    break;
                case SortableColumn.DroppedQuantity:
                    comparator = (r) => r.Value.DroppedQuantity;
                    break;
                case SortableColumn.ObtainedQuantity:
                    comparator = (r) => r.Value.ObtainedQuantity;
                    break;
                case SortableColumn.Category:
                    comparator = (r) => r.Value.Category;
                    break;
                case SortableColumn.UnitPrice:
                    comparator = (r) => r.Value.AveragePrice;
                    break;
                case SortableColumn.DroppedValue:
                    comparator = (r) => r.Value.DroppedValue;
                    break;
                case SortableColumn.ObtainedValue:
                    comparator = (r) => r.Value.ObtainedValue;
                    break;
                default:
                    comparator = (r) => r;
                    break;
            }

            //Func<KeyValuePair<LootResultKey, LootResultValue>, object> x = (r) => pinComparator(r) ? 1 : comparator(r);

            if(_includePins) {
                var pinnedList = _lootResults.Where(lr => _plugin.Configuration.LootPins.Contains(lr.Key));
                pinnedList = direction == ImGuiSortDirection.Ascending ? pinnedList.OrderBy(comparator) : pinnedList.OrderByDescending(comparator);
                var nonPinnedList = _lootResults.Where(lr => !_plugin.Configuration.LootPins.Contains(lr.Key));
                nonPinnedList = direction == ImGuiSortDirection.Ascending ? nonPinnedList.OrderBy(comparator) : nonPinnedList.OrderByDescending(comparator);
                var sortedList = pinnedList.Concat(nonPinnedList);
                _lootResults = sortedList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            } else {
                _lootResults = (direction == ImGuiSortDirection.Ascending ? _lootResults.OrderBy(comparator) : _lootResults.OrderByDescending(comparator)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            GoToPage();
        }
    }
}
