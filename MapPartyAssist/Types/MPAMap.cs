using Dalamud.Game.Text.SeStringHandling;
using LiteDB;
using Newtonsoft.Json;
using System;

namespace MapPartyAssist.Types {
    public class MPAMap : IEquatable<MPAMap> {

        [BsonId]
        [JsonIgnore]
        public ObjectId Id { get; set; }
        //set to nullable since it is referenced in plugin constructor
        public string? Owner { get; set; }
        public string Name { get; set; }
        public string Zone { get; set; }
        public DateTime Time { get; init; }
        public bool IsPortal { get; set; }
        public string? DutyName { get; set; }
        [JsonIgnore]
        public bool IsPending { get; set; }
        public bool IsManual { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsArchived { get; set; }
        public SeString? MapLink { get; set; }
        //this will cause stack overflow due to infinite recursion with ref on DutyResults -_-
        //[BsonRef("dutyresults")]
        [JsonIgnore]
        [BsonIgnore]
        public DutyResults? DutyResults { get; set; }

        [BsonCtor]
        public MPAMap() {
            Id = ObjectId.NewObjectId();
            Name = "";
            Owner = "";
            Zone = "";
        }

        public MPAMap(string name, DateTime datetime, string owner, string zone = "", bool isManual = false, bool isPortal = false) {
            Name = name;
            Time = datetime;
            Owner = owner;
            IsPortal = isPortal;
            IsManual = isManual;
            IsDeleted = false;
            IsArchived = false;
            Zone = zone;
            Id = ObjectId.NewObjectId();
        }

        public bool Equals(MPAMap? other) {
            if(Id == null || other == null || other.Id == null) {
                return false;
            } else {
                return Id.Equals(other.Id);
            }
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
