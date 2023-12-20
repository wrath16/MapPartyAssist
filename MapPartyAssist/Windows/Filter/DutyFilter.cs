using Dalamud.Interface.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Windows.Filter {
    public class DutyFilter : DataFilter {

        public override string Name => "Duty";

        public Dictionary<int, bool> FilterState { get; set; } = new();
        internal bool AllSelected { get; private set; } = false;

        internal DutyFilter() { }

        internal DutyFilter(Plugin plugin, Action action, DutyFilter? filter = null) : base(plugin, action) {
            _plugin = plugin;
            foreach(var duty in _plugin.DutyManager.Duties) {
                FilterState.Add(duty.Key, true);
                if(filter is not null && filter.FilterState.ContainsKey(duty.Key)) {
                    FilterState[duty.Key] = filter.FilterState[duty.Key];
                }
            }
            UpdateAllSelected();
        }

        private void UpdateAllSelected() {
            AllSelected = true;
            foreach(var duty in FilterState) {
                AllSelected = AllSelected && duty.Value;
            }
        }

        internal override void Draw() {
            bool allSelected = AllSelected;
            if(ImGui.Checkbox("Select All", ref allSelected)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    foreach(var duty in FilterState) {
                        FilterState[duty.Key] = allSelected;
                    }
                    AllSelected = allSelected;
                    Refresh();
                });
            }

            ImGui.BeginTable("dutyFilterTable", 2);
            ImGui.TableSetupColumn($"c1", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 2, ImGuiHelpers.GlobalScale * 400f));
            ImGui.TableSetupColumn($"c2", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 2, ImGuiHelpers.GlobalScale * 400f));
            ImGui.TableNextRow();

            foreach(var duty in FilterState) {
                ImGui.TableNextColumn();
                bool filterState = duty.Value;
                if(ImGui.Checkbox($"{_plugin.DutyManager.Duties[duty.Key].GetDisplayName()}##{GetHashCode()}", ref filterState)) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        FilterState[duty.Key] = filterState;
                        UpdateAllSelected();
                        Refresh();
                    });
                }
            }

            ImGui.EndTable();
        }
    }
}
