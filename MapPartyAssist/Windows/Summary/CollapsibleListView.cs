using LiteDB;
using System.Collections.Generic;
using static MapPartyAssist.Windows.Summary.LootSummary;

namespace MapPartyAssist.Windows.Summary {
    internal class CollapsibleListView<T> {

        protected Plugin? _plugin;
        private StatsWindow _statsWindow;

        public virtual int PageSize { get; protected set; } = 100;
        public string CSV { get; protected set; } = "";

        protected List<T> _fullModel = new();
        protected List<T> _pageModel = new();
        protected Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> _lootResults = new();

        protected int _currentPage = 0;
        protected bool _collapseAll = false;

        internal CollapsibleListView(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
        }
    }
}
