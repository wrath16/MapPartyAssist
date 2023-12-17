using ImGuiNET;
using MapPartyAssist.Types;
using System;
using static Lumina.Data.BaseFileHandle;

namespace MapPartyAssist.Windows.Filter {
    internal class MapFilter : DataFilter {
        public override string Name => "Map";

        internal bool IncludeMaps { get; private set; }

        internal MapFilter(Plugin plugin, Action action) : base(plugin, action) {
            IncludeMaps = true;
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
