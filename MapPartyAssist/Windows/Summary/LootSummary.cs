using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Summary {
    internal class LootSummary {
        private class LootResultKey : IEquatable<LootResultKey> {
            public uint ItemId;
            public bool IsHQ;

            public bool Equals(LootResultKey? other) {
                if(other is null) {
                    return false;
                }
                return ItemId == other.ItemId && IsHQ == other.IsHQ;
            }

            public override int GetHashCode() {
                return 0;
            }
        }

        private class LootResultValue : IEquatable<LootResultValue> {
            public int DroppedQuantity, ObtainedQuantity, Rarity;
            public string ItemName = "", Category = "";

            public bool Equals(LootResultValue? other) {
                if(other is null) {
                    return false;
                }
                return DroppedQuantity == other.DroppedQuantity && ObtainedQuantity == other.ObtainedQuantity && Rarity == other.Rarity
                    && ItemName == other.ItemName && Category == other.Category;
            }
        }

        enum SortableColumn {
            Name,
            DroppedQuantity,
            ObtainedQuantity,
            IsHQ,
            Category,
        }

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private int _lootEligibleRuns = 0;
        private int _lootEligibleMaps = 0;
        private Dictionary<LootResultKey, LootResultValue> _lootResults = new();
        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private bool _firstDraw;
        public string LootCSV { get; private set; }

        internal LootSummary(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
        }

        public void Refresh(List<DutyResults> dutyResults, List<MPAMap> maps) {
            Dictionary<LootResultKey, LootResultValue> newLootResults = new();
            int newLootEligibleRuns = 0;
            int newLootEligibleMaps = 0;
            string newLootCSV = "Category,Quality,Name,Dropped,Obtained\n";

            List<string> selfPlayers = new();
            _plugin.StorageManager.GetPlayers().Query().Where(p => p.IsSelf).ToList().ForEach(p => {
                selfPlayers.Add(p.Key);
            });

            var addLootResult = (LootResult lootResult) => {
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
                        Category = row.ItemUICategory.Value.Name,
                    });
                }
            };

            foreach(var dutyResult in dutyResults) {
                if(dutyResult.HasLootResults()) {
                    newLootEligibleRuns++;
                    foreach(var checkpointResult in dutyResult.CheckpointResults) {
                        foreach(var lootResult in checkpointResult.LootResults!) {
                            addLootResult(lootResult);
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
                    addLootResult(lootResult);
                }
            }

            //set names, set CSV and check for changes
            bool hasChange = newLootResults.Count != _lootResults.Count;
            foreach(var lootResult in newLootResults) {
                bool isPlural = lootResult.Value.DroppedQuantity != 1;
                var row = _plugin.DataManager.GetExcelSheet<Item>()?.First(r => r.RowId == lootResult.Key.ItemId);
                //lootResult.Value.ItemName = row is null ? "" : (isPlural ? row.Plural : row.Singular);
                lootResult.Value.ItemName = row is null ? "" : row.Name;
                newLootCSV += $"{lootResult.Value.Category},{(lootResult.Key.IsHQ ? "HQ" : "")},{lootResult.Value.ItemName},{lootResult.Value.DroppedQuantity},{lootResult.Value.ObtainedQuantity}\n";

                if(!_lootResults.ContainsKey(lootResult.Key)) {
                    hasChange = true;
                } else if(!lootResult.Value.Equals(_lootResults[lootResult.Key])) {
                    hasChange = true;
                }
            }
            if(hasChange) {
#if DEBUG
                _plugin.Log.Debug($"changes detected!");
#endif
                _lootResults = newLootResults;
                _lootEligibleRuns = newLootEligibleRuns;
                _lootEligibleMaps = newLootEligibleMaps;
                LootCSV = newLootCSV;
                _firstDraw = true;
            }
        }

        public void Draw() {
            if(ImGui.Button("Copy CSV")) {
                Task.Run(() => {
                    ImGui.SetClipboardText(LootCSV);
                });
            }
            ImGui.SameLine();
            ImGui.Text($"Eligible maps: {_lootEligibleMaps} Eligible duties: {_lootEligibleRuns}");
            //ImGuiComponents.HelpMarker("");
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Loot tracking introduced in version 1.0.3.0.");

            ImGui.BeginTable($"loottable", 5, ImGuiTableFlags.Sortable | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable
                | ImGuiTableFlags.ScrollY, new Vector2(ImGui.GetContentRegionAvail().X, float.Max(ImGuiHelpers.GlobalScale * 400f, ImGui.GetContentRegionAvail().Y)));
            ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f, (uint)SortableColumn.Category);
            ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f, (uint)SortableColumn.IsHQ);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200f, (uint)SortableColumn.Name);
            ImGui.TableSetupColumn("Dropped", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 65f, (uint)SortableColumn.DroppedQuantity);
            ImGui.TableSetupColumn("Obtained", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 70f, (uint)SortableColumn.ObtainedQuantity);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            //column sorting
            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
            if(sortSpecs.SpecsDirty || _firstDraw) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    SortByColumn((SortableColumn)sortSpecs.Specs.ColumnUserID, sortSpecs.Specs.SortDirection);
                });
                sortSpecs.SpecsDirty = false;
            }

            ImGui.TableNextRow();
            foreach(var lootResult in _lootResults) {
                ImGui.TableNextColumn();
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
                ImGui.TextColored(textColor, $"{lootResult.Value.ItemName}");
                ImGui.TableNextColumn();
                ImGui.Text($"{lootResult.Value.DroppedQuantity}");
                ImGui.TableNextColumn();
                ImGui.Text($"{lootResult.Value.ObtainedQuantity}");
            }
            ImGui.EndTable();
            _firstDraw = false;
        }

        private void SortByColumn(SortableColumn column, ImGuiSortDirection direction) {
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
                default:
                    comparator = (r) => r;
                    break;
            }

            var sortedList = direction == ImGuiSortDirection.Ascending ? _lootResults.OrderBy(comparator) : _lootResults.OrderByDescending(comparator);
            _lootResults = sortedList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
