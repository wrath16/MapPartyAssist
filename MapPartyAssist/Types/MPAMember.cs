using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class MPAMember {
        public string Name { get; set; }
        public string HomeWorld { get; set; }
        public List<MPAMap> Maps { get; set; }
        public bool IsSelf { get; set; }
        public DateTime LastJoined { get; set; }
        public MapLinkPayload MapLink { get; set; }

        public MPAMember(string name, string homeWorld, bool isSelf = false) {
            Name = name;
            HomeWorld = homeWorld;
            IsSelf = isSelf;
            LastJoined = DateTime.Now;
            Maps = new List<MPAMap>();
        }
    }
}
