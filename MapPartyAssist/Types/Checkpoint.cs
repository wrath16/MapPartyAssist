using MapPartyAssist.Types.Attributes;
using System;

namespace MapPartyAssist.Types {
    [ValidatedDataType]
    public class Checkpoint : IEquatable<Checkpoint> {
        public string Name { get; init; }
        public string? Message { get; init; }
        //not used currently
        public int? MessageChannel { get; init; }

        public Checkpoint() {
            Name = "";
        }

        public Checkpoint(string name, string message = "", int messageChannel = 2105) {
            Name = name;
            Message = message;
            MessageChannel = messageChannel;
        }

        public bool Equals(Checkpoint? obj) {
            return obj != null && obj.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}
