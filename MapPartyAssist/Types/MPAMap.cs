using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;
using System;

namespace MapPartyAssist.Types {
    public class MPAMap {
        public string Name { get; set; }
        public string Zone { get; set; }
        public DateTime Time { get; init; }
        public bool IsPortal { get; set; }
        public string DutyName { get; set; }
        [JsonIgnore]
        public bool IsPending { get; set; }
        public bool IsManual { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsArchived { get; set; }
        public SeString? MapLink { get; set; }
        [JsonIgnore]
        public DutyResults? Results { get; set; }

        public MPAMap(string name, DateTime datetime, string zone = "", bool isManual = false, bool isPortal = false) {
            Name = name;
            Time = datetime;
            IsPortal = isPortal;
            IsManual = isManual;
            IsDeleted = false;
            IsArchived = false;
            Zone = zone;
        }

        //public static MapType NameToType(string name) {

        //}
    }

    enum MapType {
        Leather,
        Goatskin,
        Toadskin,
        Boarskin,
        Peisteskin,
        Unhidden,
        Archaeoskin,
        Wyvernskin,
        Dragonskin,
        Gaganaskin,
        Gazelleskin,
        Gliderskin,
        Zonureskin,
        Saigaskin,
        Kumbhiraskin,
        Ophiotauroskin,
    }
}
