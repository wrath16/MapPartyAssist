using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class GameStateManager : IDisposable {

        private Plugin _plugin;
        private int _lastPartySize = 0;

        internal ushort CurrentTerritory { get; private set; } = 0;
        public Dictionary<string, MPAMember> CurrentPartyList { get; private set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; private set; } = new();
        public string? CurrentPlayer { get; private set; }
        public Region? CurrentRegion { get; private set; }

        public GameStateManager(Plugin plugin) {
            _plugin = plugin;
            _plugin.Framework.Update += OnFrameworkUpdate;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            _plugin.ClientState.Login += OnLogin;
            _plugin.ClientState.Logout += OnLogout;
            CurrentTerritory = _plugin.ClientState.TerritoryType;

            _plugin.DataQueue.QueueDataOperation(async () => {
                if(_plugin.ClientState.IsLoggedIn) {
                    await BuildPartyLists();
                }
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
            CurrentRegion = PlayerHelper.GetRegion(_plugin.ClientState.LocalPlayer?.CurrentWorld.Value.DataCenter.Value.Region);
            string? currentPlayerName = _plugin.ClientState.LocalPlayer?.Name?.ToString();
            string? currentPlayerWorld = _plugin.ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString();
            if(currentPlayerName != null && currentPlayerWorld != null) {
                CurrentPlayer = $"{currentPlayerName} {currentPlayerWorld}";
            } else {
                CurrentPlayer = null;
            }

            if(!_plugin.Condition[ConditionFlag.BetweenAreas] && playerJob != null && currentPartySize != _lastPartySize) {
                Plugin.Log.Debug($"Party size has changed: {_lastPartySize} to {currentPartySize}");
                _lastPartySize = currentPartySize;
                _plugin.DataQueue.QueueDataOperation(async () => {
                    await BuildPartyLists();
                    await _plugin.Refresh();
                });
            }
        }

        private void OnTerritoryChanged(ushort territoryId) {
            _plugin.DataQueue.QueueDataOperation(() => {
                CurrentTerritory = territoryId;
            });
        }

        private void OnLogin() {
            Task.Delay(5000).ContinueWith(t => {
                _plugin.DataQueue.QueueDataOperation(async () => {
                    await _plugin.MapManager.CheckAndArchiveMaps();
                    _plugin.PriceHistory.Initialize();
                    await BuildPartyLists();
                    await _plugin.Refresh();
                });
            });
        }

        private void OnLogout(int type, int code) {
            _plugin.DataQueue.QueueDataOperation(async () => {
                _plugin.PriceHistory.Shutdown();
                CurrentPartyList = new();
                await _plugin.Refresh();
            });
        }

        public string? GetCurrentPlayer() {
            return CurrentPlayer;
        }

        public Region GetCurrentRegion() {
            return CurrentRegion ?? Region.Unknown;
        }

        private async Task BuildPartyLists() {
            await BuildCurrentPartyList(_plugin.PartyList.ToArray());
            BuildRecentPartyList();
        }

        //builds current party list from scratch
        private async Task BuildCurrentPartyList(IPartyMember[] partyMembers) {
            Plugin.Log.Debug("Rebuilding current party list.");
            MPAMember currentPlayerKey = new MPAMember(GetCurrentPlayer());
            CurrentPartyList = new();
            var allPlayers = _plugin.StorageManager.GetPlayers();
            var currentPlayer = allPlayers.Query().Where(p => p.Key == currentPlayerKey.Key).FirstOrDefault();
            //enable for solo player
            if(partyMembers.Length <= 0) {
                //add yourself for initial setup
                if(currentPlayer == null) {
                    var newPlayer = new MPAMember(currentPlayerKey.Name, currentPlayerKey.HomeWorld, true);
                    CurrentPartyList.Add(currentPlayerKey.Key, newPlayer);
                    await _plugin.StorageManager.AddPlayer(newPlayer);
                } else {
                    currentPlayer.LastJoined = DateTime.UtcNow;
                    CurrentPartyList.Add(currentPlayerKey.Key, currentPlayer);
                    await _plugin.StorageManager.UpdatePlayer(currentPlayer);
                }
            } else {
                foreach(IPartyMember p in partyMembers) {
                    string partyMemberName = p.Name.ToString();
                    string partyMemberWorld = p.World.Value.Name.ToString();
                    var key = $"{partyMemberName} {partyMemberWorld}";
                    bool isCurrentPlayer = partyMemberName.Equals(currentPlayerKey.Name) && partyMemberWorld.Equals(currentPlayerKey.HomeWorld);
                    var findPlayer = allPlayers.Query().Where(p => p.Key == key).FirstOrDefault();

                    //new player!
                    if(findPlayer == null) {
                        var newPlayer = new MPAMember(partyMemberName, partyMemberWorld, isCurrentPlayer);
                        CurrentPartyList.Add(key, newPlayer);
                        await _plugin.StorageManager.AddPlayer(newPlayer);
                    } else {
                        //find existing player
                        findPlayer.LastJoined = DateTime.UtcNow;
                        findPlayer.IsSelf = isCurrentPlayer;
                        CurrentPartyList.Add(key, findPlayer);
                        await _plugin.StorageManager.UpdatePlayer(findPlayer);
                    }
                }
            }
        }

        internal void BuildRecentPartyList() {
            Plugin.Log.Debug("Rebuilding recent party list.");
            RecentPartyList = new();
            var allPlayers = _plugin.StorageManager.GetPlayers().Query().ToList();
            var currentMaps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted).ToList();
            foreach(var player in allPlayers) {
                TimeSpan timeSpan = DateTime.UtcNow - player.LastJoined;
                bool isRecent = timeSpan.TotalHours <= _plugin.Configuration.ArchiveThresholdHours;
                bool hasMaps = currentMaps.Where(m => !m.Owner.IsNullOrEmpty() && m.Owner.Equals(player.Key)).Any();
                bool notCurrent = !CurrentPartyList.ContainsKey(player.Key);
                bool notSelf = !player.IsSelf;
                if(isRecent && hasMaps && notCurrent) {
                    RecentPartyList.Add(player.Key, player);
                }
            }
        }

        internal string? MatchAliasToPlayer(string? playerKey) {
            foreach(var player in _plugin.GameStateManager.CurrentPartyList) {
                //select first match
                if(PlayerHelper.IsAliasMatch(player.Key, playerKey ?? "")) {
                    Plugin.Log.Debug($"resolving {playerKey} to {player.Key}");
                    return player.Key;
                }
            }
            Plugin.Log.Warning($"Unable to match player alias: {playerKey}");
            return null;
        }
    }
}
