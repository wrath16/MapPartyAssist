using Dalamud.Interface.Components;
using ImGuiNET;
using System;

namespace MapPartyAssist.Windows.Filter {
    internal class PartyMemberFilter : DataFilter {
        public override string Name => "P. Members";
        public override string HelpMessage => "Comma-separate multiple party members.";

        internal string[] PartyMembers { get; private set; } = new string[0];

        private string _partyMembersRaw = "";
        private string _lastValue = "";

        internal PartyMemberFilter(Plugin plugin, Action action) : base(plugin, action) {
        }

        internal override void Draw() {
            string partyMembers = _partyMembersRaw;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if(ImGui.InputText($"##partyMemberFilter", ref partyMembers, 50, ImGuiInputTextFlags.None)) {
                if(partyMembers != _lastValue) {
                    _lastValue = partyMembers;
                    _plugin.DataQueue.QueueDataOperation(() => {
                        _partyMembersRaw = partyMembers;
                        PartyMembers = partyMembers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        Refresh();
                    });
                }
            }
            //ImGuiComponents.HelpMarker("Comma-separate party members.");
        }
    }
}
