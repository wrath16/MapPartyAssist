using System.Collections.Generic;

namespace MapPartyAssist.Types.REST.Universalis {
    public struct HistoryResponse {
        public Dictionary<int, ItemHistory> Items;
        public List<int> ItemIDs, UnresolvedItems;
    }

    public struct ItemHistory {
        public int ItemID;
        public float RegularSaleVelocity, NQSaleVelocity, HQSaleVelocity;
        public string RegionName;
        public long LastUploadTime;
        public List<SaleEntry> Entries;
    }

    public struct SaleEntry {
        public bool HQ, OnMannequin;
        public int Quantity, PricePerUnit, WorldID;
        public string BuyerName, WorldName;
        public long Timestamp;
    }

}
