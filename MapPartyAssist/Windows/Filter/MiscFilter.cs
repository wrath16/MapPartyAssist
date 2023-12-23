using Dalamud.Interface.Utility;
using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    public class MiscFilter : DataFilter {
        public override string Name => "Other";

        public bool LootOnly { get; set; }

        public bool ShowDeleted { get; set; }

        public MiscFilter() { }

        internal MiscFilter(Plugin plugin, Action action, MiscFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                LootOnly = filter.LootOnly;
            }
        }

        internal override void Draw() {

            ImGui.BeginTable("miscFilterTable", 3);
            ImGui.TableSetupColumn($"c1", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 3, ImGuiHelpers.GlobalScale * 350f));
            ImGui.TableSetupColumn($"c2", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 3, ImGuiHelpers.GlobalScale * 350f));
            ImGui.TableSetupColumn($"c3", ImGuiTableColumnFlags.WidthFixed, float.Min(ImGui.GetContentRegionAvail().X / 3, ImGuiHelpers.GlobalScale * 350f));
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            bool lootOnly = LootOnly;
            if(ImGui.Checkbox("Must have loot", ref lootOnly)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    LootOnly = lootOnly;
                    Refresh();
                });
            }
            ImGui.TableNextColumn();
            bool showDeleted = ShowDeleted;
            if(ImGui.Checkbox("Show deleted", ref showDeleted)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    ShowDeleted = showDeleted;
                    Refresh();
                });
            }
            ImGui.EndTable();
        }
    }
}
