using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using LiteDB;
using MapPartyAssist.Types.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    [ValidatedDataType]
    public class MPAMap : IEquatable<MPAMap> {
        public static int CurrentVersion = 1;

        [BsonId]
        [JsonIgnore]
        public ObjectId Id { get; set; }
        //nullable for backwards compatibility
        public int? Version { get; set; }
        //set to nullable since it is referenced in plugin constructor
        public string? Owner { get; set; }
        public string Name { get; set; }
        public string Zone { get; set; }
        public DateTime Time { get; init; }
        public bool IsPortal { get; set; }
        public string? DutyName { get; set; }
        [BsonIgnore]
        public bool IsPending { get; set; }
        public bool IsManual { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsArchived { get; set; }
        public bool IsAmbiguousOwner { get; set; }
        public bool IsReassigned { get; set; }
        [BsonIgnore]
        public SeString? MapLink { get; set; }
        public List<LootResult>? LootResults { get; set; }
        public string[]? Players { get; set; }
        public int? TerritoryId { get; set; }
        public bool IsEdited { get; set; }
        public uint? EventItemId { get; set; }
        public TreasureMap? MapType { get; set; }
        public bool IsInferred { get; set; }
        public int? DutyId { get; set; }


        //this will cause a circular ref with DutyResults -_-
        //[BsonRef("dutyresults")]
        [JsonIgnore]
        [BsonIgnore]
        public DutyResults? DutyResults { get; set; }

        public MPAMap() {
            Id = ObjectId.NewObjectId();
            Name = "";
            Owner = "";
            Zone = "";
            Version = CurrentVersion;
        }

        //use this constructor for reading from database to preserve null versions
        [BsonCtor]
        [Obsolete("For database usage only!")]
        public MPAMap(ObjectId id) {
            Id = id;
            Name = "";
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
            Version = CurrentVersion;
        }

        public bool Equals(MPAMap? other) {
            if(Id == null || other == null || other.Id == null) {
                return false;
            } else {
                return Id.Equals(other.Id);
            }
        }

        //finds the first loot result with no recipient yet for the given itemId
        public LootResult? GetMatchingLootResult(uint itemId, bool isHQ, int quantity) {
            if(LootResults is null) {
                throw new InvalidOperationException("Loot results not initialized!");
            }
            foreach(var lootResult in LootResults) {
                if(!lootResult.Recipient.IsNullOrEmpty()) {
                    continue;
                }
                if(lootResult.ItemId == itemId && lootResult.IsHQ == isHQ && lootResult.Quantity == quantity) {
                    return lootResult;
                }
            }
            return null;
        }
    }

    public enum TreasureMap {
        Unknown,
        Leather,
        Goatskin,
        Toadskin,
        Boarskin,
        Peisteskin,
        Alexandrite,
        Unhidden,
        Archaeoskin,
        Wyvernskin,
        Dragonskin,
        Gaganaskin,
        Gazelleskin,
        Thief,
        SeeminglySpecial,
        Gliderskin,
        Zonureskin,
        OstensiblySpecial,
        Saigaskin,
        Kumbhiraskin,
        Ophiotauroskin,
        PotentiallySpecial,
        ConceivablySpecial,
        Loboskin,
        Braaxskin,
        Gargantuaskin,
    }

    public enum TreasureMapCategory {
        Unknown,
        ARealmReborn,
        Heavensward,
        Stormblood,
        Shadowbringers,
        Endwalker,
        Elpis,
        Dawntrail
    }
}
