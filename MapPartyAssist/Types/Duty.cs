using System;

namespace MapPartyAssist.Types {
    internal class Duty {

        public int DutyId { get; init; }
        public string Name { get; init; }
        public DutyStructure Structure { get; init; }
        public int ChamberCount { get; init; }
        public Type? ResultsType { get; init; }
        public Duty(int id, string name, DutyStructure structure, int chamberCount, Type resultsType = null) {
            DutyId = id;
            Name = name;
            Structure = structure;
            ChamberCount = chamberCount;
            ResultsType = resultsType;
        }
    }

    public enum DutyStructure {
        Doors,
        Roulette
    }
}
