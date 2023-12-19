using Newtonsoft.Json;
using System;

namespace MapPartyAssist.Windows.Filter {
    public abstract class DataFilter {

        protected Plugin? _plugin;
        [JsonIgnore]
        public virtual string Name => "";
        [JsonIgnore]
        public virtual string? HelpMessage { get; }
        private Action? RefreshData { get; init; }

        [JsonConstructor]
        public DataFilter() {
        }

        protected DataFilter(Plugin plugin, Action action) {
            _plugin = plugin;
            RefreshData = action;
        }

        internal void Refresh() {
            //_plugin.DataQueue.QueueDataOperation(() => RefreshData());
            if(RefreshData is null) {
                throw new InvalidOperationException("No refresh action initialized!");
            }
            RefreshData();
        }

        internal abstract void Draw();
    }
}
