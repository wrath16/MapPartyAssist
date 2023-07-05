using System;

namespace MapPartyAssist.Types {
    internal class Duty {

        public int DutyId { get; init; }
        public string Name { get; init; }
        public DutyStructure Structure { get; init; }
        public Type? ResultsType { get; init; }
        public Duty() {

        }
    }

    public enum DutyStructure {
        Doors,
        Roulette
    }
}
