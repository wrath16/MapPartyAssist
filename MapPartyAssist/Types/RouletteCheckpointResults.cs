using LiteDB;

namespace MapPartyAssist.Types {

    public enum Summon {
        Lesser,
        Greater,
        Elder,
        Silver,
        Gold
    }

    public class RouletteCheckpointResults : CheckpointResults {

        [BsonCtor]
        public RouletteCheckpointResults() : base() {

        }

        public RouletteCheckpointResults(Checkpoint checkpoint, Summon? summon, string? enemy, bool isSaved = false, bool isReached = false) : base(checkpoint, isReached) {
            SummonType = summon;
            MonsterName = enemy;
            IsSaved = isSaved;
        }
    }
}
