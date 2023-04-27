using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MapPartyAssist.Types {
    public class MPAMap {
        public string Name { get; set; }
        public string Zone { get; set; }
        public DateTime Time { get; init; }
        public bool IsPending { get; set; }
        public bool IsManual { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsArchived { get; set; }
        //public bool IsArchived {
        //    get {
        //        if(!IsArchived) {
        //            DateTime dateTime = DateTime.Now;
        //            TimeSpan timeDiff = dateTime - Time;

        //            //if(timeDiff.TotalHours > )
        //        } else {
        //            return true;
        //        }
        //    }
        //    set {
        //        IsArchived = value;
        //    }
        //}
        public SeString? MapLink { get; set; }
        
        public MPAMap(string name, DateTime datetime, string zone = "", bool isPending = false, bool isManual = false) { 
            this.Name= name;
            this.Time = datetime;
            this.IsPending = isPending;
            this.IsManual = isManual;
            this.IsDeleted = false;
            this.IsArchived = false;
            this.Zone = zone;
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
