using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Types {

    //class can't be abstract since we are serializing it
    public class DutyResults {

        //[JsonIgnore]
        //public virtual List<Checkpoint> Checkpoints { get; init; }
        //[JsonIgnore]
        //public virtual Checkpoint FailureCheckpoint { get; init; }
        //public static string DutyName = "";

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
        public string Owner { get; set; } = "";
        [BsonRef("map")]
        [JsonIgnore]
        public MPAMap? Map { get; set; }
        public int TotalGil { get; set; } = 0;
        public bool IsComplete { get; set; }
        public bool IsPickup { get; set; }
        public bool IsEdited { get; set; }

        public List<CheckpointResults> CheckpointResults { get; set; } = new();


        //[JsonConstructor]
        //public DutyResults(int dutyId, string[] players, string owner) {
        //    Players = players;
        //    Owner = owner;
        //    Time = DateTime.Now;
        //    DutyId = dutyId;
        //}

        public DutyResults() {
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
