using Dalamud.Configuration;
using Dalamud.Plugin;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MapPartyAssist {
    [Serializable]
    public class Configuration : IPluginConfiguration {
        public int Version { get; set; } = 2;
        public bool MasterSwitch { get; set; } = true;
        public uint ArchiveThresholdHours { get; set; } = 24;
        public bool HideZoneTableWhenEmpty { get; set; } = false;
        public bool RequireDoubleClickOnClearAll { get; set; } = false;
        public bool EnableWhileSolo { get; set; } = true;
        public Dictionary<string, MPAMember> RecentPartyList { get; set; }
        public List<DutyResults> DutyResults { get; set; }

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        [NonSerialized]
        private SemaphoreSlim _fileLock;

        public Configuration() {
            RecentPartyList = new Dictionary<string, MPAMember>();
            DutyResults = new();
            _fileLock = new SemaphoreSlim(1, 1);
        }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
        }

        public void Save() {
            _fileLock.Wait();
            this.PluginInterface!.SavePluginConfig(this);
            _fileLock.Release();
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
