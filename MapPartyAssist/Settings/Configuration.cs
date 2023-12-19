using Dalamud.Configuration;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MapPartyAssist.Settings {

    public enum ProgressTableRate {
        Total,
        Previous
    }

    public enum ProgressTableCount {
        All,
        Last
    }

    public enum ClearSequenceCount {
        All,
        Last
    }

    [Serializable]
    public class Configuration : IPluginConfiguration {
        public static int CurrentVersion = 2;
        public int Version { get; set; } = CurrentVersion;
        public bool MasterSwitch { get; set; } = true;
        public uint ArchiveThresholdHours { get; set; } = 24;
        public bool HideZoneTableWhenEmpty { get; set; } = false;
        public bool RequireDoubleClickOnClearAll { get; set; } = false;
        public bool NoOverwriteMapLink { get; set; } = false;
        public bool HighlightLinksInCurrentZone { get; set; } = true;
        public bool HighlightClosestLink { get; set; } = true;
        public bool ShowStatsWindowTooltips { get; set; } = true;
        public bool ShowAdvancedFilters { get; set; } = false;
        public ProgressTableCount ProgressTableCount { get; set; } = ProgressTableCount.All;
        public ProgressTableRate ProgressTableRate { get; set; } = ProgressTableRate.Previous;
        public ClearSequenceCount ClearSequenceCount { get; set; } = ClearSequenceCount.Last;
        public bool TotalMapsClearSequence { get; set; } = false;
        public bool EnableWhileSolo { get; set; } = true;
        public bool CurrentCharacterStatsOnly { get; set; } = false;
        public Dictionary<int, DutyConfiguration> DutyConfigurations { get; set; } = new();
        public FilterConfiguration StatsWindowFilters { get; set; } = new();
        [Obsolete]
        public Dictionary<string, MPAMember> RecentPartyList { get; set; } = new();
        [Obsolete]
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
                    DutyConfigurations.Add(duty.Key, new DutyConfiguration {
                        DutyId = duty.Key,
                        DisplayClearSequence = false,
                        DisplayDeaths = false,
                        OmitZeroCheckpoints = false,
                    });
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
            } finally {
                _fileLock.Release();
            }
        }
    }
}
