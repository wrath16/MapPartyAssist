using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Windows.Filter {
    internal class SingleDutyFilter : DataFilter {

        public override string Name => "Duty";
        public int DutyId { get; set; }
        private Dictionary<int, string> _dutyNames = new();

        internal SingleDutyFilter() { }

        internal SingleDutyFilter(Plugin plugin, Action action, bool includeNone, SingleDutyFilter? filter = null) : base(plugin, action) {
            _plugin = plugin;
            if(filter is not null) {
                DutyId = filter.DutyId;
            }

            if(includeNone) {
                _dutyNames.Add(0, "");
            }
            foreach(var duty in _plugin.DutyManager.Duties) {
                _dutyNames.Add(duty.Key, duty.Value.GetDisplayName());
            }

        }

        internal override void Draw() {
            var dutyIndex = _dutyNames.Keys.ToList().IndexOf(DutyId);
            if(ImGui.Combo($"##DutyCombo", ref dutyIndex, _dutyNames.Values.ToArray(), _dutyNames.Count)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    DutyId = _dutyNames.Keys.ElementAt(dutyIndex);
                    Refresh();
                });
            }
        }
    }
}