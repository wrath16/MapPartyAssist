using System;
using System.Collections.Generic;

namespace MapPartyAssist.Types {
    internal class Duty {

        public int DutyId { get; init; }
        public string Name { get; init; }
        public DutyStructure Structure { get; init; }
        public int ChamberCount { get; init; }
        public Type? ResultsType { get; init; }
        public List<Checkpoint>? Checkpoints { get; init; }
        public Checkpoint? FailureCheckpoint { get; init; }

        public Duty(int id, string name, DutyStructure structure, int chamberCount, List<Checkpoint>? checkpoints = null, Checkpoint? failureCheckpoint = null, Type? resultsType = null) {
            DutyId = id;
            Name = name;
            Structure = structure;
            ChamberCount = chamberCount;
            Checkpoints = checkpoints;
            FailureCheckpoint = failureCheckpoint;
            ResultsType = resultsType;
        }
    }

    public enum DutyStructure {
        Doors,
        Roulette
    }
}
