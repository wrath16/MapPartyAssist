using MapPartyAssist.Types;

namespace MapPartyAssist.Helper {
    internal static class DutyHelper {

        public static string GetSummonName(Summon summon) {
            return summon switch {
                Summon.Lesser => "Lesser",
                Summon.Greater => "Greater",
                Summon.Elder => "Elder",
                Summon.Gold => "Circle Shift",
                Summon.Silver => "Abomination",
                _ => "Unknown",
            };
        }

    }
}
