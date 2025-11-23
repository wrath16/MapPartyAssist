using LiteDB;

namespace MapPartyAssist.Types {

    public enum SlotSummon {
        Lesser,
        Greater,
        Elder,
        Final,
        Special,
    }

    public class SlotsCheckpointResults : CheckpointResults {

        [BsonCtor]
        public SlotsCheckpointResults() : base() {

        }

        public SlotsCheckpointResults(Checkpoint checkpoint, Summon? summon, string? enemy, bool isSaved = false, bool isReached = false) : base(checkpoint, isReached) {
            SummonType = summon;
            MonsterName = enemy;
            IsSaved = isSaved;
        }
    }
}
