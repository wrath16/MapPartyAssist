using LiteDB;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class MPAMember : IEquatable<MPAMember>, IEquatable<string> {
        public string Name { get; set; }
        public string HomeWorld { get; set; }
        [BsonIgnore]
        public List<MPAMap> Maps { get; set; }
        public bool IsSelf { get; set; }
        public DateTime LastJoined { get; set; }
        public MPAMapLink? MapLink { get; set; }
        [BsonId]
        public string Key {
            get {
                return $"{Name} {HomeWorld}";
            }
        }

        public MPAMember(string name, string homeWorld, bool isSelf = false) {
            Name = name;
            HomeWorld = homeWorld;
            IsSelf = isSelf;
            LastJoined = DateTime.Now;
            Maps = new List<MPAMap>();
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

        //public static bool operator ==(MPAMember? a, MPAMember? b) {
        //    if(a == null && b == null) {
        //        return true;
        //    } else {
        //        return a!.Equals(b);
        //    }
        //}

        //public static bool operator !=(MPAMember? a, MPAMember? b) {
        //    if(a == null && b == null) {
        //        return false;
        //    } else {
        //        return !a!.Equals(b);
        //    }
        //}
    }
}
