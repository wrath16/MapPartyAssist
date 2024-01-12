using LiteDB;
using System;

namespace MapPartyAssist.Types {
    internal class PriceCheck {
        [BsonId]
        public ObjectId Id { get; set; }
        public uint ItemId { get; set; }
        public uint NQPrice { get; set; }
        public uint HQPrice { get; set; }
        public DateTime LastChecked { get; set; }
        public Region Region { get; set; }
        public PriceCheck() {
            Id = new ObjectId();
        }
    }
}
