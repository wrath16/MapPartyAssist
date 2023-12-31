using MapPartyAssist.Types;
using System.Collections.Generic;

namespace MapPartyAssist.Helper {
    internal static class MapHelper {

        public static Dictionary<uint, TreasureMap> IdToMapTypeMap = new() {
            {2000297,  TreasureMap.Leather},
            {2001088,  TreasureMap.Goatskin},
            {2001089,  TreasureMap.Toadskin},
            {2001090,  TreasureMap.Boarskin},
            {2001091,  TreasureMap.Peisteskin},
            {2001223,  TreasureMap.Alexandrite},
            {2001352,  TreasureMap.Unhidden},
            {2001762,  TreasureMap.Archaeoskin},
            {2001763,  TreasureMap.Wyvernskin},
            {2001764,  TreasureMap.Dragonskin},
            {2002209,  TreasureMap.Gaganaskin},
            {2002210,  TreasureMap.Gazelleskin},
            {2002260,  TreasureMap.Thief},
            {2002503,  TreasureMap.SeeminglySpecial},
            {2002663,  TreasureMap.Gliderskin},
            {2002664,  TreasureMap.Zonureskin},
            {2003075,  TreasureMap.OstensiblySpecial},
            {2003245,  TreasureMap.Saigaskin},
            {2003246,  TreasureMap.Kumbhiraskin},
            {2003455,  TreasureMap.PotentiallySpecial},
            {2003457,  TreasureMap.Ophiotauroskin},
            {2003463,  TreasureMap.ConceivablySpecial},
        };

        public static string GetMapName(TreasureMap map) {
            return map switch {
                TreasureMap.Leather => "Leather Treasure Map",
                TreasureMap.Goatskin => "Goatskin Treasure Map",
                TreasureMap.Toadskin => "Toadskin Treasure Map",
                TreasureMap.Boarskin => "Boarskin Treasure Map",
                TreasureMap.Peisteskin => "Peisteskin Treasure Map",
                TreasureMap.Alexandrite => "Alexandrite Treasure Map",
                TreasureMap.Unhidden => "Leather Buried Treasure Map",
                TreasureMap.Archaeoskin => "Archaeoskin Treasure Map",
                TreasureMap.Wyvernskin => "Wyvernskin Treasure Map",
                TreasureMap.Dragonskin => "Dragonskin Treasure Map",
                TreasureMap.Gaganaskin => "Gaganaskin Treasure Map",
                TreasureMap.Gazelleskin => "Gazelleskin Treasure Map",
                TreasureMap.Thief => "Fabled Thief's Map",
                TreasureMap.SeeminglySpecial => "Seemingly Special Treasure Map",
                TreasureMap.Gliderskin => "Gliderskin Treasure Map",
                TreasureMap.Zonureskin => "Zonureskin Treasure Map",
                TreasureMap.OstensiblySpecial => "Ostensibly Special Treasure Map",
                TreasureMap.Saigaskin => "Saigaskin Treasure Map",
                TreasureMap.Kumbhiraskin => "Kumbhiraskin Treasure Map",
                TreasureMap.Ophiotauroskin => "Ophiotauroskin Treasure Map",
                TreasureMap.PotentiallySpecial => "Potentially Special Treasure Map",
                TreasureMap.ConceivablySpecial => "Conceivably Special Treasure Map",
                _ => "Unknown"
            };
        }

        public static string GetCategoryName(TreasureMapCategory category) {
            return category switch {
                TreasureMapCategory.ARealmReborn => "A Realm Reborn",
                TreasureMapCategory.Heavensward => "Heavensward",
                TreasureMapCategory.Stormblood => "Stormblood",
                TreasureMapCategory.Shadowbringers => "Shadowbringers",
                TreasureMapCategory.Endwalker => "Endwalker",
                TreasureMapCategory.Elpis => "Elpis",
                TreasureMapCategory.Unknown => "Unknown/Unrecorded",
            };
        }

        public static TreasureMapCategory GetCategory(int territoryId) {

            if(territoryId == 0) {
                return TreasureMapCategory.Unknown;
            } else if(territoryId < 397) {
                return TreasureMapCategory.ARealmReborn;
            } else if(territoryId < 612) {
                return TreasureMapCategory.Heavensward;
            } else if(territoryId < 812) {
                return TreasureMapCategory.Stormblood;
            } else if(territoryId < 956) {
                return TreasureMapCategory.Shadowbringers;
            } else if(territoryId == 961) {
                return TreasureMapCategory.Elpis;
            } else {
                return TreasureMapCategory.Endwalker;
            }
            //switch(territoryId) {
            //    case 612:
            //    case 613:
            //    case 614:
            //    case 620:
            //    case 621:
            //    case 622:
            //        return TreasureMapCategory.Stormblood;
            //    case 813:
            //    case 814:
            //    case 815:
            //    case 816:
            //    case 817:
            //    case 818:
            //        return TreasureMapCategory.Shadowbringers;
            //    case 956:
            //    case 957:
            //    case 958:
            //    case 959:
            //    case 960:
            //        return TreasureMapCategory.Endwalker;
            //    case 961: 
            //        return TreasureMapCategory.Elpis;
            //    default:
            //        return TreasureMapCategory.Unknown;
            //}
        }
    }
}
