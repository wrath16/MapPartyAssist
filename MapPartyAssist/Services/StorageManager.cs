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
    //internal service for managing connections to LiteDB database
    internal class StorageManager : IDisposable {

        internal const string MapTable = "map";
        internal const string DutyResultsTable = "dutyresults";
        internal const string StatsImportTable = "dutyresultsimport";
        internal const string PlayerTable = "player";

        private Plugin Plugin;
        private SemaphoreSlim _dbLock;
        private LiteDatabase Database { get; init; }

        internal ILiteCollection<MPAMap> Maps {
            get {
                return Database.GetCollection<MPAMap>(MapTable);
            }
        }

        internal StorageManager(Plugin plugin, string path) {
            Plugin = plugin;
            Database = new LiteDatabase(path);

            //set mapper properties
            BsonMapper.Global.EmptyStringToNull = false;

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

            var importCollection = Database.GetCollection<DutyResultsImport>(StatsImportTable);
            importCollection.EnsureIndex(i => i.Time);
            importCollection.EnsureIndex(i => i.DutyId);

            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            playerCollection.EnsureIndex(p => p.Name);
            playerCollection.EnsureIndex(p => p.HomeWorld);
            playerCollection.EnsureIndex(p => p.Key);

            _dbLock = new SemaphoreSlim(1, 1);
        }

        public void Dispose() {
            Database.Dispose();
        }

        internal void Import() {
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

        internal Task AddMap(MPAMap map) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Insert(map));
        }

        internal Task AddMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Insert(maps));
        }

        internal Task UpdateMap(MPAMap map) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Update(map));
        }

        internal Task UpdateMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(MapTable);
            return AsyncWriteToDatabase(() => mapCollection.Update(maps.Where(m => m.Id != null)));
        }

        internal ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(MapTable);
        }

        internal Task AddPlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            return AsyncWriteToDatabase(() => playerCollection.Insert(player), false);
        }

        internal Task UpdatePlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            return AsyncWriteToDatabase(() => playerCollection.Update(player), false);
        }

        internal ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(PlayerTable);
        }

        internal Task AddDutyResults(DutyResults results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Insert(results));
        }

        internal Task AddDutyResults(IEnumerable<DutyResults> results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Insert(results));
        }

        internal Task UpdateDutyResults(DutyResults results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Update(results));
        }

        internal Task UpdateDutyResults(IEnumerable<DutyResults> results) {
            var drCollection = Database.GetCollection<DutyResults>(DutyResultsTable);
            return AsyncWriteToDatabase(() => drCollection.Update(results));
        }

        internal ILiteCollection<DutyResults> GetDutyResults() {
            return Database.GetCollection<DutyResults>(DutyResultsTable);
        }

        internal Task AddDutyResultsImport(DutyResultsImport import) {
            var importCollection = Database.GetCollection<DutyResultsImport>(StatsImportTable);
            return AsyncWriteToDatabase(() => importCollection.Insert(import));
        }

        internal Task UpdateDutyResultsImport(DutyResultsImport import) {
            var importCollection = Database.GetCollection<DutyResultsImport>(StatsImportTable);
            return AsyncWriteToDatabase(() => importCollection.Update(import));
        }

        internal ILiteCollection<DutyResultsImport> GetDutyResultsImports() {
            return Database.GetCollection<DutyResultsImport>(StatsImportTable);
        }

        private void HandleTaskExceptions(Task task) {
            var aggException = task.Exception.Flatten();
            foreach(var exception in aggException.InnerExceptions) {
                PluginLog.Error($"{exception.Message}");
            }
        }

        //all writes are asynchronous for performance reasons
        private Task AsyncWriteToDatabase(Func<object> action, bool toSave = true) {
            Task task = new Task(() => {
                try {
                    _dbLock.Wait();
                    action.Invoke();
                    if(toSave) {
                        Plugin.Save();
                    }
                } finally {
                    _dbLock.Release();
                }
            });
            task.Start();
            //task.ContinueWith(HandleTaskExceptions, TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }
    }
}
