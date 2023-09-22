using LiteDB;
using MapPartyAssist.Types.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Types {

    [ValidatedDataType]
    public class DutyResults {
        [BsonId]
        [JsonIgnore]
        public ObjectId Id { get; set; }
        public int Version { get; set; } = 1;
        //could replace these two properties with Duty object...
        public int DutyId { get; init; }
        public string DutyName { get; set; }
        public DateTime Time { get; init; }
        public DateTime CompletionTime { get; set; }
        public string[] Players { get; set; }
        public string Owner { get; set; }
        [BsonRef("map")]
        [JsonIgnore]
        public MPAMap? Map { get; set; }
        public int TotalGil { get; set; } = 0;
        public bool IsComplete { get; set; }
        public bool IsPickup { get; set; }
        public bool IsEdited { get; set; }

        public List<CheckpointResults> CheckpointResults { get; set; } = new();

        [BsonCtor]
        public DutyResults() {
            Id = ObjectId.NewObjectId();
            Time = DateTime.Now;
            DutyName = "";
            Owner = "";
            Players = new string[0];
        }

        public DutyResults(int dutyId, string dutyName, Dictionary<string, MPAMember> players, string owner) {
            Players = players.Keys.ToArray();
            Owner = owner;
            Time = DateTime.Now;
            DutyId = dutyId;
            DutyName = dutyName.ToLower();
            Id = ObjectId.NewObjectId();
        }
    }
}
