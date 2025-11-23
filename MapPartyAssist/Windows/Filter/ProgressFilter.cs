using Dalamud.Bindings.ImGui;
using System;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Filter {
    public class ProgressFilter : DataFilter {
        public override string Name => "Progress";

        public bool OnlyClears { get; set; }

        public ProgressFilter() { }

        internal ProgressFilter(Plugin plugin, Func<Task> action, ProgressFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                OnlyClears = filter.OnlyClears;
            }
        }

        internal override void Draw() {
            bool onlyClears = OnlyClears;
            if(ImGui.Checkbox("Full clears only", ref onlyClears)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    OnlyClears = onlyClears;
                    Refresh();
                });
            }
        }
    }
}
