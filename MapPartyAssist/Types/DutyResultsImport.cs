using LiteDB;
using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class DutyResultsImport {

        [BsonId]
        public ObjectId Id { get; set; }
        public int Version { get; set; } = 1;
        public bool IsDeleted { get; set; }
        public int DutyId { get; set; }
        public DateTime Time { get; set; }
        public DateTime CreationTime { get; set; }
        public string Player { get; set; }
        public uint TotalClears { get; set; }
        public uint TotalRuns { get; set; }
        public uint? TotalGil { get; set; }
        public List<uint>? CheckpointTotals { get; set; }
        public Dictionary<Summon, uint>? SummonTotals { get; set; }
        public List<uint>? ClearSequence { get; set; }
        public uint? RunsSinceLastClear { get; set; }

        public DutyResultsImport() {
        }

        public DutyResultsImport(int dutyId, DateTime time, uint totalClears, uint totalRuns, uint? totalGil = null, List<uint>? checkpointTotals = null, List<uint>? clearSequence = null, uint? runsSinceLastClear = null) {
            DutyId = dutyId;
            Time = time;
            TotalClears = totalClears;
            TotalRuns = totalRuns;
            TotalGil = totalGil;
            CheckpointTotals = checkpointTotals;
            ClearSequence = clearSequence;
            RunsSinceLastClear = runsSinceLastClear;
            CreationTime = DateTime.Now;
            Id = ObjectId.NewObjectId();
        }
    }
}
