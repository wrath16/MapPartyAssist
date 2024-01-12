using System;

namespace MapPartyAssist.Types {
    public class LootResultKey : IEquatable<LootResultKey> {
        public uint ItemId;
        public bool IsHQ;

        public bool Equals(LootResultKey? other) {
            if(other is null) {
                return false;
            }
            return ItemId == other.ItemId && IsHQ == other.IsHQ;
        }

        public override int GetHashCode() {
            return 0;
        }
    }
}
