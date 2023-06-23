namespace MapPartyAssist.Types {
    public class Checkpoint {
        public string Name { get; init; }
        public string Message { get; init; }
        public int MessageChannel { get; init; }

        public Checkpoint(string name, string message, int messageChannel = 2105) {
            Name = name;
            Message = message;
            MessageChannel = messageChannel;
        }
    }
}
