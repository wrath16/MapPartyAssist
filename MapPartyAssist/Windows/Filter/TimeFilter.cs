using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {

    public enum StatRange {
        Current,
        PastDay,
        PastWeek,
        ThisMonth,
        LastMonth,
        ThisYear,
        LastYear,
        SinceLastClear,
        All,
        AllLegacy
    }

    public class TimeFilter : DataFilter {

        public override string Name => "Time";
        public override string HelpMessage => "'Current' limits to maps and linked duties on the map tracker window.";

        public StatRange StatRange { get; set; } = StatRange.All;
        public static string[] Range = { "Current", "Last 24 hours", "Last 7 days", "This month", "Last month", "This year", "Last year", "Since last clear", "All-Time", "All-Time with imported data" };

        //public static string RangeToString(StatRange range) {
        //    return range switch {
        //        StatRange.Current => "Current",
        //        StatRange.PastDay => "Last 24 hours",
        //        StatRange.PastWeek => "Last 7 days",
        //        StatRange.SinceLastClear => "Since last clear",
        //        StatRange.All => "All-Time",
        //        StatRange.AllLegacy => "All-Time with Imports",
        //        _ => "???",
        //    };
        //}

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
                _plugin!.DataQueue.QueueDataOperation(() => {
                    StatRange = (StatRange)statRangeToInt;
                    Refresh();
                });
            }
        }
    }
}
