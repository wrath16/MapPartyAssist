using MapPartyAssist.Windows.Filter;

namespace MapPartyAssist.Settings {
    public class FilterConfiguration {

        public MapFilter? MapFilter { get; set; }
        public DutyFilter? DutyFilter { get; set; }
        public OwnerFilter? OwnerFilter { get; set; }
        public PartyMemberFilter? PartyMemberFilter { get; set; }
        public TimeFilter? TimeFilter { get; set; }

    }
}
