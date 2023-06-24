namespace MapPartyAssist.Types {
    internal class RouletteCheckpoint : Checkpoint {

        enum Summon {
            Lesser,
            Greater,
            Elder,
            Silver,
            Gold
        }

        public RouletteCheckpoint(string name, string message, int messageChannel = 2105) : base(name, message, messageChannel) {
        }
    }
}
