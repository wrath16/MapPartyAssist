using MapPartyAssist.Types;
using System.Linq;

namespace MapPartyAssist.Services {
    //internal class for handling duty results imports
    internal class ImportManager {
        private Plugin _plugin;

        public ImportManager(Plugin plugin) {
            _plugin = plugin;
        }

        internal void SetupCheckpointTotals(DutyResultsImport import) {
            var duty = _plugin.DutyManager.Duties[import.DutyId];

            //check for valid duty
            if(duty == null || duty.Checkpoints == null) {
                import.CheckpointTotals = null;
                return;
            }

            import.CheckpointTotals = new();
            foreach(var checkpoint in duty.Checkpoints!) {
                import.CheckpointTotals.Add(0);
            }
        }

        internal void SetupSummonsTotals(DutyResultsImport import) {
            import.SummonTotals = new() {
                { Summon.Lesser, 0 },
                { Summon.Greater, 0 },
                { Summon.Elder, 0 },
                { Summon.Silver, 0 },
                { Summon.Gold, 0 }
            };
        }

        internal void AddorEditImport(DutyResultsImport import, bool validate = true) {
            //validate
            if(validate && !ValidateImport(import)) {
                return;
            }

            //check to see if already exists
            if(_plugin.StorageManager.GetDutyResultsImports().Query().Where(i => i.Id == import.Id).FirstOrDefault() != null) {
                //update
                _plugin.StorageManager.UpdateDutyResultsImport(import);
            } else {
                //new
                _plugin.StorageManager.AddDutyResultsImport(import);
            }
        }

        //returns whether this is a valid import
        internal bool ValidateImport(DutyResultsImport import) {

            //check for valid duty
            if(!_plugin.DutyManager.Duties.ContainsKey(import.DutyId)) {
                return false;
            }

            //check total runs
            if(import.TotalRuns <= 0) {
                return false;
            }

            //check total clears
            if(import.TotalClears < 0 || import.TotalClears >= import.TotalRuns) {
                return false;
            }

            //check gil
            if(import.TotalGil != null && import.TotalGil < 0) {
                return false;
            }

            //check checkpoint totals
            if(import.CheckpointTotals != null) {
                for(int i = 0; i < import.CheckpointTotals.Count; i++) {
                    var checkpointCount = import.CheckpointTotals.ElementAt(i);
                    //check for negative number
                    if(checkpointCount < 0) {
                        return false;
                    }
                    //check against previous checkpoint
                    if(i == 0 && checkpointCount > import.TotalRuns) {
                        return false;
                    } else if(i == import.CheckpointTotals.Count - 1 && checkpointCount != import.TotalClears) {
                        return false;
                    }
                    if(i != 0 && checkpointCount > import.CheckpointTotals.ElementAt(i - 1)) {
                        return false;
                    }
                }
            }

            //check clear sequence
            if(import.ClearSequence != null) {

                //check against total clears
                if(import.ClearSequence.Count != import.TotalClears) {
                    return false;
                }

                uint clearSum = 0;
                foreach(var clear in import.ClearSequence) {
                    //check for non-positive number
                    if(clear <= 0) {
                        return false;
                    }
                    clearSum += clear;
                }
                //check runs since last clear
                if(import.RunsSinceLastClear < 0) {
                    return false;
                }
                clearSum += (uint)import.RunsSinceLastClear!;

                //check total
                if(clearSum != import.TotalRuns) {
                    return false;
                }
            }

            //check summon count against checkpoint count
            if(import.SummonTotals != null) {
                uint summonSum = 0;
                foreach(var summon in import.SummonTotals) {
                    //check for non-negative number
                    if(summon.Value < 0) {
                        return false;
                    }
                    summonSum += summon.Value;
                }
                //check against checkpoint totals
                //if(import.CheckpointTotals != null) {

                //}
            }

            return true;
        }
    }
}
