using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using LiteDB;
using Lumina.Excel.Sheets;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace MapPartyAssist.Windows {

    public enum DutyRange {
        Current,
        PastDay,
        PastWeek,
        All
    }

    internal class DutyResultsWindow : Window {

        private Plugin _plugin;
        private List<DutyResults> _dutyResults = new();
        private Dictionary<ObjectId, List<LootResult>> _lootResults = new();
        private int _currentPage = 0;
        private bool _collapseAll = false;
        private DutyRange _dutyRange = DutyRange.All;
        private readonly string[] _rangeCombo = { "Current", "Last Day", "Last Week", "All-Time" };
        private int _dutyId = 0;
        private int _selectedDuty = 0;
        private readonly int[] _dutyIdCombo = { 0, 179, 268, 276, 586, 688, 745, 819, 909 };
        private readonly string[] _dutyNameCombo;
        private string _partyMemberFilter = "";
        private string _ownerFilter = "";

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        internal DutyResultsWindow(Plugin plugin) : base("Edit Duty Results") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(400, 50),
                MaximumSize = new Vector2(500, 800)
            };
            _plugin = plugin;

            //setup duty name combo
            _dutyNameCombo = new string[_dutyIdCombo.Length];
            _dutyNameCombo[0] = "All Duties";
            for(int i = 1; i < _dutyIdCombo.Length; i++) {
                _dutyNameCombo[i] = _plugin.DutyManager.Duties[_dutyIdCombo[i]].GetDisplayName();
            }
        }

        internal Task Refresh(int? pageIndex = null) {
            return Task.Run(async () => {
                try {
                    await _refreshLock.WaitAsync();
                    _collapseAll = true;
                    //null index = stay on same page
                    pageIndex ??= _currentPage;
                    _currentPage = (int)pageIndex;
                    _lootResults = new();

                    //better performance to filter on DB than use LINQ due to indexing
                    _dutyResults = _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map)
                        .Where(dr => (_dutyRange != DutyRange.Current || (dr.Map != null && !dr.Map.IsArchived && !dr.Map.IsDeleted))
                        //&& (_dutyRange != DutyRange.PastDay || ((DateTime.Now - dr.Time).TotalHours < 24))
                        //&& (_dutyRange != DutyRange.PastWeek || ((DateTime.Now - dr.Time).TotalDays < 7))
                        && dr.Owner.Contains(_ownerFilter, StringComparison.OrdinalIgnoreCase)
                        && (_dutyId == 0 || dr.DutyId == _dutyId)).OrderByDescending(dr => dr.Time).ToList();

                    string[] partyMemberFilters = _partyMemberFilter.Split(",");

                    //these expressions don't get converted to BSONExpressions properly so we'll use LINQ
                    _dutyResults = _dutyResults.Where(dr => (_dutyRange != DutyRange.PastDay || ((DateTime.Now - dr.Time).TotalHours < 24))
                        && (_dutyRange != DutyRange.PastWeek || ((DateTime.Now - dr.Time).TotalDays < 7))).Where(dr => {
                            bool allMatch = true;
                            foreach(string partyMemberFilter in partyMemberFilters) {
                                bool matchFound = false;
                                string partyMemberFilterTrimmed = partyMemberFilter.Trim();
                                foreach(string partyMember in dr.Players) {
                                    if(partyMember.Contains(partyMemberFilterTrimmed, StringComparison.OrdinalIgnoreCase)) {
                                        matchFound = true;
                                        break;
                                    }
                                }
                                allMatch = allMatch && matchFound;
                                if(!allMatch) {
                                    return false;
                                }
                            }
                            return allMatch;
                        }).Skip(_currentPage * 100).Take(100).ToList();

                    //calculate loot results
                    foreach(var dr in _dutyResults) {
                        _lootResults.Add(dr.Id, dr.GetSummarizeLootResults());
                    }
                    //set item names. this is an expensive operation!
                    foreach(var lootresultList in _lootResults.Values) {
                        foreach(var lr in lootresultList) {
                            var row = _plugin.DataManager.GetExcelSheet<Item>()?.First(r => r.RowId == lr.ItemId);
                            bool isPlural = lr.Quantity > 1;
                            if(row is not null) {
                                lr.ItemName = isPlural ? row.Value.Plural.ToString() : row.Value.Singular.ToString();
                            }
                        }
                    }
                } finally {
                    _refreshLock.Release();
                }
            });
        }

        public override void OnClose() {
            base.OnClose();
            //Refresh(0);
        }

        public override void OnOpen() {
            base.OnOpen();
        }

        public override void Draw() {
            if(ImGui.BeginTable($"##filterTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX)) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                //ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                if(ImGui.Combo($"Duty##DutyCombo", ref _selectedDuty, _dutyNameCombo, _dutyNameCombo.Length)) {
                    _dutyId = _dutyIdCombo[_selectedDuty];
                    Refresh(0);
                }
                ImGui.TableNextColumn();
                int dutyRangeToInt = (int)_dutyRange;
                //ImGui.SameLine();
                //ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                if(ImGui.Combo($"Range##includesCombo", ref dutyRangeToInt, _rangeCombo, _rangeCombo.Length)) {
                    _dutyRange = (DutyRange)dutyRangeToInt;
                    Refresh(0);
                }
                ImGui.EndTable();
            }

            if(ImGui.InputText($"Map Owner", ref _ownerFilter, 50)) {
                Refresh(0);
            }
            if(ImGui.InputText($"Party Members", ref _partyMemberFilter, 100)) {
                Refresh(0);
            }
            ImGuiComponents.HelpMarker("Party members present during run. \nSeparate party members by commas.");
            try {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
            } finally {
                ImGui.PopFont();
            }
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudRed, $"EDIT AT YOUR OWN RISK");
            ImGui.SameLine();
            try {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
            } finally {
                ImGui.PopFont();
            }
            ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);
            foreach(var results in _dutyResults) {
                if(_collapseAll) {
                    ImGui.SetNextItemOpen(false);
                }
                DrawDutyResults(results);
            }
            _collapseAll = false;

            ImGui.EndChild();

            if(ImGui.Button("Save")) {
                //should this go in manager?
                _plugin.StorageManager.UpdateDutyResults(_dutyResults.Where(dr => dr.IsEdited));
            }

            ImGui.SameLine();
            if(ImGui.Button("Copy CSV")) {
                string csv = "";
                foreach(var dutyResult in _dutyResults.OrderBy(dr => dr.Time)) {
                    //no checks
                    float checkpoint = dutyResult.CheckpointResults.Count / 2f;
                    if(_plugin.DutyManager.Duties[dutyResult.DutyId].Structure == DutyStructure.Doors) {
                        checkpoint += 0.5f;
                    }
                    csv = csv + checkpoint.ToString() + ",";
                }
                ImGui.SetClipboardText(csv);
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
                if(ImGui.Button("Previous 100")) {
                    Refresh(_currentPage - 1);
                }
            }

            if(_dutyResults.Count >= 100) {
                ImGui.SameLine();
                if(ImGui.Button("Next 100")) {
                    Refresh(_currentPage + 1);
                }
            }
        }

        private void DrawDutyResults(DutyResults dutyResults) {
            List<string> lastCheckpoints = new() {
                "None"
            };
            string? owner = dutyResults.Owner ?? "";
            string? gil = dutyResults.TotalGil.ToString();
            bool isCompleted = dutyResults.IsComplete;
            foreach(var checkpoint in _plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!) {
                lastCheckpoints.Add(checkpoint.Name);
            }

            if(ImGui.CollapsingHeader(String.Format("{0:-23}     {1:-40}", dutyResults.Time.ToString(), dutyResults.DutyName))) {
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

                if(dutyResults.HasLootResults()) {
                    ImGui.BeginTable($"##{dutyResults.Id}--Loot", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip);
                    ImGui.TableSetupColumn($"quantity", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 45f);
                    ImGui.TableSetupColumn("item", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
                    ImGui.TableNextRow();

                    foreach(var lr in _lootResults[dutyResults.Id]) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{lr.Quantity}");
                        ImGui.TableNextColumn();
                        bool isPlural = lr.Quantity > 1;
                        //var row = _plugin.DataManager.GetExcelSheet<Item>().First(r => r.RowId == lr.ItemId);
                        //ImGui.Text($"{(isPlural ? row.Plural : row.Singular)}");
                        ImGui.Text($"{lr.ItemName}");
                    }
                    ImGui.EndTable();
                }
            }
        }
    }
}
