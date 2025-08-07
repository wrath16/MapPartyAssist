using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
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
        Custom
    }

    public class TimeFilter : DataFilter {

        public override string Name => "Time";
        public override string HelpMessage => "'Current' limits to maps and linked duties on the map tracker.\nCustom time ranges input auto-formats using your local timezone.";

        public StatRange StatRange { get; set; } = StatRange.All;
        public static string[] Range = { "Current", "Past 24 hours", "Past 7 days", "This month", "Last month", "This year", "Last year", "Since last clear", "All-time", "Custom" };

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        private string _lastStartTime = "";
        private string _lastEndTime = "";

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
                StartTime = filter.StartTime;
                EndTime = filter.EndTime;
            }
        }

        internal override void Draw() {
            int statRangeToInt = (int)StatRange;
            ImGui.SetNextItemWidth(float.Max(ImGui.GetContentRegionAvail().X / 2f, ImGuiHelpers.GlobalScale * 100f));
            if(ImGui.Combo($"##timeRangeCombo", ref statRangeToInt, Range, Range.Length)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    StatRange = (StatRange)statRangeToInt;
                    Refresh();
                });
            }
            if(StatRange == StatRange.Custom) {
                using var table = ImRaii.Table("timeFilterTable", 2);
                if(table) {
                    ImGui.TableSetupColumn($"c1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn($"c2", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Start:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                    var startTime = StartTime.ToString();
                    if(ImGui.InputText($"##startTime", ref startTime, 50, ImGuiInputTextFlags.None)) {
                        if(startTime != _lastStartTime) {
                            _lastStartTime = startTime;
                            if(DateTime.TryParse(startTime, out DateTime newStartTime)) {
                                _plugin!.DataQueue.QueueDataOperation(() => {
                                    StartTime = newStartTime;
                                    Refresh();
                                });
                            }
                        }
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text("End:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    var endTime = EndTime.ToString();
                    if(ImGui.InputText($"##endTime", ref endTime, 50, ImGuiInputTextFlags.None)) {
                        if(endTime != _lastEndTime) {
                            _lastEndTime = endTime;
                            if(DateTime.TryParse(endTime, out DateTime newEndTime)) {
                                _plugin!.DataQueue.QueueDataOperation(() => {
                                    EndTime = newEndTime;
                                    Refresh();
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}
