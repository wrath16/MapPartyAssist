using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapPartyAssist.Types {
    public class MPAMember {
        public string Name { get; set; }
        public string HomeWorld { get; set; }
        public List<MPAMap> Maps { get; set; }
        public bool IsSelf { get; set; }
        public DateTime LastJoined { get; set; }
        public SeString MapLink { get; set; }

        public MPAMember(string name, string homeWorld, bool isSelf = false) {
            Name = name;
            HomeWorld = homeWorld;
            IsSelf = isSelf;
            LastJoined = DateTime.Now;
            Maps = new List<MPAMap>();
        }
    }
}
