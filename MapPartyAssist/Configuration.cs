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
    }
}
