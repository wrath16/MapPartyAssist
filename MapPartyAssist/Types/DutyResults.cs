using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Types {

    //class can't be abstract since we are serializing it
    public class DutyResults {

        public static List<Checkpoint> Checkpoints = new();
        public static Checkpoint FailureCheckpoint;
        //public static string DutyName = "";

        public int Version { get; } = 1;

        public int DutyId { get; init; }
        public string DutyName { get; set; }
        public DateTime Time { get; init; }
        public DateTime CompletionTime { get; set; }
        public string[] Players { get; init; }
        public string Owner { get; set; }
        public MPAMap Map { get; set; }
        public List<CheckpointResults> CheckpointResults = new();
        public int TotalGil { get; set; } = 0;
        public bool IsComplete { get; set; }

        private bool _isComplete = false;

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
        }

        //returns true if changes were made
        public virtual bool ProcessChat(XivChatType type, uint senderId, SeString sender, SeString message) {
            if((int)type == 62) {
                //check for gil obtained
                Match m = Regex.Match(message.ToString(), @"(?<=You obtain )[\d,\.]+(?= gil)");
                if(m.Success) {
                    string parsedGilString = m.Value.Replace(",", "").Replace(".", "");
                    TotalGil += int.Parse(parsedGilString);
                }
                return true;
            }

            //add 2233 for self!

            var nextCheckpoint = Checkpoints[CheckpointResults.Count];
            if((int)type ==  2233 || (int)type == 2105 && nextCheckpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                CheckpointResults.Add(new CheckpointResults(nextCheckpoint, true));
                //if all checkpoints reached, set to duty complete
                if(CheckpointResults.Where(cr => cr.IsReached).Count() == Checkpoints.Count) {
                    IsComplete = true;
                }
                return true;
            }

            //foreach(var checkpoint in Checkpoints) {
            //    if(checkpoint.MessageChannel == (int)type && checkpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
            //        CheckpointResults.Add(new CheckpointResults(checkpoint, true));
            //        //if all checkpoints reached, set to duty complete
            //        if(CheckpointResults.Where(cr => cr.IsReached).Count() == Checkpoints.Count) {
            //            IsComplete = true;
            //        }
            //        return true;
            //    }
            //}

            //check for failure
            if((int) type == FailureCheckpoint.MessageChannel && FailureCheckpoint.Message.Equals(message.ToString(), StringComparison.OrdinalIgnoreCase)) {
                //CheckpointResults.Add(new CheckpointResults(nextCheckpoint, false));
                IsComplete = true;
                return true;
                //CheckpointResults.Add(new CheckpointResults(FailureCheckpoint, false));
            }
            return false;
        }
    }
}
