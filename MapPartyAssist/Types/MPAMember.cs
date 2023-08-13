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
        public MPAMapLink MapLink { get; set; }
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
            return other != null && Key.Equals(other.Key);
        }

        public bool Equals(string? other) {
            return other != null && Key.Equals(other);
        }
    }
}
