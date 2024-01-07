using MapPartyAssist.Types;

namespace MapPartyAssist.Helper {
    internal static class PlayerHelper {

        public static Region GetRegion(byte? regionByte) {
            return regionByte switch {
                1 => Region.Japan,
                2 => Region.NorthAmerica,
                3 => Region.Europe,
                4 => Region.Oceania,
                _ => Region.Unknown,
            };
        }
    }
}
