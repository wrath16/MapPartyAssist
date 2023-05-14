using Dalamud.Game.Text.SeStringHandling.Payloads;
using Newtonsoft.Json;

namespace MapPartyAssist.Types {
    public class MPAMapLinkPayload : MapLinkPayload {


        [JsonProperty]
        public int RawX { get; set; }


        [JsonProperty]
        public int RawY { get; set; }

        [JsonConstructor]
        MPAMapLinkPayload(uint territoryTypeId, uint mapId, int rawX, int rawY) : base(territoryTypeId, mapId, rawX, rawY) {
            RawX = rawX;
            RawY = rawY;
        }
    }
}
