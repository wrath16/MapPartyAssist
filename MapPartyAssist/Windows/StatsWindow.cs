using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using MapPartyAssist.Windows.Filter;
using MapPartyAssist.Windows.Summary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace MapPartyAssist.Windows {

    public enum StatRange {
        Current,
        PastDay,
        PastWeek,
        SinceLastClear,
        All,
        AllLegacy
    }

    internal class StatsWindow : Window {

        private Plugin _plugin;
        private ViewDutyResultsImportsWindow _viewImportsWindow;

        private LootSummary _lootSummary;
        private DutyProgressSummary _dutySummary;
        private DutyResultsListView _dutyResultsList;
        private MapListView _mapList;
        internal List<DataFilter> Filters { get; private set; } = new();

        internal SemaphoreSlim RefreshLock { get; init; } = new SemaphoreSlim(1, 1);

        internal StatsWindow(Plugin plugin) : base("Treasure Hunt Statistics") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 400),
                MaximumSize = new Vector2(1500, 1080)
            };
            Flags |= ImGuiWindowFlags.MenuBar;
            _plugin = plugin;
            _viewImportsWindow = new ViewDutyResultsImportsWindow(plugin, this);
            _viewImportsWindow.IsOpen = false;
            _plugin.WindowSystem.AddWindow(_viewImportsWindow);

            Filters.Add(new MapFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.MapFilter));
            Filters.Add(new DutyFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.DutyFilter));
            Filters.Add(new TimeFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.TimeFilter));
            Filters.Add(new OwnerFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.OwnerFilter));
            Filters.Add(new PartyMemberFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.PartyMemberFilter));
            Filters.Add(new ProgressFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.ProgressFilter));
            Filters.Add(new MiscFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.MiscFilter));
            _lootSummary = new(plugin, this);
            _dutySummary = new(plugin, this);
            _dutyResultsList = new(plugin, this);
            _mapList = new(plugin, this);
            //_lootSummary.Refresh(_dutyResults);
            _plugin.DataQueue.QueueDataOperation(Refresh);
        }

        public void Refresh() {
            try {
                //_plugin.Log.Debug("start!");
                //DateTime dt = DateTime.Now;
                RefreshLock.Wait();
                var dutyResults = _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).OrderBy(dr => dr.Time).ToList();
                var maps = _plugin.StorageManager.GetMaps().Query().OrderBy(m => m.Time).ToList();
                var imports = new List<DutyResultsImport>();

                //DateTime dt2 = DateTime.Now;
                //_plugin.Log.Debug($"from db: {(dt2 - dt).TotalMilliseconds}ms");

                if(_plugin.Configuration.CurrentCharacterStatsOnly && !_plugin.GameStateManager.GetCurrentPlayer().IsNullOrEmpty()) {
                    dutyResults = dutyResults.Where(dr => dr.Players.Contains(_plugin.GameStateManager.GetCurrentPlayer())).ToList();
                }

                //apply filters
                var dutyFilter = (DutyFilter)Filters.Where(f => f.GetType() == typeof(DutyFilter)).First();
                foreach(var filter in Filters) {
                    switch(filter.GetType()) {
                        case Type _ when filter.GetType() == typeof(DutyFilter):
                            //var dutyFilter = (DutyFilter)filter;
                            dutyResults = dutyResults.Where(dr => dutyFilter.FilterState[dr.DutyId]).ToList();
                            //apply omit zero checkpoints
                            dutyResults = dutyResults.Where(dr => !_plugin.Configuration.DutyConfigurations[dr.DutyId].OmitZeroCheckpoints || dr.CheckpointResults.Count > 0).ToList();
                            _plugin.Configuration.StatsWindowFilters.DutyFilter = dutyFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(MapFilter):
                            var mapFilter = (MapFilter)filter;
                            maps = maps.Where(m => {
                                if(m.TerritoryId == null && mapFilter.FilterState[TreasureMapCategory.Unknown]) {
                                    return true;
                                } else if(m.TerritoryId != null && mapFilter.FilterState[MapHelper.GetCategory((int)m.TerritoryId)]) {
                                    return true;
                                }
                                return false;
                            }).ToList();
                            _plugin.Configuration.StatsWindowFilters.MapFilter = mapFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(OwnerFilter):
                            var ownerFilter = (OwnerFilter)filter;
                            string trimmedOwner = ownerFilter.Owner.Trim();
                            dutyResults = dutyResults.Where(dr => dr.Owner.Contains(trimmedOwner, StringComparison.OrdinalIgnoreCase)).ToList();
                            maps = maps.Where(m => m.Owner is not null && m.Owner.Contains(trimmedOwner, StringComparison.OrdinalIgnoreCase)).ToList();
                            _plugin.Configuration.StatsWindowFilters.OwnerFilter = ownerFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(PartyMemberFilter):
                            var partyMemberFilter = (PartyMemberFilter)filter;
                            if(partyMemberFilter.OnlySolo) {
                                dutyResults = dutyResults.Where(dr => dr.Players.Length == 1).ToList();
                                maps = maps.Where(m => m.Players != null && m.Players.Length == 1).ToList();
                            }

                            if(partyMemberFilter.PartyMembers.Length <= 0) {
                                break;
                            }
                            dutyResults = dutyResults.Where(dr => {
                                bool allMatch = true;
                                foreach(string partyMemberFilter in partyMemberFilter.PartyMembers) {
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
                            }).ToList();
                            maps = maps.Where(m => {
                                if(m.Players is null) {
                                    return false;
                                }
                                bool allMatch = true;
                                foreach(string partyMemberFilter in partyMemberFilter.PartyMembers) {
                                    bool matchFound = false;
                                    string partyMemberFilterTrimmed = partyMemberFilter.Trim();
                                    foreach(string partyMember in m.Players) {
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
                            }).ToList();
                            _plugin.Configuration.StatsWindowFilters.PartyMemberFilter = partyMemberFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(TimeFilter):
                            var timeFilter = (TimeFilter)filter;
                            switch(timeFilter.StatRange) {
                                case StatRange.Current:
                                    dutyResults = dutyResults.Where(dr => dr.Map != null && !dr.Map.IsArchived).ToList();
                                    maps = maps.Where(m => !m.IsArchived).ToList();
                                    break;
                                case StatRange.PastDay:
                                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalHours < 24).ToList();
                                    maps = maps.Where(m => (DateTime.Now - m.Time).TotalHours < 24).ToList();
                                    break;
                                case StatRange.PastWeek:
                                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalDays < 7).ToList();
                                    maps = maps.Where(m => (DateTime.Now - m.Time).TotalDays < 7).ToList();
                                    break;
                                case StatRange.SinceLastClear:
                                    foreach(var duty in _plugin.DutyManager.Duties.Where(d => dutyFilter.FilterState[d.Key])) {
                                        var lastClear = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == duty.Key).OrderBy(dr => dr.Time).ToList()
                                            .Where(dr => dr.CheckpointResults.Count == _plugin.DutyManager.Duties[dr.DutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                                        if(lastClear != null) {
                                            dutyResults = dutyResults.Where(dr => dr.DutyId != duty.Key || dr.Time > lastClear.Time).ToList();
                                            //this will default to the latest clear...
                                            maps = maps.Where(m => m.Time > lastClear.Time).ToList();
                                        }
                                    }
                                    break;
                                case StatRange.AllLegacy:
                                    imports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted).OrderBy(i => i.Time).ToList().Where(i => dutyFilter.FilterState[i.DutyId]).ToList();
                                    break;

                                case StatRange.All:
                                default:
                                    break;
                            }
                            _plugin.Configuration.StatsWindowFilters.TimeFilter = timeFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(ProgressFilter):
                            var progressFilter = (ProgressFilter)filter;
                            if(progressFilter.OnlyClears) {
                                dutyResults = dutyResults.Where(dr => dr.CheckpointResults.Count == _plugin.DutyManager.Duties[dr.DutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).ToList();
                            }
                            _plugin.Configuration.StatsWindowFilters.ProgressFilter = progressFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(MiscFilter):
                            var miscFilter = (MiscFilter)filter;
                            if(!miscFilter.ShowDeleted) {
                                maps = maps.Where(m => !m.IsDeleted).ToList();
                                dutyResults = dutyResults.Where(dr => dr.IsComplete).ToList();
                            }
                            if(miscFilter.LootOnly) {
                                maps = maps.Where(m => m.LootResults != null && m.LootResults.Count > 0).ToList();
                                dutyResults = dutyResults.Where(dr => dr.HasLootResults()).ToList();
                            }
                            _plugin.Configuration.StatsWindowFilters.MiscFilter = miscFilter;
                            break;
                        default:
                            break;
                    }
                }
                //DateTime dt3 = DateTime.Now;
                //_plugin.Log.Debug($"filters: {(dt3 - dt2).TotalMilliseconds}ms");

                _lootSummary.Refresh(dutyResults, maps);

                //DateTime dt4 = DateTime.Now;
                //_plugin.Log.Debug($"loot summary refresh: {(dt4 - dt3).TotalMilliseconds}ms");

                _dutySummary.Refresh(dutyResults, imports);

                //DateTime dt5 = DateTime.Now;
                //_plugin.Log.Debug($"duty summary refresh: {(dt5 - dt4).TotalMilliseconds}ms");

                _dutyResultsList.Refresh(dutyResults);

                //DateTime dt6 = DateTime.Now;
                //_plugin.Log.Debug($"duty list refresh: {(dt6 - dt5).TotalMilliseconds}ms");

                _mapList.Refresh(maps);

                //DateTime dt7 = DateTime.Now;
                //_plugin.Log.Debug($"map list refresh: {(dt7 - dt6).TotalMilliseconds}ms");

                _viewImportsWindow.Refresh();

                //DateTime dt8 = DateTime.Now;
                //_plugin.Log.Debug($"imports refresh: {(dt8 - dt7).TotalMilliseconds}ms");

                _plugin.Configuration.Save();

                //DateTime dt9 = DateTime.Now;
                //_plugin.Log.Debug($"save plugin: {(dt9 - dt8).TotalMilliseconds}ms");


            } finally {
                RefreshLock.Release();
            }
        }

        public override void OnClose() {
            _viewImportsWindow.IsOpen = false;
            base.OnClose();
        }

        public override void Draw() {
            //draw filters
            //ImGui.BeginChild($"filterChild##{GetHashCode()}", new Vector2(ImGui.GetWindowPos().X, 150f * ImGuiHelpers.GlobalScale));
            //if(ImGui.BeginChild($"filterChild##{GetHashCode()}", new Vector2(ImGui.GetWindowPos().X, 150f * ImGuiHelpers.GlobalScale))) {

            //    ImGui.EndChild();
            //}
            if(!ImGui.Begin(WindowName)) {
                ImGui.End();
                return;
            }

            if(ImGui.BeginMenuBar()) {
                if(ImGui.BeginMenu("Windows")) {
                    if(ImGui.MenuItem("Map Tracker", null, _plugin.MainWindow.IsOpen)) {
                        OpenMapWindow();
                    }
                    ImGui.EndMenu();
                }
                if(ImGui.BeginMenu("Options")) {
                    if(ImGui.MenuItem("Manage Imports")) {
                        OpenImportsWindow();
                    }
                    if(ImGui.MenuItem("Settings")) {
                        OpenConfigWindow();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }

            ImGui.BeginChild("FilterChild", new Vector2(ImGui.GetContentRegionAvail().X, float.Max(ImGuiHelpers.GlobalScale * 150, ImGui.GetWindowHeight() / 4f)), true, ImGuiWindowFlags.AlwaysAutoResize);

            if(ImGui.BeginTable("FilterTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH)) {
                ImGui.BeginTable("FilterTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInner);
                ImGui.TableSetupColumn("filterName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 110f);
                ImGui.TableSetupColumn($"filters", ImGuiTableColumnFlags.WidthStretch);
                //ImGui.TableNextRow();

                foreach(var filter in Filters) {
                    //ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, 4);
                    ImGui.TableNextColumn();

                    if(filter.HelpMessage != null) {
                        ImGui.AlignTextToFramePadding();
                        ImGuiHelper.HelpMarker(filter.HelpMessage);
                        ImGui.SameLine();
                    }
                    //ImGui.GetStyle().FramePadding.X = ImGui.GetStyle().FramePadding.X - 2f;
                    string nameText = $"{filter.Name}:";
                    ImGuiHelper.RightAlignCursor(nameText);
                    ImGui.AlignTextToFramePadding();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + float.Max(0, 16f - 4f * ImGuiHelpers.GlobalScale));
                    ImGui.Text($"{nameText}");
                    //ImGui.PopStyleVar();
                    //ImGui.GetStyle().FramePadding.X = ImGui.GetStyle().FramePadding.X + 2f;
                    ImGui.TableNextColumn();
                    if(filter.GetType() == typeof(TimeFilter)) {
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2f);
                    }
                    filter.Draw();
                }
                ImGui.EndTable();
            }
            ImGui.EndChild();

            if(ImGui.BeginTabBar("TabBar", ImGuiTabBarFlags.None)) {
                if(ImGui.BeginTabItem("Duty Progress Summary")) {
                    if(ImGui.BeginChild("DungeonSummaryChild")) {
                        _dutySummary.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Loot")) {
                    if(ImGui.BeginChild("LootResultsChild")) {
                        _lootSummary.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Maps")) {
                    if(ImGui.BeginChild("Maps")) {
                        _mapList.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("Duties")) {
                    if(ImGui.BeginChild("DutyResultsChild")) {
                        _dutyResultsList.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        internal void OpenImportsWindow() {
            if(!_viewImportsWindow.IsOpen) {
                _viewImportsWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                _viewImportsWindow.IsOpen = true;
            }
            _viewImportsWindow.BringToFront();
        }

        internal void OpenConfigWindow() {
            if(!_plugin.ConfigWindow.IsOpen) {
                _plugin.ConfigWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                _plugin.ConfigWindow.IsOpen = true;
            }
            _plugin.ConfigWindow.BringToFront();
        }

        internal void OpenMapWindow() {
            if(!_plugin.MainWindow.IsOpen) {
                _plugin.MainWindow.PositionCondition = ImGuiCond.Appearing;
                _plugin.MainWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                _plugin.MainWindow.IsOpen = true;
            }
            _plugin.MainWindow.BringToFront();
        }
    }
}
