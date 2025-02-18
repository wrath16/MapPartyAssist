﻿using LiteDB;
using MapPartyAssist.Types.Attributes;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    [ValidatedDataType]
    public class MPAMember : IEquatable<MPAMember>, IEquatable<string> {
        public string Name { get; set; }
        public string HomeWorld { get; set; }
        [BsonIgnore]
        public List<MPAMap>? Maps { get; set; }
        public bool IsSelf { get; set; }
        public DateTime LastJoined { get; set; }
        public MPAMapLink? MapLink { get; set; }
        public MPAMapLink? PreviousMapLink { get; set; }
        [BsonId]
        public string Key => $"{Name} {HomeWorld}";

        [BsonIgnore]
        public string FirstName => Name.Split(" ")[0];

        public MPAMember(string name, string homeWorld, bool isSelf = false) {
            Name = name;
            HomeWorld = homeWorld;
            IsSelf = isSelf;
            LastJoined = DateTime.Now;
            Maps = new();
        }

        public MPAMember(string key, bool isSelf = false) {
            var split = key.Split(" ");
            Name = $"{split[0]} {split[1]}";
            HomeWorld = split[2];
            IsSelf = isSelf;
            LastJoined = DateTime.Now;
            Maps = new();
        }
        public bool Equals(MPAMember? other) {
            if(other == null) {
                return false;
            } else {
                return Key.Equals(other.Key);
            }
        }

        public bool Equals(string? other) {
            if(other == null) {
                return false;
            } else {
                return Key.Equals(other);
            }
        }

        public void SetMapLink(MPAMapLink? link) {
            if(MapLink != null) {
                PreviousMapLink = MapLink;
            }
            MapLink = link;
        }

        //public static bool operator ==(MPAMember? a, MPAMember? b) {
        //    if((object?)a == null && (object?)b == null) {
        //        return true;
        //    } else if((object?)a == null || (object?)b == null) {
        //        return false;
        //    } else {
        //        return a.Equals(b);
        //    }
        //}

        //public static bool operator !=(MPAMember? a, MPAMember? b) {
        //    return !(a == b);
        //    //if(a == null && b == null) {
        //    //    return false;
        //    //} else if(a == null || b == null) {
        //    //    return true;
        //    //} else {
        //    //    return !a!.Equals(b);
        //    //}
        //}
    }
}
