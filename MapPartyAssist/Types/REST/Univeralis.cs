using System.Collections.Generic;

namespace MapPartyAssist.Types.REST.Universalis {
    public struct HistoryResponse {
        public Dictionary<uint, ItemHistory> Items;
        public List<uint> ItemIDs, UnresolvedItems;
    }

    public struct ItemHistory {
        public uint ItemID;
        public float RegularSaleVelocity, NQSaleVelocity, HQSaleVelocity;
        public string RegionName;
        public long LastUploadTime;
        public List<SaleEntry> Entries;
    }

    public struct SaleEntry {
        public bool HQ, OnMannequin;
        public uint Quantity, PricePerUnit, WorldID;
        public string BuyerName, WorldName;
        public long Timestamp;
    }

}
