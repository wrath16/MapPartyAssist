using Dalamud.Bindings.ImGui;
using System;

namespace MapPartyAssist.Windows.Filter {
    public class PartyMemberFilter : DataFilter {
        public override string Name => "P. Members";
        public override string HelpMessage => "Comma-separate multiple party members.";
        public string PartyMembersRaw { get; set; } = "";
        public bool OnlySolo { get; set; }
        internal string[] PartyMembers { get; private set; } = new string[0];
        private string _lastValue = "";

        public PartyMemberFilter() { }

        internal PartyMemberFilter(Plugin plugin, Action action, PartyMemberFilter? filter = null) : base(plugin, action) {
            if(filter is not null) {
                PartyMembersRaw = filter.PartyMembersRaw;
                SetPartyMemberArray(PartyMembersRaw);
                _lastValue = PartyMembersRaw;
            }
        }

        private void SetPartyMemberArray(string raw) {
            PartyMembers = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        internal override void Draw() {
            string partyMembers = PartyMembersRaw;
            bool onlySolo = OnlySolo;

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if(ImGui.InputText($"##partyMemberFilter", ref partyMembers, 50, ImGuiInputTextFlags.None)) {
                if(partyMembers != _lastValue) {
                    _lastValue = partyMembers;
                    _plugin!.DataQueue.QueueDataOperation(() => {
                        PartyMembersRaw = partyMembers;
                        SetPartyMemberArray(partyMembers);
                        Refresh();
                    });
                }
            }

            if(ImGui.Checkbox("Solo only", ref onlySolo)) {
                _plugin!.DataQueue.QueueDataOperation(() => {
                    OnlySolo = onlySolo;
                    Refresh();
                });
            }
        }
    }
}
