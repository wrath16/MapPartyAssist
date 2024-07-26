using LiteDB;
using MapPartyAssist.Types;
using MapPartyAssist.Types.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

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
        internal void AddMap(MPAMap map, bool toSave = true) {
            LogUpdate(map.Id.ToString());
            WriteToDatabase(() => GetMaps().Insert(map), toSave);
        }

        internal void AddMaps(IEnumerable<MPAMap> maps, bool toSave = true) {
            LogUpdate(null, maps.Count());
            WriteToDatabase(() => GetMaps().Insert(maps), toSave);
        }

        internal void UpdateMap(MPAMap map, bool toSave = true) {
            LogUpdate(map.Id.ToString());
            WriteToDatabase(() => GetMaps().Update(map), toSave);
        }

        internal void UpdateMaps(IEnumerable<MPAMap> maps, bool toSave = true) {
            LogUpdate(null, maps.Count());
            WriteToDatabase(() => GetMaps().Update(maps.Where(m => m.Id != null)), toSave);
        }

        internal ILiteCollection<MPAMap> GetMaps() {
            return Database.GetCollection<MPAMap>(MapTable);
        }

        internal void AddPlayer(MPAMember player, bool toSave = true) {
            LogUpdate(player.Key);
            WriteToDatabase(() => GetPlayers().Insert(player), toSave);
        }

        internal void UpdatePlayer(MPAMember player, bool toSave = true) {
            LogUpdate(player.Key);
            WriteToDatabase(() => GetPlayers().Update(player), toSave);
        }

        internal ILiteCollection<MPAMember> GetPlayers() {
            return Database.GetCollection<MPAMember>(PlayerTable);
        }

        internal void AddDutyResults(DutyResults results, bool toSave = true) {
            LogUpdate(results.Id.ToString());
            WriteToDatabase(() => GetDutyResults().Insert(results), toSave);
        }

        internal void AddDutyResults(IEnumerable<DutyResults> results, bool toSave = true) {
            LogUpdate(null, results.Count());
            WriteToDatabase(() => GetDutyResults().Insert(results), toSave);
        }

        internal void UpdateDutyResults(DutyResults results, bool toSave = true) {
            LogUpdate(results.Id.ToString());
            WriteToDatabase(() => GetDutyResults().Update(results), toSave);
        }

        internal void UpdateDutyResults(IEnumerable<DutyResults> results, bool toSave = true) {
            LogUpdate(null, results.Count());
            WriteToDatabase(() => GetDutyResults().Update(results), toSave);
        }

        internal ILiteCollection<DutyResults> GetDutyResults() {
            return Database.GetCollection<DutyResults>(DutyResultsTable);
        }

        internal void AddDutyResultsRaw(DutyResultsRaw results, bool toSave = true) {
            LogUpdate(results.Id.ToString());
            WriteToDatabase(() => GetDutyResultsRaw().Insert(results), toSave);
        }

        internal void UpdateDutyResultsRaw(DutyResultsRaw results, bool toSave = true) {
            LogUpdate(results.Id.ToString());
            WriteToDatabase(() => GetDutyResultsRaw().Update(results), toSave);
        }

        internal ILiteCollection<DutyResultsRaw> GetDutyResultsRaw() {
            return Database.GetCollection<DutyResultsRaw>(DutyResultsRawTable);
        }

        internal void AddDutyResultsImport(DutyResultsImport import, bool toSave = true) {
            LogUpdate(import.Id.ToString());
            WriteToDatabase(() => GetDutyResultsImports().Insert(import), toSave);
        }

        internal void UpdateDutyResultsImport(DutyResultsImport import, bool toSave = true) {
            LogUpdate(import.Id.ToString());
            WriteToDatabase(() => GetDutyResultsImports().Update(import), toSave);
        }

        internal ILiteCollection<DutyResultsImport> GetDutyResultsImports() {
            return Database.GetCollection<DutyResultsImport>(StatsImportTable);
        }

        internal void AddPrices(IEnumerable<PriceCheck> prices, bool toSave = true) {
            LogUpdate(null, prices.Count());
            WriteToDatabase(() => GetPrices().Insert(prices), toSave);
        }

        internal void UpdatePrices(IEnumerable<PriceCheck> prices, bool toSave = true) {
            LogUpdate(null, prices.Count());
            WriteToDatabase(() => GetPrices().Update(prices), toSave);
        }

        internal ILiteCollection<PriceCheck> GetPrices() {
            return Database.GetCollection<PriceCheck>(PriceTable);
        }

        private void LogUpdate(string? id = null, int count = 0) {
            var callingMethod = new StackFrame(2, true).GetMethod();
            var writeMethod = new StackFrame(1, true).GetMethod();

            _plugin.Log.Debug(string.Format("Invoking {0,-25} {2,-30}{3,-30} Caller: {1,-70}",
                writeMethod?.Name, $"{callingMethod?.DeclaringType?.ToString() ?? ""}.{callingMethod?.Name ?? ""}", id != null ? $"ID: {id}" : "", count != 0 ? $"Count: {count}" : ""));
        }

        //synchronous write
        private void WriteToDatabase(Func<object> action, bool toSave = true) {
            try {
                _dbLock.Wait();
                action.Invoke();
                if(toSave) {
                    _plugin.Refresh();
                }
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
            //_plugin.Log.Debug($"Type: {toValidate.GetType().Name}");
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
                //_plugin.Log.Debug(string.Format("Name:  {0, -20} Type: {1, -15} IsEnumerable: {7,-6} HasEnumerableData: {4, -6} IsDataType: {5, -6} IsReference: {6, -6} Nullable: {2, -6} IsNull: {3,-6}", prop.Name, prop.PropertyType.Name, nullable, isNull, hasEnumerableData, isDataType, isReference, isEnumerable));

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
                            //_plugin.Log.Warning($"No default constructor for type: {prop.PropertyType.Name}");
                            //initialize empty strings
                            if(prop.PropertyType == typeof(string)) {
                                prop.SetValue(toValidate, "");
                            }
                        }
                    }
                }
            }
            //_plugin.Log.Debug($"");
            return isValid;
        }
    }
}
