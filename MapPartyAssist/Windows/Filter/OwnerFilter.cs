using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    internal class OwnerFilter : DataFilter {
        public override string Name => "Map Owner";
        internal string Owner { get; private set; } = "";
        private string _lastValue = "";

        internal OwnerFilter(Plugin plugin, Action action) : base(plugin, action) {
        }

        internal override void Draw() {
            string ownerFilter = Owner;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if(ImGui.InputText($"##ownerFilter", ref ownerFilter, 50, ImGuiInputTextFlags.None)) {
                if(ownerFilter != _lastValue) {
                    _lastValue = ownerFilter;
                    _plugin.DataQueue.QueueDataOperation(() => {
                        Owner = ownerFilter;
                        Refresh();
                    });
                }
            }
            //ImGuiComponents.HelpMarker("The owner of the treasure map.");
        }
    }
}
