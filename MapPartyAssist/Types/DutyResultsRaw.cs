using Dalamud.Game;
using LiteDB;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    internal class DutyResultsRaw {

        [BsonId]
        public ObjectId Id { get; set; }
        public DateTime Time { get; set; }
        public int DutyId { get; init; }
        public bool IsComplete { get; set; }
        public bool IsParsed { get; set; }
        public ClientLanguage Language { get; set; }
        public List<Message> Messages { get; set; }
        [BsonRef("map")]
        public MPAMap? Map { get; set; }
        public string? Owner { get; set; }
        public string[] Players { get; set; }

        public DutyResultsRaw() {
            Id = new();
            Time = DateTime.UtcNow;
            Messages = new();
        }
    }
}
