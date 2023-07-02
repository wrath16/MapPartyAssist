using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapPartyAssist.Types {
    internal class ShiftingAltarsOfUznairResults : DutyResults {

        public ShiftingAltarsOfUznairResults(int dutyId, string dutyName, Dictionary<string, MPAMember> players, string owner) : base(dutyId, dutyName, players, owner) {
            //DutyName = "The Hidden Canals of Uznair";
        }

        static ShiftingAltarsOfUznairResults() {
            //DutyName = "The Shifting Altars of Uznair";
            FailureCheckpoint = new Checkpoint("Failure", "The Shifting Altars of Uznair has ended.", 2105);
            //FailureMessage = "A trap is triggered! You are expelled from the area!";
            //setup checkpoints
            Checkpoints.Add(new Checkpoint("Complete 1st Summon"));
            Checkpoints.Add(new Checkpoint("Defeat 1st Summon"));
            Checkpoints.Add(new Checkpoint("Complete 2nd Summon"));
            Checkpoints.Add(new Checkpoint("Defeat 2nd Summon"));
            Checkpoints.Add(new Checkpoint("Complete 3rd Summon"));
            Checkpoints.Add(new Checkpoint("Defeat 3rd Summon"));
            Checkpoints.Add(new Checkpoint("Complete 4th Summon"));
            Checkpoints.Add(new Checkpoint("Defeat 4th Summon"));
            Checkpoints.Add(new Checkpoint("Complete final Summon"));
            Checkpoints.Add(new Checkpoint("Defeat final Summon"));
        }

        public override bool ProcessChat(XivChatType type, uint senderId, SeString sender, SeString message) {
            if((int)type == 62) {
                //check for gil obtained
                Match m = Regex.Match(message.ToString(), @"(?<=You obtain )[\d,\.]+(?= gil)", RegexOptions.IgnoreCase);
                if(m.Success) {
                    string parsedGilString = m.Value.Replace(",", "").Replace(".", "");
                    TotalGil += int.Parse(parsedGilString);
                }
                return true;
            } else if((int) type == 2105) {
                //check for circles shift
                Match shiftMatch = Regex.Match(message.ToString(), @"(?<=The circles shift and (a |an )?)(the great gold whisker|altar airavata|altar mandragora|altar manticore|altar apanda|altar diresaur)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(shiftMatch.Success) {
                    AddCheckpointResults(Summon.Gold, shiftMatch.Value);
                    return true;
                }
                //check for special summon
                Match specialMatch = Regex.Match(message.ToString(), @"^The summon retreats into the shadows.$", RegexOptions.IgnoreCase);
                if(specialMatch.Success) {
                    AddCheckpointResults(Summon.Silver);
                    return true;
                }
                //check for lesser summon
                Match lesserMatch = Regex.Match(message.ToString(), @"(Hati|altar chimera|altar beast|altar dullahan|altar skatene|altar totem)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(lesserMatch.Success) {
                    AddCheckpointResults(Summon.Lesser, lesserMatch.Value);
                    return true;
                }
                //check for greater summon
                Match greaterMatch = Regex.Match(message.ToString(), @"(The Winged|The Older One|altar kelpie|altar arachne)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(greaterMatch.Success) {
                    AddCheckpointResults(Summon.Greater, greaterMatch.Value);
                    return true;
                }
                //check for elder summon
                Match elderMatch = Regex.Match(message.ToString(), @"(the great gold whisker|altar airavata|altar mandragora|altar manticore|altar apanda|altar diresaur)(?=,? appears?)", RegexOptions.IgnoreCase);
                if(elderMatch.Success) {
                    AddCheckpointResults(Summon.Elder, elderMatch.Value);
                    return true;
                }
                //enemy defeated
                if(Regex.IsMatch(message.ToString(), @"^The summon is dispelled.$", RegexOptions.IgnoreCase)) {
                    AddCheckpointResults(null);
                    return true;
                }
            }
            //failure
            if((int)type == FailureCheckpoint.MessageChannel && FailureCheckpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                IsComplete = true;
                return true;
            }
            return false;
        }

        private void AddCheckpointResults(Summon? summon, string? monsterName = null) {
            var size = CheckpointResults.Count;
            CheckpointResults.Add(new RouletteCheckpointResults(Checkpoints[size], summon, monsterName, true));
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).SummonType = summon;
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).Enemy = enemy;
        }
    }
}
