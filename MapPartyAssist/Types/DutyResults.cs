using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Types {
    public class DutyResults {

        public static List<Checkpoint> Checkpoints = new();
        public string DutyName { get; init; }
        public DateTime Time { get; init; }
        public string[] Players { get; init; }
        public List<CheckpointResults> CheckpointResults = new();

        public DutyResults(string dutyName, Dictionary<string, MPAMember> players) {
            DutyName = dutyName;
            Players = players.Keys.ToArray();
            Time = DateTime.Now;
        }

        public virtual void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            foreach(var checkpoint in Checkpoints) {
                if(checkpoint.MessageChannel == (int)type && checkpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                    CheckpointResults.Add(new CheckpointResults(checkpoint, true));
                    break;
                }
            }
        }
    }
}
