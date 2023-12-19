using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    public class TimeFilter : DataFilter {

        public override string Name => "Time";

        public StatRange StatRange { get; set; } = StatRange.All;
        public static string[] Range = { "Current", "Last Day", "Last Week", "Since last clear", "All-Time", "All-Time with imported data" };

        public TimeFilter() { }

        internal TimeFilter(Plugin plugin, Action action, TimeFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                StatRange = filter.StatRange;
            }
        }

        internal override void Draw() {
            int statRangeToInt = (int)StatRange;
            //ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2f);
            if(ImGui.Combo($"##timeRangeCombo", ref statRangeToInt, Range, Range.Length)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    StatRange = (StatRange)statRangeToInt;
                    Refresh();
                });
            }
        }
    }
}
