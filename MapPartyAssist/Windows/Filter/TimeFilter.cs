using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    internal class TimeFilter : DataFilter {

        public override string Name => "Time";

        internal StatRange StatRange { get; private set; } = StatRange.All;
        private readonly string[] _rangeCombo = { "Current", "Last Day", "Last Week", "Since last clear", "All-Time", "All-Time with imported data" };

        internal TimeFilter(Plugin plugin, Action action) : base(plugin, action) {
        }

        internal override void Draw() {
            int statRangeToInt = (int)StatRange;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2f);
            if(ImGui.Combo($"##timeRangeCombo", ref statRangeToInt, _rangeCombo, _rangeCombo.Length)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    StatRange = (StatRange)statRangeToInt;
                    Refresh();
                });
            }
        }
    }
}
