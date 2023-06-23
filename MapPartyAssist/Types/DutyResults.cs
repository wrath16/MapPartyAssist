using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Types {
    public abstract class DutyResults {

        public static List<Checkpoint> Checkpoints = new();
        public static string DutyName = "";
        public static string FailureMessage;
        public DateTime Time { get; init; }
        public string[] Players { get; init; }
        public string Owner { get; set; }
        public List<CheckpointResults> CheckpointResults = new();
        public int TotalGil { get; private set; } = 0;
        public bool IsComplete { get; set; }

        public DutyResults(Dictionary<string, MPAMember> players, string owner) {
            Players = players.Keys.ToArray();
            Owner = owner;
            Time = DateTime.Now;
        }

        public virtual void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if((int)type == 62) {
                //check for gil obtained
                Match m = Regex.Match(message.ToString(), @"(?<=You obtain )[\\d,\\.]+(?= gil)");
                if(m.Success) {
                    string parsedGilString = m.Value.Replace(",", "").Replace(".", "");
                    int gilAmount = int.Parse(parsedGilString);
                    TotalGil += gilAmount;
                }
                return;
            }

            foreach(var checkpoint in Checkpoints) {
                if(checkpoint.MessageChannel == (int)type && checkpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                    CheckpointResults.Add(new CheckpointResults(checkpoint, true));
                    break;
                }
            }
        }
    }
}
