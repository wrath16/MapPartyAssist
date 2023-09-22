﻿using LiteDB;
using MapPartyAssist.Types.Attributes;
using System;

namespace MapPartyAssist.Types {
    [ValidatedDataType]
    public class Checkpoint : IEquatable<Checkpoint> {
        //effectively the Id
        public string Name { get; init; }
        public string? Message { get; init; }
        //not used currently
        public int? MessageChannel { get; init; }
        //whether this checkpoint's message must occur in order
        public bool IsSequential { get; init; }

        [BsonCtor]
        public Checkpoint() {
            Name = "";
            IsSequential = true;
        }

        public Checkpoint(string name, string message = "", bool isSequential = true, int messageChannel = 2105) {
            Name = name;
            Message = message;
            MessageChannel = messageChannel;
            IsSequential = isSequential;
        }

        public bool Equals(Checkpoint? obj) {
            return obj != null && obj.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}
