using LiteDB;
using MapPartyAssist.Types.Attributes;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    [ValidatedDataType]
    public class CheckpointResults {
        public Checkpoint Checkpoint { get; set; }
        public DateTime Time { get; set; }
        public bool IsReached { get; set; }

        //these properties should only be on RouletteCheckpointResults, but we need them here for deserialization
        public Summon? SummonType { get; set; }
        public string? MonsterName { get; set; }
        public bool IsSaved { get; set; }

        public List<LootResult>? LootResults { get; set; }

        [BsonCtor]
        public CheckpointResults() {
            Checkpoint = new Checkpoint("");
            Time = DateTime.Now;
        }

        public CheckpointResults(Checkpoint checkpoint, bool isReached = false) {
            Checkpoint = checkpoint;
            IsReached = isReached;
            if(IsReached) {
                Time = DateTime.Now;
            }
        }
    }
}
