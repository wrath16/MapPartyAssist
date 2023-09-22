using Dalamud.Logging;
using Dalamud.Utility;
using LiteDB;
using MapPartyAssist.Types;
using MapPartyAssist.Types.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    //internal service for managing connections to LiteDB database
    internal class StorageManager : IDisposable {

        internal const string MapTable = "map";
        internal const string DutyResultsTable = "dutyresults";
        internal const string StatsImportTable = "dutyresultsimport";
        internal const string PlayerTable = "player";

        private Plugin _plugin;
        private SemaphoreSlim _dbLock = new SemaphoreSlim(1, 1);
        private LiteDatabase Database { get; init; }

        internal ILiteCollection<MPAMap> Maps {
            get {
                return Database.GetCollection<MPAMap>(MapTable);
            }
        }

        internal StorageManager(Plugin plugin, string path) {
            _plugin = plugin;
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
        }

        public void Dispose() {
#if DEBUG
            PluginLog.Debug("disposing storage manager");
#endif
            Database.Dispose();
        }


#pragma warning disable 612, 618
        internal void Import() {
            PluginLog.Information("Importing data from config file into database...");

            List<MPAMap> maps = new();

            foreach(var player in _plugin.Configuration.RecentPartyList.Where(p => p.Value.Maps != null)) {
                foreach(var map in player.Value.Maps!) {
                    if(map.Owner.IsNullOrEmpty()) {
                        map.Owner = player.Key;
                    }
                    maps.Add(map);
                }
                player.Value.Maps = null;
                AddPlayer(player.Value);
            }
            AddMaps(maps);

            foreach(var dutyResults in _plugin.Configuration.DutyResults) {
                //find map...
                var map = _plugin.MapManager.FindMapForDutyResults(dutyResults);
                dutyResults.Map = map;
                //if(map != null) {
                //    map.DutyResults = dutyResults;
                //}
            }
            AddDutyResults(_plugin.Configuration.DutyResults);

            _plugin.Configuration.DutyResults = new();
            _plugin.Configuration.RecentPartyList = new();

            _plugin.Configuration.Version = 2;
            _plugin.Save();
        }
#pragma warning restore 612, 618

        internal Task AddMap(MPAMap map) {
            return AsyncWriteToDatabase(() => GetMaps().Insert(map));
        }

        internal Task AddMaps(IEnumerable<MPAMap> maps) {
            return AsyncWriteToDatabase(() => GetMaps().Insert(maps));
        }

        internal Task UpdateMap(MPAMap map) {
            return AsyncWriteToDatabase(() => GetMaps().Update(map));
        }

        internal Task UpdateMaps(IEnumerable<MPAMap> maps) {
            return AsyncWriteToDatabase(() => GetMaps().Update(maps.Where(m => m.Id != null)));
        }

        internal ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(MapTable);
        }

        internal Task AddPlayer(MPAMember player) {
            return AsyncWriteToDatabase(() => GetPlayers().Insert(player), false);
        }

        internal Task UpdatePlayer(MPAMember player) {
            return AsyncWriteToDatabase(() => GetPlayers().Update(player), false);
        }

        internal ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(PlayerTable);
        }

        internal Task AddDutyResults(DutyResults results) {
            return AsyncWriteToDatabase(() => GetDutyResults().Insert(results));
        }

        internal Task AddDutyResults(IEnumerable<DutyResults> results) {
            return AsyncWriteToDatabase(() => GetDutyResults().Insert(results));
        }

        internal Task UpdateDutyResults(DutyResults results) {
            return AsyncWriteToDatabase(() => GetDutyResults().Update(results));
        }

        internal Task UpdateDutyResults(IEnumerable<DutyResults> results) {
            return AsyncWriteToDatabase(() => GetDutyResults().Update(results));
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

        //private void HandleTaskExceptions(Task task) {
        //    var aggException = task.Exception.Flatten();
        //    foreach(var exception in aggException.InnerExceptions) {
        //        PluginLog.Error($"{exception.Message}");
        //    }
        //}

        //all writes are asynchronous for performance reasons
        private Task AsyncWriteToDatabase(Func<object> action, bool toSave = true) {
            Task task = new Task(() => {
                try {
                    _dbLock.Wait();
                    action.Invoke();
                    if(toSave) {
                        _plugin.Save();
                    }
                } finally {
                    _dbLock.Release();
                }
            });
            task.Start();
            //task.ContinueWith(HandleTaskExceptions, TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }

        //checks a data type for null values on non-nullable properties
        public bool ValidateDataType(object toValidate, bool correctErrors = false) {
            //if(toValidate.GetType().GetCustomAttribute(typeof(ValidatedDataTypeAttribute), true) == null) {
            //    throw new ArgumentException("Attempting to validate a non-data type");
            //}

            bool isValid = true;
            NullabilityInfoContext nullabilityContext = new();
            //PluginLog.Debug($"Type: {toValidate.GetType().Name}");
            foreach(var prop in toValidate.GetType().GetProperties()) {
                var nullabilityInfo = nullabilityContext.Create(prop);
                bool nullable = nullabilityInfo.WriteState is NullabilityState.Nullable;
                var curValue = prop.GetValue(toValidate);
                bool isNull = curValue is null;
                //bool isEnumerable = typeof(IEnumerable<MPADataType>).IsAssignableFrom(prop.PropertyType);
                bool isEnumerable = typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType);
                bool hasEnumerableData = false;
                if(isEnumerable) {
                    foreach(var type in prop.PropertyType.GetGenericArguments()) {
                        if(type.GetCustomAttribute(typeof(ValidatedDataTypeAttribute), true) != null) {
                            hasEnumerableData = true;
                        }
                    }
                }
                bool isReference = prop.GetCustomAttribute(typeof(BsonRefAttribute), true) != null;
                //bool isDataType = typeof(MPADataType).IsAssignableFrom(prop.PropertyType);
                bool isDataType = prop.PropertyType.GetCustomAttribute(typeof(ValidatedDataTypeAttribute), true) != null;
                //PluginLog.Debug(string.Format("Name:  {0, -20} Type: {1, -15} IsEnumerable: {7,-6} HasEnumerableData: {4, -6} IsDataType: {5, -6} IsReference: {6, -6} Nullable: {2, -6} IsNull: {3,-6}", prop.Name, prop.PropertyType.Name, nullable, isNull, hasEnumerableData, isDataType, isReference, isEnumerable));

                //check recursive data type
                if(isDataType && !isReference && !isNull && !ValidateDataType(curValue!, correctErrors)) {
                    isValid = false;
                }

                //check enumerable
                if(hasEnumerableData && !isNull && prop.PropertyType != typeof(string)) {
                    var enumerable = (System.Collections.IEnumerable)curValue!;

                    foreach(var element in (System.Collections.IEnumerable)curValue!) {
                        isValid = ValidateDataType(element, correctErrors) && isValid;
                    }
                }

                if(!nullable && isNull) {
                    isValid = false;
                    if(correctErrors) {
                        //invoke default constructor if it has fixes
                        var defaultCtor = prop.PropertyType.GetConstructor(Type.EmptyTypes);
                        if(defaultCtor != null) {
                            prop.SetValue(toValidate, defaultCtor.Invoke(null));
                        } else {
                            //PluginLog.Warning($"No default constructor for type: {prop.PropertyType.Name}");
                            //initialize empty strings
                            if(prop.PropertyType == typeof(string)) {
                                prop.SetValue(toValidate, "");
                            }
                        }
                    }
                }
            }
            //PluginLog.Debug($"");
            return isValid;
        }
    }
}
