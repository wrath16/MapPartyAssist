using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using MapPartyAssist.Types.REST.Universalis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Services {
    internal class PriceHistoryService : IDisposable {

        private const int _queryThresholdMinutes = 2;
        private const int _updateCheckSeconds = 15;
        private const int _staleDataHours = 24;
        private const int _maxSalesInAverage = 25;
        private const int _entriesToQuery = 100;
        private const int _maxSaleWindowDays = 90;
        private const int _consecutiveFailCount = 5;

        private Plugin _plugin;
        private Dictionary<LootResultKey, int> _priceCache = new();
        private Dictionary<LootResultKey, DateTime> _priceCacheUpdateTime = new();
        private List<LootResultKey> _invalidData = new();
        private List<uint> _toCheck = new();
        private DateTime _lastQuery;
        private CancellationTokenSource? _cancelUpdate;
        private SemaphoreSlim _updateLock = new(1, 1);
        private int _failCount = 0;
        private float _failMultiplier = 1;

        public bool IsEnabled => _cancelUpdate != null && !_cancelUpdate.IsCancellationRequested;

        internal PriceHistoryService(Plugin plugin) {
            _plugin = plugin;
            if(_plugin.Configuration.EnablePriceCheck) {
                Enable();
            }
        }

        public void Dispose() {
            if(IsEnabled) {
                Disable();
            }
        }

        internal int? CheckPrice(uint itemId, bool isHQ) {
            LootResultKey itemKey = new() {
                ItemId = itemId,
                IsHQ = isHQ,
            };
            return CheckPrice(itemKey);
        }

        internal int? CheckPrice(LootResultKey itemKey) {
            //gil
            if(itemKey.ItemId == 1) {
                return 1;
            }

            //tokens
            if(itemKey.ItemId >= 20 && itemKey.ItemId < 100) {
                return null;
            }

            var row = _plugin.DataManager.GetExcelSheet<Item>()?.GetRow(itemKey.ItemId);
            if(row is null || !row.CanBeHq && itemKey.IsHQ || row.IsUntradable) {
                return null;
            }

            if(_priceCache.ContainsKey(itemKey)) {
                if((DateTime.Now - _priceCacheUpdateTime[itemKey]).TotalHours > _staleDataHours && !_toCheck.Contains(itemKey.ItemId)) {
                    _plugin.Log.Verbose($"Stale data! Adding {itemKey.ItemId} to price check queue.");
                    _toCheck.Add(itemKey.ItemId);
                } else if(_priceCache[itemKey] == 0) {
                    _plugin.Log.Verbose($"Stale data! Adding {itemKey.ItemId} to price check queue.");
                    _toCheck.Add(itemKey.ItemId);
                }
                return _priceCache[itemKey];
            } else if(!_toCheck.Contains(itemKey.ItemId)) {
                _plugin.Log.Verbose($"Adding {itemKey.ItemId} to price check queue.");
                _toCheck.Add(itemKey.ItemId);
            }
            return null;
        }

        internal void Disable() {
            _plugin.Log.Information("Disabling price updates.");
            _cancelUpdate!.Cancel();
            _cancelUpdate.Dispose();
        }

        internal void Enable() {
            if(!IsEnabled) {
                _plugin.Log.Information("Enabling price updates.");
                CheckAndUpdate();
                _ = StartUpdateCheck();
            }
        }

        private async Task StartUpdateCheck() {
            _cancelUpdate = new();
            PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(_updateCheckSeconds));
            while(true) {
                CheckAndUpdate();
                await periodicTimer.WaitForNextTickAsync(_cancelUpdate.Token);
            }
        }

        private async void CheckAndUpdate() {
            _plugin.Log.Verbose("checking price validity...");
            if(_toCheck.Count > 0 && (DateTime.Now - _lastQuery).TotalMinutes > _queryThresholdMinutes * _failMultiplier && _plugin.ClientState.IsLoggedIn && _updateLock.Wait(0)) {
                try {
                    _plugin.Log.Debug("Updating item prices from Universalis API.");
                    while(_toCheck.Count > 0) {
                        try {
                            //can only query 100 at a time
                            var toCheckPage = _toCheck.Take(100).ToArray();
                            await UpdatePrices(toCheckPage);
                            if(_failCount > _consecutiveFailCount) {
                                if(_failMultiplier < 100) {
                                    _failMultiplier += 10f;
                                }
                                _plugin.Log.Error("Unable to reach Universalis API...increasing polling period.");
                                //Disable();
                                return;
                            } else {
                                _failMultiplier = 1;
                            }
                            //todo check for invalid items
                            _toCheck = _toCheck.Skip(100).ToList();
                        } catch(ArgumentException e) {
                            //invalid region or not logged in...
                            //_plugin.Log.Warning("argument exception on update prices");
                            _plugin.Log.Error(e.Message);
                            _plugin.Log.Error(e.StackTrace ?? "");
                            return;
                        }
                    }
                    _ = _plugin.DataQueue.QueueDataOperation(() => {
                        _plugin.Save();
                    });
                } finally {
                    _updateLock.Release();
                }
            }
        }

        private async Task UpdatePrices(uint[] itemIds) {
            HistoryResponse? results = await QueryUniversalisHistory(itemIds, _plugin.GameStateManager.GetCurrentRegion());
            if(results is not null) {
                foreach(var item in results.Value.Items) {
                    string itemName = _plugin.DataManager.GetExcelSheet<Item>().GetRow((uint)item.Key).Name;
                    //int normalTotal = 0;
                    int normalCount = 0;
                    //int hqTotal = 0;
                    int hqCount = 0;
                    //int averagePrice;
                    List<int> normalSales = new();
                    List<int> hqSales = new();
                    foreach(var sale in item.Value.Entries) {
                        if(sale.HQ && hqCount < _maxSalesInAverage) {
                            //hqTotal += sale.PricePerUnit;
                            hqCount++;
                            hqSales.Add(sale.PricePerUnit);
                        } else if(normalCount < _maxSalesInAverage) {
                            //normalTotal += sale.PricePerUnit;
                            normalCount++;
                            normalSales.Add(sale.PricePerUnit);
                        }
                    }

                    if(normalCount > 0) {
                        LootResultKey itemKey = new() {
                            ItemId = (uint)item.Key,
                            IsHQ = false,
                        };
                        int normalMedian = normalSales.Order().ElementAt(normalSales.Count / 2);
                        //averagePrice = normalTotal / normalCount;
                        if(_priceCache.ContainsKey(itemKey)) {
                            _priceCache[itemKey] = normalMedian;
                            _priceCacheUpdateTime[itemKey] = DateTime.Now;
                        } else {
                            _priceCache.Add(itemKey, normalMedian);
                            _priceCacheUpdateTime.Add(itemKey, DateTime.Now);
                        }
                        _plugin.Log.Verbose(string.Format("ID: {0,-8} HQ:{1,-5} Name: {2,-50} Median Price: {3,-9}", item.Key, itemKey.IsHQ, itemName, normalMedian));
                    }
                    if(hqCount > 0) {
                        LootResultKey itemKey = new() {
                            ItemId = (uint)item.Key,
                            IsHQ = true,
                        };
                        int hqMedian = hqSales.Order().ElementAt(hqSales.Count / 2);
                        //averagePrice = hqTotal / hqCount;
                        if(_priceCache.ContainsKey(itemKey)) {
                            _priceCache[itemKey] = hqMedian;
                            _priceCacheUpdateTime[itemKey] = DateTime.Now;
                        } else {
                            _priceCache.Add(itemKey, hqMedian);
                            _priceCacheUpdateTime.Add(itemKey, DateTime.Now);
                        }
                        _plugin.Log.Verbose(string.Format("ID: {0,-8} HQ:{1,-5} Name: {2,-50} Median Price: {3,-9}", item.Key, itemKey.IsHQ, itemName, hqMedian));
                    }
                }

            }
        }

        private async Task<HistoryResponse?> QueryUniversalisHistory(uint[] itemIds, Region region) {
            if(itemIds.Length <= 0) {
                throw new ArgumentException("No items specified for price lookup.");
            }

            HttpClient client = new HttpClient();
            string endpoint = "https://universalis.app/api/v2/history/";
            string searchParams = "";

            try {
                switch(region) {
                    case Region.Japan:
                        endpoint += "Japan/";
                        break;
                    case Region.NorthAmerica:
                        endpoint += "North-America/";
                        break;
                    case Region.Europe:
                        endpoint += "Europe/";
                        break;
                    case Region.Oceania:
                        endpoint += "Oceania/";
                        break;
                    default:
                        throw new ArgumentException("Invalid region.");
                }

                foreach(var id in itemIds) {
                    searchParams += id + ",";
                }
                searchParams += $"?entriesToReturn={_entriesToQuery}&entriesWithin={_maxSaleWindowDays * 24 * 60 * 60}";

                client.BaseAddress = new Uri(endpoint);
                _plugin.Log.Debug($"Query: {endpoint}{searchParams}");
                HttpResponseMessage response = await client.GetAsync(searchParams);

                if(!response.IsSuccessStatusCode) {
                    _plugin.Log.Error($"Failed to query Universalis API. {(int)response.StatusCode} {response.StatusCode}\n{response.ReasonPhrase}");
                    _failCount++;
                    return null;
                } else {
                    _failCount = 0;
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var stringReader = new StringReader(jsonResponse);
                    var stringWriter = new StringWriter();
                    var jsonReader = new JsonTextReader(stringReader);
                    var jsonWriter = new JsonTextWriter(stringWriter) {
                        Formatting = Formatting.Indented
                    };
                    jsonWriter.WriteToken(jsonReader);
                    _plugin.Log.Verbose(stringWriter.ToString());

                    HistoryResponse? result = null;
                    try {
                        result = JsonConvert.DeserializeObject<HistoryResponse>(jsonResponse, new HistoryResponseConverter(itemIds.Length == 1));
                    } catch(Exception e) {
                        _plugin.Log.Error("Deserialization failed!");
                        _plugin.Log.Error($"{e.Message} {e.Source}");
                    }
                    _lastQuery = DateTime.Now;
                    return result;
                }
            } finally {
                client.Dispose();
            }
        }
    }

    internal class HistoryResponseConverter : JsonConverter<HistoryResponse> {

        public bool SingleExpected { get; init; }

        public HistoryResponseConverter(bool singleExpected) {
            SingleExpected = singleExpected;
        }

        public override HistoryResponse ReadJson(JsonReader reader, Type objectType, HistoryResponse existingValue, bool hasExistingValue, JsonSerializer serializer) {
            HistoryResponse historyResponse = new();
            historyResponse.Items = new();
            if(SingleExpected) {
                //reader.Read();
                var itemHistory = serializer.Deserialize<ItemHistory>(reader);
                historyResponse.Items.Add(itemHistory.ItemID, itemHistory);
            }

            while(reader.Read()) {
                //if(reader.TokenType != JsonToken.PropertyName)
                //    break;

                //if(!reader.Read())
                //    continue;

                switch(reader.TokenType) {
                    case JsonToken.StartObject:
                        continue;
                    case JsonToken.PropertyName:
                        var propertyName1 = (string?)reader.Value;
                        if(int.TryParse(propertyName1, out var value)) {
                            reader.Read();
                            var x = serializer.Deserialize<ItemHistory>(reader);
                            //PluginLog.Debug($"found item: {x.ItemID}");
                            historyResponse.Items.Add(value, x);
                        } else {
                            if(propertyName1 == "unresolvedItems") {
                                reader.Read();
                                int[]? unresolvedItems = serializer.Deserialize<int[]>(reader);
                                historyResponse.UnresolvedItems = new(unresolvedItems ?? []);
                            }
                        }
                        break;
                    default:
                        continue;
                }
            }
            return historyResponse;
        }

        public override void WriteJson(JsonWriter writer, HistoryResponse value, JsonSerializer serializer) {
            throw new NotImplementedException();
        }
    }
}
