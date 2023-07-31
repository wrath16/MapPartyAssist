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

        private const string _mapTable = "map";
        private const string _dutyResultsTable = "dutyresults";
        private const string _playerTable = "player";

        private Plugin Plugin;

        private SemaphoreSlim _dbLock;

        private LiteDatabase Database { get; init; }

        public ILiteCollection<MPAMap> Maps {
            get {
                return Database.GetCollection<MPAMap>(_mapTable);
            }
        }

        public StorageManager(Plugin plugin, string path) {
            Plugin = plugin;
            Database = new LiteDatabase(path);

            //create indices
            var mapCollection = Database.GetCollection<MPAMap>(_mapTable);
            mapCollection.EnsureIndex(m => m.Time);
            mapCollection.EnsureIndex(m => m.Owner);
            mapCollection.EnsureIndex(m => m.IsArchived);
            mapCollection.EnsureIndex(m => m.IsDeleted);
            mapCollection.EnsureIndex(m => m.IsManual);

            var drCollection = Database.GetCollection<DutyResults>(_dutyResultsTable);
            drCollection.EnsureIndex(dr => dr.Time);
            drCollection.EnsureIndex(dr => dr.DutyName);
            drCollection.EnsureIndex(dr => dr.DutyId);

            var playerCollection = Database.GetCollection<MPAMember>(_playerTable);
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

            Plugin.Configuration.Version = 2;
            Plugin.Save();
        }

        private void ImportMaps() {

        }

        private void ImportDutyResults() {

        }


        public void AddMap(MPAMap map) {
            //Maps.Insert(map);
            var mapCollection = Database.GetCollection<MPAMap>(_mapTable);
            //run in new thread for performance reasons.
            //Thread t = new Thread(() => {
            //    mapCollection.Insert(map);
            //    Plugin.Save();
            //});
            //t.Start();

            Task task = new Task(() => {
                _dbLock.Wait();
                mapCollection.Insert(map);
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        public void AddMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(_mapTable);
            Task task = new Task(() => {
                _dbLock.Wait();
                mapCollection.Insert(maps);
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        //returns false if not found
        public void UpdateMap(MPAMap map) {
            var mapCollection = Database.GetCollection<MPAMap>(_mapTable);
            //run in new thread for performance reasons.
            //Thread t = new Thread(() => {
            //    mapCollection.Update(map);
            //    Plugin.Save();
            //});
            //t.Start();


            Task task = new Task(() => {
                _dbLock.Wait();
                mapCollection.Update(map);
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        public void UpdateMaps(IEnumerable<MPAMap> maps) {
            var mapCollection = Database.GetCollection<MPAMap>(_mapTable);

            //Thread t = new Thread(() => {
            //    mapCollection.Update(maps.Where(m => m.Id != null));
            //    Plugin.Save();
            //});
            //t.Start();

            Task task = new Task(() => {
                _dbLock.Wait();
                mapCollection.Update(maps.Where(m => m.Id != null));
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        public ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(_mapTable);
        }


        public void AddPlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(_playerTable);
            Task task = new Task(() => {
                _dbLock.Wait();
                playerCollection.Insert(player);
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        public void UpdatePlayer(MPAMember player) {
            var playerCollection = Database.GetCollection<MPAMember>(_playerTable);
            Task task = new Task(() => {
                _dbLock.Wait();
                playerCollection.Update(player);
                Plugin.Save();
                _dbLock.Release();
            });
            task.Start();
        }

        public ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(_playerTable);
        }
    }
}
