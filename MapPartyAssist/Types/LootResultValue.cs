using System;

namespace MapPartyAssist.Types {
    public class LootResultValue : IEquatable<LootResultValue> {
        public int DroppedQuantity, ObtainedQuantity, Rarity;
        public int? AveragePrice, DroppedValue, ObtainedValue;
        public string ItemName = "", Category = "";

        public bool Equals(LootResultValue? other) {
            if(other is null) {
                return false;
            }
            return DroppedQuantity == other.DroppedQuantity && ObtainedQuantity == other.ObtainedQuantity && Rarity == other.Rarity
                && ItemName == other.ItemName && Category == other.Category && AveragePrice == other.AveragePrice;
        }
    }
}
