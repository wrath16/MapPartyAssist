using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class LostCanalsOfUznairResults : DutyResults {
        public LostCanalsOfUznairResults(int dutyId, Dictionary<string, MPAMember> players, string owner) : base(dutyId, players, owner) {
            //DutyName = "The Lost Canals of Uznair";
        }

        static LostCanalsOfUznairResults() {
            DutyName = "The Lost Canals of Uznair";
            FailureCheckpoint = new Checkpoint("Failure", "The Lost Canals of Uznair has ended.", 2105);
            //setup checkpoints
            Checkpoints.Add(new Checkpoint("Clear 1st chamber", "The First Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 2nd chamber", "The Second Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 3rd chamber", "The Third Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 4th chamber", "The Fourth Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 5th chamber", "The Fifth Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 6th chamber", "The Sixth Sluice is no longer sealed!"));
            Checkpoints.Add(new Checkpoint("Open final chamber", "The gate to the final chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear final chamber", "The Seventh Sluice is no longer sealed!"));
        }
    }
}
