using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    public class MapFilter : DataFilter {
        public override string Name => "Map";

        public bool IncludeMaps { get; set; }

        public MapFilter() { }

        internal MapFilter(Plugin plugin, Action action, MapFilter? filter = null) : base(plugin, action) {
            IncludeMaps = true;
            if(filter is not null) {
                IncludeMaps = filter.IncludeMaps;
            }
        }

        internal override void Draw() {
            bool includeMaps = IncludeMaps;
            if(ImGui.Checkbox("Include All", ref includeMaps)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    IncludeMaps = includeMaps;
                    Refresh();
                });
            }
        }
    }
}
