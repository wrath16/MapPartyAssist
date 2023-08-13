using Dalamud.Logging;
using Dalamud.Utility;
using LiteDB;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class StorageManager : IDisposable {

        public const string MapTable = "map";
        public const string DutyResultsTable = "dutyresults";
        public const string PlayerTable = "player";

        private Plugin Plugin;
        private SemaphoreSlim _dbLock;
        private LiteDatabase Database { get; init; }

        public ILiteCollection<MPAMap> Maps {
            get {
                return Database.GetCollection<MPAMap>(MapTable);
            }
        }

        public StorageManager(Plugin plugin, string path) {
            Plugin = plugin;
            Database = new LiteDatabase(path);

            //create indices
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            mapCollection.EnsureIndex(m => m.Time);
            mapCollection.EnsureIndex(m => m.Owner);
            mapCollection.EnsureIndex(m => m.IsArchived);
            mapCollection.EnsureIndex(m => m.IsDeleted);
            mapCollection.EnsureIndex(m => m.IsManual);

            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            drCollection.EnsureIndex(dr => dr.Time);
            drCollection.EnsureIndex(dr => dr.DutyName);
            drCollection.EnsureIndex(dr => dr.DutyId);

            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            playerCollection.EnsureIndex(p => p.Name);
            playerCollection.EnsureIndex(p => p.HomeWorld);
            playerCollection.EnsureIndex(p => p.Key);

            _dbLock = new SemaphoreSlim(1, 1);
        }

        public void Dispose() {
            Database.Dispose();
        }


        //wip
        public void Import() {
            PluginLog.Information("Importing data from config file into database...");

            List<MPAMap> maps = new();

            foreach(var player in Plugin.Configuration.RecentPartyList) {
                foreach(var map in player.Value.Maps) {
                    if(map.Owner.IsNullOrEmpty()) {
                        map.Owner = player.Key;
                    }
                    maps.Add(map);
                }
                player.Value.Maps = null;
                AddPlayer(player.Value);
            }
            AddMaps(maps);

            foreach(var dutyResults in Plugin.Configuration.DutyResults) {
                //find map...
                var map = Plugin.MapManager.FindMapForDutyResults(dutyResults);
                dutyResults.Map = map;
                //if(map != null) {
                //    map.DutyResults = dutyResults;
                //}
            }
            AddDutyResults(Plugin.Configuration.DutyResults);

            Plugin.Configuration.DutyResults = new();
            Plugin.Configuration.RecentPartyList = new();

            Plugin.Configuration.Version = 2;
            Plugin.Save();
        }

        private void ImportMaps() {

        }

        private void ImportDutyResults() {

        }

        public Task AddMap(MPAMap map) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Insert(map));
        }

        public Task AddMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Insert(maps));
        }

        public Task UpdateMap(MPAMap map) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Update(map));
        }

        public Task UpdateMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Update(maps.Where(m => m.Id != null)));
        }

        public ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(MapTable);
        }


        public Task AddPlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            return AsyncWriteToDatabase(() => playerCollection.Insert(player), false);
        }

        public Task UpdatePlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            return AsyncWriteToDatabase(() => playerCollection.Update(player), false);
        }

        public ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(PlayerTable);
        }

        public Task AddDutyResults(DutyResults results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Insert(results));
        }

        public Task AddDutyResults(IEnumerable<DutyResults> results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Insert(results));
        }

        public Task UpdateDutyResults(DutyResults results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Update(results));
        }

        public Task UpdateDutyResults(IEnumerable<DutyResults> results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Update(results));
        }

        public ILiteCollection<DutyResults> GetDutyResults() {
            return Database.GetCollection<DutyResults>(DutyResultsTable);
        }

        private void HandleTaskExceptions(Task task) {
            var aggException = task.Exception.Flatten();
            foreach(var exception in aggException.InnerExceptions) {
                PluginLog.Error($"{exception.Message}");
            }
        }

        private Task AsyncWriteToDatabase(Func<object> action, bool toSave = true) {
            Task task = new Task(() => {
                try {
                    _dbLock.Wait();
                    action.Invoke();
                    if(toSave) {
                        Plugin.Save();
                    }
                    _dbLock.Release();
                } catch(Exception e) {
                    _dbLock.Release();
                    PluginLog.Error($"Task Error: {e.Message}");
                    PluginLog.Error(e.StackTrace);
                }
            });
            task.Start();
            //task.ContinueWith(HandleTaskExceptions, TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }
    }
}
