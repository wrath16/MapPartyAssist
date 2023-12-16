using System;

namespace MapPartyAssist.Types {
    public class LootResult {

        public DateTime Time { get; set; }
        //this is only used in display
        public string? ItemName { get; set; }
        public uint ItemId { get; set; }
        public int Quantity { get; set; }
        public bool IsHQ { get; set; }
        public string? Recipient { get; set; }
    }
}
