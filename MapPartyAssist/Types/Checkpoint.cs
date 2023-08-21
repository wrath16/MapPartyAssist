namespace MapPartyAssist.Types {
    public class Checkpoint {
        public string Name { get; init; }
        public string Message { get; init; }
        public int MessageChannel { get; init; }

        public Checkpoint(string name, string message = "", int messageChannel = 2105) {
            Name = name;
            Message = message;
            MessageChannel = messageChannel;
        }

        public override bool Equals(object obj) {
            return Equals(obj as Checkpoint);
        }

        public bool Equals(Checkpoint obj) {
            return obj != null && obj.Name == Name;
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}
