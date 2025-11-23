using Dalamud.Bindings.ImGui;
using System;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Filter {
    public class ImportFilter : DataFilter {
        public override string Name => "Imports";

        public override string HelpMessage => "Checking this can reduce the amount of information in 'Duty Progress Summary' \ndepending on what was recorded.";

        public bool IncludeImports { get; set; }

        public ImportFilter() { }

        internal ImportFilter(Plugin plugin, Func<Task> action, ImportFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                IncludeImports = filter.IncludeImports;
            }
        }

        internal override void Draw() {
            bool includeImports = IncludeImports;
            if(ImGui.Checkbox("Include imported duty stats", ref includeImports)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    IncludeImports = includeImports;
                    Refresh();
                });
            }
        }
    }
}
