using System;

namespace MapPartyAssist.Services {
    internal class GameStateManager : IDisposable {

        private Plugin _plugin;

        internal ushort CurrentTerritory { get; private set; } = 0;

        public GameStateManager(Plugin plugin) {
            _plugin = plugin;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            CurrentTerritory = _plugin.ClientState.TerritoryType;
        }

        public void Dispose() {
            _plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        }

        private void OnTerritoryChanged(ushort territoryId) {
            _plugin.DataQueue.QueueDataOperation(() => {
                CurrentTerritory = territoryId;
            });
        }
    }
}
