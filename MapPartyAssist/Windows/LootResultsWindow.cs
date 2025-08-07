//using Dalamud.Interface.Colors;
//using Dalamud.Interface.Components;
//using Dalamud.Interface.Utility;
//using Dalamud.Interface.Windowing;
//using Dalamud.Bindings.ImGui;
//using Lumina.Excel.GeneratedSheets;
//using MapPartyAssist.Types;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using System.Threading;
//using System.Threading.Tasks;

//namespace MapPartyAssist.Windows {

//    internal class LootResultsWindow : Window {
//        private class LootResultKey : IEquatable<LootResultKey> {
//            public uint ItemId;
//            public bool IsHQ;

//            public bool Equals(LootResultKey? other) {
//                if(other is null) {
//                    return false;
//                }
//                return ItemId == other.ItemId && IsHQ == other.IsHQ;
//            }

//            public override int GetHashCode() {
//                return 0;
//            }
//        }

//        private class LootResultValue {
//            public int DroppedQuantity, ObtainedQuantity, Rarity;
//            public string ItemName = "";
//        }

//        enum SortableColumn {
//            Name,
//            DroppedQuantity,
//            ObtainedQuantity,
//            IsHQ,
//        }


//        private Plugin _plugin;
//        private int _lootEligibleRuns = 0;
//        private Dictionary<LootResultKey, LootResultValue> _lootResults = new();
//        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
//        private bool _firstDraw;

//        internal LootResultsWindow(Plugin plugin) : base("Loot Results") {
//            SizeConstraints = new WindowSizeConstraints {
//                MinimumSize = new Vector2(300, 50),
//                MaximumSize = new Vector2(600, 1000)
//            };
//            PositionCondition = ImGuiCond.Appearing;
//            _plugin = plugin;
//        }

//        public Task Refresh(List<DutyResults> dutyResults) {
//            return Task.Run(async () => {
//                try {
//                    await _refreshLock.WaitAsync();
//                    List<string> selfPlayers = new();
//                    _plugin.StorageManager.GetPlayers().Query().Where(p => p.IsSelf).ToList().ForEach(p => {
//                        selfPlayers.Add(p.Key);
//                    });
//                    _lootResults = new();
//                    _lootEligibleRuns = 0;
//                    foreach(var dutyResult in dutyResults) {
//                        if(dutyResult.HasLootResults()) {
//                            _lootEligibleRuns++;
//                            foreach(var checkpointResult in dutyResult.CheckpointResults) {
//                                foreach(var lootResult in checkpointResult.LootResults!) {
//                                    var key = new LootResultKey { ItemId = lootResult.ItemId, IsHQ = lootResult.IsHQ };
//                                    bool selfObtained = lootResult.Recipient is not null && selfPlayers.Contains(lootResult.Recipient);
//                                    int obtainedQuantity = selfObtained ? lootResult.Quantity : 0;
//                                    if(_lootResults.ContainsKey(key)) {
//                                        _lootResults[key].ObtainedQuantity += obtainedQuantity;
//                                        _lootResults[key].DroppedQuantity += lootResult.Quantity;
//                                    } else {
//                                        _lootResults.Add(key, new LootResultValue {
//                                            DroppedQuantity = lootResult.Quantity,
//                                            ObtainedQuantity = obtainedQuantity,
//                                            Rarity = _plugin.DataManager.GetExcelSheet<Item>().GetRow(lootResult.ItemId).Rarity
//                                        });
//                                    }
//                                }
//                            }
//                        }
//                    }

//                    //set names
//                    foreach(var lootResult in _lootResults) {
//                        bool isPlural = lootResult.Value.DroppedQuantity != 1;
//                        var row = _plugin.DataManager.GetExcelSheet<Item>()?.First(r => r.RowId == lootResult.Key.ItemId);
//                        //lootResult.Value.ItemName = row is null ? "" : (isPlural ? row.Plural : row.Singular);
//                        lootResult.Value.ItemName = row is null ? "" : row.Name;
//                    }
//                    _firstDraw = true;
//                } finally {
//                    _refreshLock.Release();
//                }

//            });
//        }

//        public override void Draw() {
//            ImGui.Text($"Eligible runs: {_lootEligibleRuns}");
//            ImGuiComponents.HelpMarker("Loot tracking started with version 1.0.3.0.");


//            ImGui.BeginTable($"loottable", 4, ImGuiTableFlags.Sortable | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable);
//            ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f, (uint)SortableColumn.IsHQ);
//            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200f, (uint)SortableColumn.Name);
//            ImGui.TableSetupColumn("Dropped", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 65f, (uint)SortableColumn.DroppedQuantity);
//            ImGui.TableSetupColumn("Obtained", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.ObtainedQuantity);

//            ImGui.TableHeadersRow();

//            //column sorting
//            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
//            if(sortSpecs.SpecsDirty || _firstDraw) {
//                SortByColumn((SortableColumn)sortSpecs.Specs.ColumnUserID, sortSpecs.Specs.SortDirection);
//                sortSpecs.SpecsDirty = false;
//            }


//            ImGui.TableNextRow();
//            foreach(var lootResult in _lootResults) {
//                ImGui.TableNextColumn();
//                //ImGui.AlignTextToFramePadding();
//                //ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f);
//                ImGui.Text($"{(lootResult.Key.IsHQ ? "HQ" : "")}");
//                ImGui.TableNextColumn();
//                var textColor = ImGuiColors.DalamudWhite;
//                switch(lootResult.Value.Rarity) {
//                    default:
//                        textColor = ImGuiColors.DalamudWhite; break;
//                    case 2: //green
//                        textColor = ImGuiColors.HealerGreen; break;
//                    case 3: //blue
//                        textColor = ImGuiColors.TankBlue; break;
//                    case 4: //purple
//                        textColor = ImGuiColors.ParsedPurple; break;
//                    case 7: //pink
//                        textColor = ImGuiColors.ParsedPink; break;
//                }
//                ImGui.TextColored(textColor, $"{lootResult.Value.ItemName}");
//                ImGui.TableNextColumn();
//                ImGui.Text($"{lootResult.Value.DroppedQuantity}");
//                ImGui.TableNextColumn();
//                ImGui.Text($"{lootResult.Value.ObtainedQuantity}");
//            }

//            ImGui.EndTable();
//            _firstDraw = false;
//        }

//        private void SortByColumn(SortableColumn column, ImGuiSortDirection direction) {
//            Func<KeyValuePair<LootResultKey, LootResultValue>, object> comparator = (r) => 0;

//            switch(column) {
//                case SortableColumn.Name:
//                    comparator = (r) => r.Value.ItemName;
//                    break;
//                case SortableColumn.IsHQ:
//                    comparator = (r) => r.Key.IsHQ;
//                    break;
//                case SortableColumn.DroppedQuantity:
//                    comparator = (r) => r.Value.DroppedQuantity;
//                    break;
//                case SortableColumn.ObtainedQuantity:
//                    comparator = (r) => r.Value.ObtainedQuantity;
//                    break;
//                default:
//                    comparator = (r) => r;
//                    break;
//            }

//            var sortedList = direction == ImGuiSortDirection.Ascending ? _lootResults.OrderBy(comparator) : _lootResults.OrderByDescending(comparator);
//            _lootResults = sortedList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
//        }
//    }
//}
