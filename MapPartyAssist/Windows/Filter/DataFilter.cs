using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows.Filter {
    public abstract class DataFilter {

        protected Plugin? _plugin;
        [JsonIgnore]
        public virtual string Name => "";
        [JsonIgnore]
        public virtual string? HelpMessage { get; }
        private Func<Task>? RefreshData { get; init; }

        [JsonConstructor]
        public DataFilter() {
        }

        protected DataFilter(Plugin plugin, Func<Task> action) {
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
