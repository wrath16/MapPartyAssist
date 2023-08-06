using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using LiteDB;
using Lumina.Excel.GeneratedSheets;

namespace MapPartyAssist.Types {
    public class MPAMapLink {
        public int RawX { get; set; }
        public int RawY { get; set; }
        public uint TerritoryTypeId { get; set; }
        public uint MapId { get; set; }

        [BsonIgnore]
        private MapLinkPayload _mapLinkPayload;

        public MPAMapLink() {
        }

        public MPAMapLink(uint territoryTypeId, uint mapId, int rawX, int rawY) {
            RawX = rawX;
            RawY = rawY;
            TerritoryTypeId = territoryTypeId;
            MapId = mapId;
            _mapLinkPayload = new MapLinkPayload(territoryTypeId, mapId, rawX, rawY);
        }

        public MPAMapLink(MapLinkPayload mapLinkPayload) {
            RawX = mapLinkPayload.RawX;
            RawY = mapLinkPayload.RawY;
            TerritoryTypeId = mapLinkPayload.TerritoryType.RowId;
            MapId = mapLinkPayload.Map.RowId;
            _mapLinkPayload = mapLinkPayload;
        }

        public MapLinkPayload GetMapLinkPayload() {
            return _mapLinkPayload == null ? new MapLinkPayload(TerritoryTypeId, MapId, RawX, RawY) : _mapLinkPayload;
        }
    }
}
