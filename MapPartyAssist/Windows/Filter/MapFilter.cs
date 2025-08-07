using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Windows.Filter {
    public class MapFilter : DataFilter {
        public override string Name => "Map Type";
        public override string HelpMessage => "It isn't possible to tell with certainty what kind of map has been used by a party member, " +
            "\nso maps are grouped by the expansion of the zone in which they were recorded.\n\n" +
            "For maps recorded prior to v2.0.0.0, this information may be unavailable.";
        internal bool AllSelected { get; set; }
        public Dictionary<TreasureMapCategory, bool> FilterState { get; set; } = new();

        public MapFilter() { }

        internal MapFilter(Plugin plugin, Action action, MapFilter? filter = null) : base(plugin, action) {
            //AllSelected = true;
            FilterState = new() {
                {TreasureMapCategory.ARealmReborn, true },
                {TreasureMapCategory.Heavensward, true },
                {TreasureMapCategory.Stormblood, true },
                {TreasureMapCategory.Shadowbringers, true },
                {TreasureMapCategory.Endwalker, true },
                {TreasureMapCategory.Elpis, true },
                {TreasureMapCategory.Dawntrail, true },
                {TreasureMapCategory.Unknown, true },
            };

            if(filter is not null) {
                foreach(var category in filter.FilterState) {
                    FilterState[category.Key] = category.Value;
                }
            }
            UpdateAllSelected();
        }

        private void UpdateAllSelected() {
            AllSelected = true;
            foreach(var category in FilterState) {
                AllSelected = AllSelected && category.Value;
            }
        }

        internal override void Draw() {
            bool allSelected = AllSelected;
            if(ImGui.Checkbox($"Select All##{GetHashCode()}", ref allSelected)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    foreach(var category in FilterState) {
                        FilterState[category.Key] = allSelected;
                    }
                    AllSelected = allSelected;
                    Refresh();
                });
            }

            using var table = ImRaii.Table("mapFilterTable", 2);
            if(table) {
                ImGui.TableSetupColumn($"c1", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 2, ImGuiHelpers.GlobalScale * 400f));
                ImGui.TableSetupColumn($"c2", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 2, ImGuiHelpers.GlobalScale * 400f));
                ImGui.TableNextRow();

                foreach(var category in FilterState) {
                    ImGui.TableNextColumn();
                    bool filterState = category.Value;
                    if(ImGui.Checkbox($"{MapHelper.GetCategoryName(category.Key)}##{GetHashCode()}", ref filterState)) {
                        _plugin!.DataQueue.QueueDataOperation(() => {
                            FilterState[category.Key] = filterState;
                            UpdateAllSelected();
                            Refresh();
                        });
                    }
                }
            }
        }
    }
}
