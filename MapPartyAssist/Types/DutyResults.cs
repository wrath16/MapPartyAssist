using Dalamud.Utility;
using LiteDB;
using MapPartyAssist.Types.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Types {

    [ValidatedDataType]
    public class DutyResults {
        [BsonId]
        [JsonIgnore]
        public ObjectId Id { get; set; }
        //Version 2: loot results
        public int Version { get; set; } = 1;
        public int DutyId { get; init; }
        public string DutyName { get; set; }
        public DateTime Time { get; init; }
        public DateTime CompletionTime { get; set; }
        public string[] Players { get; set; }
        public string Owner { get; set; }
        [BsonRef("map")]
        [JsonIgnore]
        public MPAMap? Map { get; set; }
        public int TotalGil { get; set; } = 0;
        public bool IsComplete { get; set; }
        public bool IsPickup { get; set; }
        public bool IsEdited { get; set; }

        //this sux
        public List<LootResult> FirstLootResults { get; set; } = new();

        public List<CheckpointResults> CheckpointResults { get; set; } = new();

        [BsonIgnore]
        internal CheckpointResults? LastCheckpoint => CheckpointResults.LastOrDefault();

        [BsonCtor]
        public DutyResults() {
            Id = ObjectId.NewObjectId();
            Time = DateTime.Now;
            DutyName = "";
            Owner = "";
            Players = new string[0];
        }

        public DutyResults(int dutyId, string dutyName, Dictionary<string, MPAMember> players, string owner) {
            Players = players.Keys.ToArray();
            Owner = owner;
            Time = DateTime.Now;
            DutyId = dutyId;
            DutyName = dutyName.ToLower();
            Id = ObjectId.NewObjectId();
        }

        //finds the first loot result with no recipient yet for the given itemId
        public LootResult? GetMatchingLootResult(uint itemId, bool isHQ, int quantity) {
            //should all be chronologically 
            foreach(var checkpointResult in CheckpointResults) {
                if(checkpointResult.LootResults is not null) {
                    foreach(var lootResult in checkpointResult.LootResults) {
                        if(!lootResult.Recipient.IsNullOrEmpty()) {
                            continue;
                        }
                        if(lootResult.ItemId == itemId && lootResult.IsHQ == isHQ && lootResult.Quantity == quantity) {
                            return lootResult;
                        }
                    }
                }
            }
            return null;
        }

        public bool HasLootResults() {
            foreach(var cpr in CheckpointResults) {
                if(cpr.LootResults is not null) {
                    return true;
                }
            }
            return false;
        }

        public List<LootResult> GetSummarizeLootResults(bool separateHQ = false) {
            if(separateHQ) {
                return GetSummarizeLootResultsWithQuality().OrderByDescending(l => l.Quantity).ToList();
            }

            Dictionary<uint, LootResult> consolidatedResults = new();
            foreach(var checkpointResult in CheckpointResults) {
                if(checkpointResult.LootResults is not null) {
                    foreach(var lr in checkpointResult.LootResults) {
                        if(consolidatedResults.ContainsKey(lr.ItemId)) {
                            consolidatedResults[lr.ItemId].Quantity += lr.Quantity;
                        } else {
                            consolidatedResults.Add(lr.ItemId, lr);
                        }
                    }
                }
            }
            return consolidatedResults.Values.OrderByDescending(l => l.Quantity).ToList();
        }

        public List<LootResult> GetSummarizeLootResultsWithQuality() {
            List<LootResult> summarizedResults = new();
            foreach(var checkpointResult in CheckpointResults) {
                if(checkpointResult.LootResults is not null) {
                    foreach(var lootResult in checkpointResult.LootResults) {
                        //find item with quality
                        bool isFound = false;
                        foreach(var summarizedResult in summarizedResults) {
                            if(summarizedResult.ItemId == lootResult.ItemId && summarizedResult.IsHQ == lootResult.IsHQ) {
                                summarizedResult.Quantity += lootResult.Quantity;
                                isFound = true;
                                break;
                            }
                        }
                        if(!isFound) {
                            summarizedResults.Add(lootResult);
                        }
                    }
                }
            }
            return summarizedResults;
        }
    }
}
