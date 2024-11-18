using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Services {
    internal class GameStateManager : IDisposable {

        private Plugin _plugin;
        private int _lastPartySize = 0;

        internal ushort CurrentTerritory { get; private set; } = 0;
        public Dictionary<string, MPAMember> CurrentPartyList { get; private set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; private set; } = new();

        public GameStateManager(Plugin plugin) {
            _plugin = plugin;
            _plugin.Framework.Update += OnFrameworkUpdate;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            _plugin.ClientState.Login += OnLogin;
            _plugin.ClientState.Logout += OnLogout;
            CurrentTerritory = _plugin.ClientState.TerritoryType;

            _plugin.DataQueue.QueueDataOperation(() => {
                if(_plugin.ClientState.IsLoggedIn) {
                    BuildPartyLists();
                }
                _plugin.Refresh();
            });
        }

        public void Dispose() {
            _plugin.Framework.Update -= OnFrameworkUpdate;
            _plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            _plugin.ClientState.Login -= OnLogin;
            _plugin.ClientState.Logout -= OnLogout;
        }

        private void OnFrameworkUpdate(IFramework framework) {
            var playerJob = _plugin.ClientState.LocalPlayer?.ClassJob.Value.Abbreviation;
            var currentPartySize = _plugin.PartyList.Length;

            if(!_plugin.Condition[ConditionFlag.BetweenAreas] && playerJob != null && currentPartySize != _lastPartySize) {
                _plugin.Log.Debug($"Party size has changed: {_lastPartySize} to {currentPartySize}");
                _lastPartySize = currentPartySize;
                _plugin.DataQueue.QueueDataOperation(() => {
                    BuildPartyLists();
                    _plugin.Refresh();
                });
            }
        }

        private void OnTerritoryChanged(ushort territoryId) {
            _plugin.DataQueue.QueueDataOperation(() => {
                CurrentTerritory = territoryId;
            });
        }

        private void OnLogin() {
            _plugin.DataQueue.QueueDataOperation(() => {
                _plugin.PriceHistory.Initialize();
                BuildPartyLists();
                _plugin.MapManager.CheckAndArchiveMaps();
            });
        }

        private void OnLogout(int type, int code) {
            _plugin.DataQueue.QueueDataOperation(() => {
                _plugin.PriceHistory.Shutdown();
                CurrentPartyList = new();
                _plugin.Refresh();
            });
        }

        public string? GetCurrentPlayer() {
            string? currentPlayerName = _plugin.ClientState.LocalPlayer?.Name?.ToString();
            string? currentPlayerWorld = _plugin.ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString();
            if(currentPlayerName == null || currentPlayerWorld == null) {
                //throw exception?
                //throw new InvalidOperationException("Cannot retrieve current player");
                return null;
            }
            return $"{currentPlayerName} {currentPlayerWorld}";
        }

        public Region GetCurrentRegion() {
            var currentPlayerRegion = _plugin.ClientState.LocalPlayer?.CurrentWorld.Value.DataCenter.Value.Region;
            return PlayerHelper.GetRegion(currentPlayerRegion);
        }

        private void BuildPartyLists() {
            BuildCurrentPartyList(_plugin.PartyList.ToArray());
            BuildRecentPartyList();
        }

        //builds current party list from scratch
        private void BuildCurrentPartyList(IPartyMember[] partyMembers) {
            _plugin.Log.Debug("Rebuilding current party list.");
            string currentPlayerName = _plugin.ClientState.LocalPlayer!.Name.ToString()!;
            string currentPlayerWorld = _plugin.ClientState.LocalPlayer!.HomeWorld.Value.Name.ToString();
            string currentPlayerKey = GetCurrentPlayer()!;
            CurrentPartyList = new();
            var allPlayers = _plugin.StorageManager.GetPlayers();
            var currentPlayer = allPlayers.Query().Where(p => p.Key == currentPlayerKey).FirstOrDefault();
            //enable for solo player
            if(partyMembers.Length <= 0) {
                //add yourself for initial setup
                if(currentPlayer == null) {
                    var newPlayer = new MPAMember(currentPlayerName, currentPlayerWorld, true);
                    CurrentPartyList.Add(currentPlayerKey, newPlayer);
                    _plugin.StorageManager.AddPlayer(newPlayer, false);
                } else {
                    currentPlayer.LastJoined = DateTime.Now;
                    CurrentPartyList.Add(currentPlayerKey, currentPlayer);
                    _plugin.StorageManager.UpdatePlayer(currentPlayer, false);
                }
            } else {
                foreach(IPartyMember p in partyMembers) {
                    string partyMemberName = p.Name.ToString();
                    string partyMemberWorld = p.World.Value.Name.ToString();
                    var key = $"{partyMemberName} {partyMemberWorld}";
                    bool isCurrentPlayer = partyMemberName.Equals(currentPlayerName) && partyMemberWorld.Equals(currentPlayerWorld);
                    var findPlayer = allPlayers.Query().Where(p => p.Key == key).FirstOrDefault();

                    //new player!
                    if(findPlayer == null) {
                        var newPlayer = new MPAMember(partyMemberName, partyMemberWorld, isCurrentPlayer);
                        CurrentPartyList.Add(key, newPlayer);
                        _plugin.StorageManager.AddPlayer(newPlayer, false);
                    } else {
                        //find existing player
                        findPlayer.LastJoined = DateTime.Now;
                        findPlayer.IsSelf = isCurrentPlayer;
                        CurrentPartyList.Add(key, findPlayer);
                        _plugin.StorageManager.UpdatePlayer(findPlayer, false);
                    }
                }
            }
        }

        internal void BuildRecentPartyList() {
            _plugin.Log.Debug("Rebuilding recent party list.");
            RecentPartyList = new();
            var allPlayers = _plugin.StorageManager.GetPlayers().Query().ToList();
            var currentMaps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted).ToList();
            foreach(var player in allPlayers) {
                TimeSpan timeSpan = DateTime.Now - player.LastJoined;
                bool isRecent = timeSpan.TotalHours <= _plugin.Configuration.ArchiveThresholdHours;
                bool hasMaps = currentMaps.Where(m => !m.Owner.IsNullOrEmpty() && m.Owner.Equals(player.Key)).Any();
                bool notCurrent = !CurrentPartyList.ContainsKey(player.Key);
                bool notSelf = !player.IsSelf;
                if(isRecent && hasMaps && notCurrent) {
                    RecentPartyList.Add(player.Key, player);
                }
            }
        }
    }
}
