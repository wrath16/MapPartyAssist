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
        internal const string DutyResultsRawTable = "dutyresults_raw";
        internal const string StatsImportTable = "dutyresultsimport";
        internal const string PlayerTable = "player";
        internal const string PriceTable = "price";

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
            BsonMapper.Global.RegisterType<DateTime>(
                serialize: dt => new BsonValue(dt.ToUniversalTime()),
                deserialize: v => v.AsDateTime.ToUniversalTime()
            );

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
            drCollection.EnsureIndex(dr => dr.Owner);

            var importCollection = Database.GetCollection<DutyResultsImport>(StatsImportTable);
            importCollection.EnsureIndex(i => i.Time);
            importCollection.EnsureIndex(i => i.DutyId);

            var playerCollection = Database.GetCollection<MPAMember>(PlayerTable);
            playerCollection.EnsureIndex(p => p.Name);
            playerCollection.EnsureIndex(p => p.HomeWorld);
            playerCollection.EnsureIndex(p => p.Key);

            GetPrices().EnsureIndex(p => p.ItemId);
            GetPrices().EnsureIndex(p => p.Region);
        }

        public void Dispose() {
            Database.Dispose();
        }
        internal async Task AddMap(MPAMap map) {
            Plugin.Log.Debug($"DB: adding map: {map.Id}");
            await WriteToDatabase(() => GetMaps().Insert(map));
        }

        internal async Task AddMaps(IEnumerable<MPAMap> maps) {
            Plugin.Log.Debug($"DB: adding maps list: {maps.Count()}");
            await WriteToDatabase(() => GetMaps().Insert(maps));
        }

        internal async Task UpdateMap(MPAMap map) {
            Plugin.Log.Debug($"DB: updating map: {map.Id}");
            await WriteToDatabase(() => GetMaps().Update(map));
        }

        internal async Task UpdateMaps(IEnumerable<MPAMap> maps) {
            Plugin.Log.Debug($"DB: updating maps list: {maps.Count()}");
            await WriteToDatabase(() => GetMaps().Update(maps.Where(m => m.Id != null)));
        }

        internal ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(MapTable);
        }

        internal async Task AddPlayer(MPAMember player) {
            Plugin.Log.Debug($"DB: adding player: {player.Key}");
            await WriteToDatabase(() => GetPlayers().Insert(player));
        }

        internal async Task UpdatePlayer(MPAMember player) {
            Plugin.Log.Debug($"DB: updating player: {player.Key}");
            await WriteToDatabase(() => GetPlayers().Update(player));
        }

        internal ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(PlayerTable);
        }

        internal async Task AddDutyResults(DutyResults results) {
            Plugin.Log.Debug($"DB: adding duty results: {results.Id}");
            await WriteToDatabase(() => GetDutyResults().Insert(results));
        }

        internal async Task AddDutyResults(IEnumerable<DutyResults> results) {
            Plugin.Log.Debug($"DB: adding duty results list: {results.Count()}");
            await WriteToDatabase(() => GetDutyResults().Insert(results));
        }

        internal async Task UpdateDutyResults(DutyResults results) {
            Plugin.Log.Debug($"DB: updating duty results: {results.Id}");
            await WriteToDatabase(() => GetDutyResults().Update(results));
        }

        internal async Task UpdateDutyResults(IEnumerable<DutyResults> results) {
            Plugin.Log.Debug($"DB: updating duty results list: {results.Count()}");
            await WriteToDatabase(() => GetDutyResults().Update(results));
        }

        internal ILiteCollection<DutyResults> GetDutyResults() {
            return Database.GetCollection<DutyResults>(DutyResultsTable);
        }

        internal async Task AddDutyResultsImport(DutyResultsImport import) {
            Plugin.Log.Debug($"DB: adding import: {import.Id}");
            await WriteToDatabase(() => GetDutyResultsImports().Insert(import));
        }

        internal async Task UpdateDutyResultsImport(DutyResultsImport import) {
            Plugin.Log.Debug($"DB: updating import: {import.Id}");
            await WriteToDatabase(() => GetDutyResultsImports().Update(import));
        }

        internal ILiteCollection<DutyResultsImport> GetDutyResultsImports() {
            return Database.GetCollection<DutyResultsImport>(StatsImportTable);
        }

        internal async Task AddPrices(IEnumerable<PriceCheck> prices) {
            Plugin.Log.Debug("DB: adding prices");
            await WriteToDatabase(() => GetPrices().Insert(prices));
        }

        internal async Task UpdatePrices(IEnumerable<PriceCheck> prices) {
            Plugin.Log.Debug("DB: updating prices");
            await WriteToDatabase(() => GetPrices().Update(prices));
        }

        internal ILiteCollection<PriceCheck> GetPrices() {
            return Database.GetCollection<PriceCheck>(PriceTable);
        }

        private async Task WriteToDatabase(Func<object> action) {
            try {
                await _dbLock.WaitAsync();
                action.Invoke();
            } finally {
                _dbLock.Release();
            }
        }

        //all writes are asynchronous for performance reasons
        //private Task AsyncWriteToDatabase(Func<object> action, bool toSave = true) {
        //    Task task = new Task(() => {
        //        try {
        //            _dbLock.Wait();
        //            action.Invoke();
        //            if(toSave) {
        //                _plugin.Save();
        //            }
        //        } finally {
        //            _dbLock.Release();
        //        }
        //    });
        //    task.Start();
        //    //task.ContinueWith(HandleTaskExceptions, TaskContinuationOptions.OnlyOnFaulted);
        //    return task;
        //}

        //checks a data type for null values on non-nullable properties
        public bool ValidateDataType(object toValidate, bool correctErrors = false) {
            //if(toValidate.GetType().GetCustomAttribute(typeof(ValidatedDataTypeAttribute), true) == null) {
            //    throw new ArgumentException("Attempting to validate a non-data type");
            //}

            bool isValid = true;
            NullabilityInfoContext nullabilityContext = new();
            //Plugin.Plugin.Log.Debug($"Type: {toValidate.GetType().Name}");
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
                //Plugin.Plugin.Log.Debug(string.Format("Name:  {0, -20} Type: {1, -15} IsEnumerable: {7,-6} HasEnumerableData: {4, -6} IsDataType: {5, -6} IsReference: {6, -6} Nullable: {2, -6} IsNull: {3,-6}", prop.Name, prop.PropertyType.Name, nullable, isNull, hasEnumerableData, isDataType, isReference, isEnumerable));

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
                            //Plugin.Plugin.Log.Warning($"No default constructor for type: {prop.PropertyType.Name}");
                            //initialize empty strings
                            if(prop.PropertyType == typeof(string)) {
                                prop.SetValue(toValidate, "");
                            }
                        }
                    }
                }
            }
            //Plugin.Plugin.Log.Debug($"");
            return isValid;
        }
    }
}
