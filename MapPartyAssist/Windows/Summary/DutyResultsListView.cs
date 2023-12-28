using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;
using LiteDB;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static MapPartyAssist.Windows.Summary.LootSummary;

namespace MapPartyAssist.Windows.Summary {
    internal class DutyResultsListView {

        private const int _maxPageSize = 100;

        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private List<DutyResults> _dutyResults = new();
        private List<DutyResults> _dutyResultsPage = new();
        private Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> _lootResults = new();

        private int _currentPage = 0;
        private bool _collapseAll = false;
        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public string CSV { get; private set; } = "";

        internal DutyResultsListView(Plugin plugin, StatsWindow statsWindow) {
            _plugin = plugin;
            _statsWindow = statsWindow;
        }

        public void Refresh(List<DutyResults> dutyResults) {
            Dictionary<ObjectId, Dictionary<LootResultKey, LootResultValue>> lootResults = new();
            //calculate loot results (this is largely duplicated from lootsummary)
            List<string> selfPlayers = new();
            _plugin.StorageManager.GetPlayers().Query().Where(p => p.IsSelf).ToList().ForEach(p => {
                selfPlayers.Add(p.Key);
            });

            foreach(var dr in dutyResults) {
                if(!dr.HasLootResults()) {
                    continue;
                }
                Dictionary<LootResultKey, LootResultValue> newLootResults = new();
                foreach(var checkpointResult in dr.CheckpointResults) {
                    foreach(var lootResult in checkpointResult.LootResults!) {
                        var key = new LootResultKey { ItemId = lootResult.ItemId, IsHQ = lootResult.IsHQ };
                        bool selfObtained = lootResult.Recipient is not null && selfPlayers.Contains(lootResult.Recipient);
                        int obtainedQuantity = selfObtained ? lootResult.Quantity : 0;
                        if(newLootResults.ContainsKey(key)) {
                            newLootResults[key].ObtainedQuantity += obtainedQuantity;
                            newLootResults[key].DroppedQuantity += lootResult.Quantity;
                        } else {
                            var row = _plugin.DataManager.GetExcelSheet<Item>().GetRow(lootResult.ItemId);
                            newLootResults.Add(key, new LootResultValue {
                                DroppedQuantity = lootResult.Quantity,
                                ObtainedQuantity = obtainedQuantity,
                                Rarity = row.Rarity,
                                ItemName = row.Name,
                                Category = row.ItemUICategory.Value.Name,
                            });
                        }
                    }
                }
                newLootResults = newLootResults.OrderByDescending(lr => lr.Value.DroppedQuantity).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                lootResults.Add(dr.Id, newLootResults);
            }
            try {
                _refreshLock.Wait();
                _dutyResults = dutyResults;
                _lootResults = lootResults;
            } finally {
                _refreshLock.Release();
            }
            GoToPage();
        }

        private void GoToPage(int? page = null) {
            //null = stay on same page
            page ??= _currentPage;
            _currentPage = (int)page;
            _dutyResultsPage = _dutyResults.OrderByDescending(dr => dr.Time).Skip(_currentPage * _maxPageSize).Take(_maxPageSize).ToList();
            CSV = "";
            foreach(var dutyResult in _dutyResultsPage.OrderBy(dr => dr.Time)) {
                //no checks
                float checkpoint = dutyResult.CheckpointResults.Count / 2f;
                if(_plugin.DutyManager.Duties[dutyResult.DutyId].Structure == DutyStructure.Doors) {
                    checkpoint += 0.5f;
                }
                CSV = CSV + checkpoint.ToString() + ",";
            }
        }

        public void Draw() {
            if(!_refreshLock.Wait(0)) {
                return;
            }
            try {
                if(_plugin.AllowEdit) {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    ImGui.PopFont();
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"EDIT MODE ENABLED");
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
                    ImGui.PopFont();
                }

                if(_plugin.AllowEdit && ImGui.Button("Save")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        _plugin.StorageManager.UpdateDutyResults(_dutyResultsPage.Where(dr => dr.IsEdited));
                        //_plugin.Save();
                    });
                }

                ImGui.SameLine();
                if(ImGui.Button("Copy CSV")) {
                    Task.Run(() => {
                        ImGui.SetClipboardText(CSV);
                    });
                }
                if(ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.Text($"Creates a sequential comma-separated list of the last checkpoint reached to the clipboard.");
                    ImGui.EndTooltip();
                }

                ImGui.SameLine();
                if(ImGui.Button("Collapse All")) {
                    _collapseAll = true;
                }

                if(_currentPage > 0) {
                    ImGui.SameLine();
                    if(ImGui.Button($"Previous {_maxPageSize}")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            GoToPage(_currentPage - 1);
                        });
                    }
                }

                if(_dutyResultsPage.Count >= _maxPageSize) {
                    ImGui.SameLine();
                    if(ImGui.Button($"Next {_maxPageSize}")) {
                        _plugin.DataQueue.QueueDataOperation(() => {
                            GoToPage(_currentPage + 1);
                        });
                    }
                }
                ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);
                foreach(var results in _dutyResultsPage) {
                    if(_collapseAll) {
                        ImGui.SetNextItemOpen(false);
                    }

                    float targetWidth1 = 150f * ImGuiHelpers.GlobalScale;
                    float targetWidth2 = 215f * ImGuiHelpers.GlobalScale;
                    var text1 = results.Time.ToString();
                    var text2 = _plugin.DutyManager.Duties[results.DutyId].GetDisplayName();
                    while(ImGui.CalcTextSize(text1).X < targetWidth1) {
                        text1 += " ";
                    }
                    while(ImGui.CalcTextSize(text2).X < targetWidth2) {
                        text2 += " ";
                    }

                    if(ImGui.CollapsingHeader(string.Format("{0:-23} {1:-40} {2:-25}", text1, text2, results.Id.ToString()))) {
                        if(_plugin.AllowEdit) {
                            DrawDutyResultsEditable(results);
                        } else {
                            DrawDutyResults(results);
                        }
                    }
                }
                ImGui.EndChild();
                _collapseAll = false;
            } finally {
                _refreshLock.Release();
            }
        }

        private void DrawDutyResults(DutyResults dutyResults) {
            if(ImGui.BeginTable($"##{dutyResults.Id}--MainTable", 2, ImGuiTableFlags.NoClip)) {
                ImGui.TableNextColumn();
                if(ImGui.BeginTable($"##{dutyResults.Id}--SubTable1", 2)) {
                    ImGui.TableSetupColumn("propName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 95f);
                    ImGui.TableSetupColumn("propVal", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Completed: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{dutyResults.IsComplete}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Duration: ");
                    ImGui.TableNextColumn();
                    string durationString = "";
                    if(dutyResults.CompletionTime > dutyResults.Time) {
                        durationString = (dutyResults.CompletionTime - dutyResults.Time).ToString(@"mm\:ss");
                    }
                    ImGui.Text($"{durationString}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Owner: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{dutyResults.Owner}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Map ID: ");
                    ImGui.TableNextColumn();
                    if(dutyResults.Map != null) {
                        ImGui.Text($"{dutyResults.Map.Id}");
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Last Checkpoint: ");
                    ImGui.TableNextColumn();
                    var currentLastCheckpointIndex = dutyResults.CheckpointResults.Count - 1;
                    ImGui.Text($"{_plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!.ElementAt(currentLastCheckpointIndex).Name}");

                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Total Gil: ");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{dutyResults.TotalGil}");

                    if(_plugin.DutyManager.Duties[dutyResults.DutyId].Structure == DutyStructure.Roulette) {
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.TextColored(ImGuiColors.DalamudGrey, "Summons: ");
                        ImGui.TableNextColumn();
                        foreach(var checkpointResult in dutyResults.CheckpointResults) {
                            if(checkpointResult.SummonType != null) {
                                ImGui.Text($"{DutyHelper.GetSummonName((Summon)checkpointResult.SummonType)}");
                            }
                        }
                    }
                    ImGui.EndTable();
                }
                ImGui.TableNextColumn();
                if(ImGui.BeginTable($"##{dutyResults.Id}--SubTable2", 2)) {
                    ImGui.TableSetupColumn("propName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 95f);
                    ImGui.TableSetupColumn("propVal", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Party Members: ");
                    ImGui.TableNextColumn();
                    foreach(var partyMember in dutyResults.Players) {
                        ImGui.Text($"{partyMember}");
                    }
                    ImGui.EndTable();
                }
                ImGui.EndTable();
            }
            if(dutyResults.HasLootResults()) {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Loot: ");
                if(ImGui.BeginTable($"loottable", 4, ImGuiTableFlags.None)) {
                    //ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f);
                    ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 55f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200f);
                    ImGui.TableSetupColumn("Dropped", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 65f);
                    ImGui.TableSetupColumn("Obtained", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 70f);
                    ImGui.TableNextColumn();
                    ImGui.Text("Quality");
                    ImGui.TableNextColumn();
                    ImGui.Text("Name");
                    ImGui.TableNextColumn();
                    ImGui.Text("Dropped");
                    ImGui.TableNextColumn();
                    ImGui.Text("Obtained");
                    foreach(var lootResult in _lootResults[dutyResults.Id]) {
                        //ImGui.TableNextColumn();
                        //ImGui.Text($"{lootResult.Value.Category}");
                        ImGui.TableNextColumn();
                        var qualityText = lootResult.Key.IsHQ ? "HQ" : "";
                        ImGuiHelper.CenterAlignCursor(qualityText);
                        ImGui.Text($"{qualityText}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{lootResult.Value.ItemName}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{lootResult.Value.DroppedQuantity}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{lootResult.Value.ObtainedQuantity}");
                    }
                    ImGui.EndTable();
                }
            }
        }

        private void DrawDutyResultsEditable(DutyResults dutyResults) {
            List<string> lastCheckpoints = new() {
                "None"
            };
            string? owner = dutyResults.Owner ?? "";
            string? gil = dutyResults.TotalGil.ToString();
            bool isCompleted = dutyResults.IsComplete;
            foreach(var checkpoint in _plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!) {
                lastCheckpoints.Add(checkpoint.Name);
            }

            if(ImGui.Checkbox($"Completed##{dutyResults.Id}--Completed", ref isCompleted)) {
                dutyResults.IsEdited = true;
                dutyResults.IsComplete = isCompleted;
            }
            if(ImGui.InputText($"Owner##{dutyResults.Id}--Owner", ref owner, 50, ImGuiInputTextFlags.AutoSelectAll)) {
                dutyResults.IsEdited = true;
                dutyResults.Owner = owner;
            }
            if(ImGui.InputText($"Total Gil##{dutyResults.Id}--TotalGil", ref gil, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                int gilInt;
                if(int.TryParse(gil, out gilInt)) {
                    dutyResults.IsEdited = true;
                    dutyResults.TotalGil = gilInt;
                }
            }
            var currentLastCheckpointIndex = dutyResults.CheckpointResults.Count;
            if(ImGui.Combo($"Last Checkpoint##{dutyResults.Id}--LastCheckpoint", ref currentLastCheckpointIndex, lastCheckpoints.ToArray(), lastCheckpoints.Count)) {
                if(currentLastCheckpointIndex > dutyResults.CheckpointResults.Count) {
                    dutyResults.IsEdited = true;
                    for(int i = dutyResults.CheckpointResults.Count; i <= currentLastCheckpointIndex - 1; i++) {
                        dutyResults.CheckpointResults.Add(new CheckpointResults(_plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints![i], true));
                    }
                } else if(currentLastCheckpointIndex < dutyResults.CheckpointResults.Count) {
                    dutyResults.IsEdited = true;
                    for(int i = dutyResults.CheckpointResults.Count - 1; i >= currentLastCheckpointIndex; i--) {
                        dutyResults.CheckpointResults.RemoveAt(i);
                    }
                }
            }
            if(_plugin.DutyManager.Duties[dutyResults.DutyId].Structure == DutyStructure.Roulette) {
                string[] summons = { "Lesser", "Greater", "Elder", "Abomination", "Circle Shift" };
                var summonCheckpoints = dutyResults.CheckpointResults.Where(cr => cr.Checkpoint.Name.StartsWith("Complete")).ToList();
                for(int i = 0; i < summonCheckpoints.Count(); i++) {
                    int summonIndex = (int?)summonCheckpoints[i].SummonType ?? 3;
                    if(ImGui.Combo($"{StringHelper.AddOrdinal(i + 1)} Summon##{summonCheckpoints[i].GetHashCode()}-Summon", ref summonIndex, summons, summons.Length)) {
                        dutyResults.IsEdited = true;
                        summonCheckpoints[i].SummonType = (Summon)summonIndex;
                    }
                }
            }
        }
    }
}
