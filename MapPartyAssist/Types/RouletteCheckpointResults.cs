namespace MapPartyAssist.Types {

    public enum Summon {
        Lesser,
        Greater,
        Elder,
        Silver,
        Gold
    }

    internal class RouletteCheckpointResults : CheckpointResults {

        public RouletteCheckpointResults() {

        }

        public RouletteCheckpointResults(Checkpoint checkpoint, Summon? summon, string? enemy, bool isSaved = false, bool isReached = false) : base(checkpoint, isReached) {
            SummonType = summon;
            MonsterName = enemy;
            IsSaved = isSaved;
        }
    }
}
