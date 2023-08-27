using Dalamud.Configuration;
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
        public Dictionary<int, DutyConfiguration> DutyConfigurations { get; set; }
        public Dictionary<string, MPAMember> RecentPartyList { get; set; }
        public List<DutyResults> DutyResults { get; set; }

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private Plugin? Plugin;

        [NonSerialized]
        private SemaphoreSlim _fileLock;

        public Configuration() {
            RecentPartyList = new Dictionary<string, MPAMember>();
            DutyResults = new();
            DutyConfigurations = new();
            //DutyConfigurations = new() {
            //    { 179, new DutyConfiguration() },
            //    { 268, new DutyConfiguration() },
            //    { 276, new DutyConfiguration() }
            //};
            //DutyConfigurations[276].DisplayClearSequence = true;
            _fileLock = new SemaphoreSlim(1, 1);
        }

        public void Initialize(Plugin plugin) {
            Plugin = plugin;

            // add new duty configurations...
            foreach(var duty in Plugin.DutyManager.Duties) {
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
            _fileLock.Wait();
            Plugin.PluginInterface!.SavePluginConfig(this);
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
