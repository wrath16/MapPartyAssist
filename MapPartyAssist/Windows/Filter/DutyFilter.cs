using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using static Lumina.Data.BaseFileHandle;

namespace MapPartyAssist.Windows.Filter {
    internal partial class DutyFilter : DataFilter {

        public override string Name => "Duty";

        internal Dictionary<int, bool> FilterState = new();
        internal bool AllSelected { get; private set; } = false;

        //partial void Refresh();

        internal DutyFilter(Plugin plugin, Action action) : base(plugin, action) {
            foreach(var duty in _plugin.DutyManager.Duties) {
                FilterState.Add(duty.Key, true);
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
            ImGui.TableSetupColumn($"c1", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn($"c2", ImGuiTableColumnFlags.WidthStretch);
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
