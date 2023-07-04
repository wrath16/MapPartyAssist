using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class HiddenCanalsOfUznairResults : DutyResults {


        //putting this first...
        public HiddenCanalsOfUznairResults(int dutyId, string dutyName, Dictionary<string, MPAMember> players, string owner) : base(dutyId, dutyName, players, owner) {
        }

        static HiddenCanalsOfUznairResults() {
            //DutyName = "The Hidden Canals of Uznair";
            FailureCheckpoint = new Checkpoint("Failure", "The Hidden Canals of Uznair has ended.", 2105);
            //FailureMessage = "A trap is triggered! You are expelled from the area!";
            //setup checkpoints
            Checkpoints.Add(new Checkpoint("Clear 1st chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open 2nd chamber", "The gate to the 2nd chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 2nd chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open 3rd chamber", "The gate to the 3rd chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 3rd chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open 4th chamber", "The gate to the 4th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 4th chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open 5th chamber", "The gate to the 5th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 5th chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open 6th chamber", "The gate to the 6th chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear 6th chamber", "The cages are empty."));
            Checkpoints.Add(new Checkpoint("Open final chamber", "The gate to the final chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear final chamber", "The cages are empty."));
        }
    }
}
