using System.Collections.Generic;

namespace MapPartyAssist.Types {
    public class HiddenCanalsOfUznairResults : DutyResults {
        public HiddenCanalsOfUznairResults(string dutyName, Dictionary<string, MPAMember> players) : base(dutyName, players) {
            DutyName = "The Hidden Canals of Uznair";
        }

        static HiddenCanalsOfUznairResults() {
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
            Checkpoints.Add(new Checkpoint("Open final chamber", "The gate to the final chamber opens."));
            Checkpoints.Add(new Checkpoint("Clear final chamber", "Duty complete!"));
        }
    }
}
