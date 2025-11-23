using Dalamud.Bindings.ImGui;
using System;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Filter {
    public class OwnerFilter : DataFilter {
        public override string Name => "Map Owner";
        public string Owner { get; set; } = "";
        private string _lastValue = "";

        public OwnerFilter() { }

        internal OwnerFilter(Plugin plugin, Func<Task> action, OwnerFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                Owner = filter.Owner;
                _lastValue = Owner;
            }
        }

        internal override void Draw() {
            string ownerFilter = Owner;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if(ImGui.InputText($"##ownerFilter", ref ownerFilter, 50, ImGuiInputTextFlags.None)) {
                if(ownerFilter != _lastValue) {
                    _lastValue = ownerFilter;
                    _plugin!.DataQueue.QueueDataOperation(() => {
                        Owner = ownerFilter;
                        Refresh();
                    });
                }
            }
            //ImGuiComponents.HelpMarker("The owner of the treasure map.");
        }
    }
}
