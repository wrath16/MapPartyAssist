using System;

namespace MapPartyAssist.Types {
    public class CheckpointResults {
        public Checkpoint Checkpoint { get; init; }
        public DateTime Time { get; set; }
        public bool IsReached { get; set; }

        //these properties should only be on RouletteCheckpoint, but we need them here for deserialization
        public Summon? SummonType { get; set; }
        public string? MonsterName { get; set; }
        public bool IsSaved { get; set; }

        public CheckpointResults() {

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
