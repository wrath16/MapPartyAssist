using Dalamud.Configuration;
using Dalamud.Plugin;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;

namespace MapPartyAssist {
    [Serializable]
    public class Configuration : IPluginConfiguration {
        public int Version { get; set; } = 0;
        public bool MasterSwitch { get; set; } = true;
        public uint ArchiveThresholdHours { get; set; } = 24;
        public Dictionary<string, MPAMember> RecentPartyList { get; set; }
        public bool ShowZoneTable { get; set; } = false;
        public bool EnableWhileSolo { get; set; } = true;

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public Configuration() {
            RecentPartyList = new Dictionary<string, MPAMember>();
        }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
        }

        public void Save() {
            this.PluginInterface!.SavePluginConfig(this);
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
