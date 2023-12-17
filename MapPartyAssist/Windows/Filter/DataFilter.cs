using System;

namespace MapPartyAssist.Windows.Filter {
    internal abstract class DataFilter {

        protected Plugin _plugin;
        public virtual string Name { get; }
        public virtual string? HelpMessage { get; }
        private Action RefreshData { get; init; }

        protected DataFilter(Plugin plugin, Action action) {
            _plugin = plugin;
            RefreshData = action;
        }

        internal void Refresh() {
            //_plugin.DataQueue.QueueDataOperation(() => RefreshData());
            RefreshData();
        }

        internal abstract void Draw();
    }
}
