using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Types {
    public class Duty {
        public int DutyId { get; init; }
        public string Name { get; init; }
        public DutyStructure Structure { get; init; }
        public int ChamberCount { get; init; }
        //not used
        public Type? ResultsType { get; init; }
        public List<Checkpoint>? Checkpoints { get; init; }
        public Checkpoint? FailureCheckpoint { get; init; }
        public string[]? LesserSummons { get; init; }
        public string[]? GreaterSummons { get; init; }
        public string[]? ElderSummons { get; init; }
        public string[]? FinalSummons { get; init; }

        public Duty(int id, string name, DutyStructure structure, int chamberCount, List<Checkpoint>? checkpoints = null, Checkpoint? failureCheckpoint = null, string[]? lesserSummons = null, string[]? greaterSummons = null, string[]? elderSummons = null, string[]? finalSummons = null) {
            DutyId = id;
            Name = name;
            Structure = structure;
            ChamberCount = chamberCount;
            Checkpoints = checkpoints;
            FailureCheckpoint = failureCheckpoint;
            LesserSummons = lesserSummons;
            GreaterSummons = greaterSummons;
            ElderSummons = elderSummons;
            FinalSummons = finalSummons;
        }

        public string GetSummonPatternString(Summon summonType) {
            List<string> summonList;
            switch(summonType) {
                case Summon.Lesser:
                    if(LesserSummons == null) {
                        throw new InvalidOperationException("Duty missing lesser summons");
                    }
                    summonList = LesserSummons.ToList();
                    break;
                case Summon.Greater:
                    if(GreaterSummons == null) {
                        throw new InvalidOperationException("Duty missing greater summons");
                    }
                    summonList = GreaterSummons.ToList();
                    break;
                case Summon.Elder:
                    if(ElderSummons == null) {
                        throw new InvalidOperationException("Duty missing elder summons");
                    }
                    summonList = ElderSummons.ToList();
                    if(FinalSummons != null) {
                        summonList = summonList.Concat(FinalSummons).ToList();
                    }
                    break;
                default:
                    return "";
            }

            string pattern = "(";
            for(int i = 0; i < summonList.Count; i++) {
                pattern += summonList[i];
                if(i == summonList.Count - 1) {
                    pattern += ")";
                } else {
                    pattern += "|";
                }
            }
            return pattern;
        }

        public string GetDisplayName() {
            string displayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Name);
            //re-lowercase 'of'
            displayName = Regex.Replace(displayName, @"(?<!^)\bof\b", "of", RegexOptions.IgnoreCase);
            return displayName;
        }
    }

    public enum DutyStructure {
        Doors,
        Roulette
    }
}
