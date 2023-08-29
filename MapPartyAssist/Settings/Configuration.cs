using Dalamud.Configuration;
using Dalamud.Logging;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MapPartyAssist.Settings {
    [Serializable]
    public class Configuration : IPluginConfiguration {
        public int Version { get; set; } = 2;
        public bool MasterSwitch { get; set; } = true;
        public uint ArchiveThresholdHours { get; set; } = 24;
        public bool HideZoneTableWhenEmpty { get; set; } = false;
        public bool RequireDoubleClickOnClearAll { get; set; } = false;
        public bool EnableWhileSolo { get; set; } = true;
        //public bool ShowDeaths { get; set; } = false;
        public bool CurrentCharacterStatsOnly { get; set; } = false;
        public Dictionary<int, DutyConfiguration> DutyConfigurations { get; set; } = new();
        public Dictionary<string, MPAMember> RecentPartyList { get; set; } = new();
        public List<DutyResults> DutyResults { get; set; } = new();

        [NonSerialized]
        private Plugin? _plugin;
        [NonSerialized]
        private SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public Configuration() {
        }

        public void Initialize(Plugin plugin) {
            _plugin = plugin;

            // add new duty configurations...
            foreach(var duty in _plugin.DutyManager.Duties) {
                if(!DutyConfigurations.ContainsKey(duty.Key)) {
                    DutyConfigurations.Add(duty.Key, new DutyConfiguration(duty.Key, false));
                    //hidden canals
                    if(duty.Key == 276) {
                        DutyConfigurations[duty.Key].DisplayClearSequence = true;
                    }
                }
            }

        }

        public void Save() {
            try {
                _fileLock.Wait();
                _plugin!.PluginInterface.SavePluginConfig(this);
                _fileLock.Release();
            } catch(Exception e) {
                _fileLock.Release();
                PluginLog.Error($"Save config error: {e.Message}");
                PluginLog.Error($"{e.StackTrace}");
            }
        }

        //removes players who have 0 maps and last joined >24 hours
        public void PruneRecentPartyList() {
            foreach(var player in RecentPartyList) {
                if(player.Value.Maps.Count <= 0 && (DateTime.Now - player.Value.LastJoined).TotalHours >= ArchiveThresholdHours) {
                    RecentPartyList.Remove(player.Key);
                }
            }
            Save();
        }
    }
}
