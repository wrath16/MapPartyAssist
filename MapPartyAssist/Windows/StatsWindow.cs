using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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

    internal class StatsWindow : Window {

        private Plugin _plugin;
        private ViewDutyResultsImportsWindow _viewImportsWindow;

        private LootSummary _lootSummary;
        private DutyProgressSummary _dutySummary;
        private DutyResultsListView _dutyResultsList;
        private MapListView _mapList;
        private bool _collapseFilters;
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
            Filters.Add(new ImportFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.ImportFilter));
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
                var imports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted).OrderBy(i => i.Time).ToList();

                //DateTime dt2 = DateTime.Now;
                //_plugin.Log.Debug($"from db: {(dt2 - dt).TotalMilliseconds}ms");

                if(_plugin.Configuration.CurrentCharacterStatsOnly && !_plugin.GameStateManager.GetCurrentPlayer().IsNullOrEmpty()) {
                    dutyResults = dutyResults.Where(dr => dr.Players.Contains(_plugin.GameStateManager.GetCurrentPlayer())).ToList();
                    maps = maps.Where(m => m.Players == null || m.Players.Contains(_plugin.GameStateManager.GetCurrentPlayer())).ToList();
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
                            imports = imports.Where(i => dutyFilter.FilterState[i.DutyId]).ToList();
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
                            if(!trimmedOwner.IsNullOrEmpty()) {
                                dutyResults = dutyResults.Where(dr => dr.Owner.Contains(trimmedOwner, StringComparison.OrdinalIgnoreCase)).ToList();
                                maps = maps.Where(m => m.Owner is not null && m.Owner.Contains(trimmedOwner, StringComparison.OrdinalIgnoreCase)).ToList();
                                imports = new();
                            }
                            _plugin.Configuration.StatsWindowFilters.OwnerFilter = ownerFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(PartyMemberFilter):
                            var partyMemberFilter = (PartyMemberFilter)filter;
                            if(partyMemberFilter.OnlySolo) {
                                dutyResults = dutyResults.Where(dr => dr.Players.Length == 1).ToList();
                                maps = maps.Where(m => m.Players != null && m.Players.Length == 1).ToList();
                                imports = new();
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
                            imports = new();
                            _plugin.Configuration.StatsWindowFilters.PartyMemberFilter = partyMemberFilter;
                            break;
                        case Type _ when filter.GetType() == typeof(TimeFilter):
                            var timeFilter = (TimeFilter)filter;
                            switch(timeFilter.StatRange) {
                                case StatRange.Current:
                                    dutyResults = dutyResults.Where(dr => dr.Map != null && !dr.Map.IsArchived).ToList();
                                    maps = maps.Where(m => !m.IsArchived).ToList();
                                    imports = new();
                                    break;
                                case StatRange.PastDay:
                                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalHours < 24).ToList();
                                    maps = maps.Where(m => (DateTime.Now - m.Time).TotalHours < 24).ToList();
                                    imports = imports.Where(i => (DateTime.Now - i.Time).TotalHours < 24).ToList();
                                    break;
                                case StatRange.PastWeek:
                                    dutyResults = dutyResults.Where(dr => (DateTime.Now - dr.Time).TotalDays < 7).ToList();
                                    maps = maps.Where(m => (DateTime.Now - m.Time).TotalDays < 7).ToList();
                                    imports = imports.Where(i => (DateTime.Now - i.Time).TotalDays < 7).ToList();
                                    break;
                                case StatRange.ThisMonth:
                                    dutyResults = dutyResults.Where(dr => dr.Time.Month == DateTime.Now.Month && dr.Time.Year == DateTime.Now.Year).ToList();
                                    maps = maps.Where(m => m.Time.Month == DateTime.Now.Month && m.Time.Year == DateTime.Now.Year).ToList();
                                    imports = imports.Where(i => i.Time.Month == DateTime.Now.Month && i.Time.Year == DateTime.Now.Year).ToList();
                                    break;
                                case StatRange.LastMonth:
                                    var lastMonth = DateTime.Now.AddMonths(-1);
                                    dutyResults = dutyResults.Where(dr => dr.Time.Month == lastMonth.Month && dr.Time.Year == lastMonth.Year).ToList();
                                    maps = maps.Where(m => m.Time.Month == lastMonth.Month && m.Time.Year == lastMonth.Year).ToList();
                                    imports = imports.Where(i => i.Time.Month == lastMonth.Month && i.Time.Year == lastMonth.Year).ToList();
                                    break;
                                case StatRange.ThisYear:
                                    dutyResults = dutyResults.Where(dr => dr.Time.Year == DateTime.Now.Year).ToList();
                                    maps = maps.Where(m => m.Time.Year == DateTime.Now.Year).ToList();
                                    imports = imports.Where(i => i.Time.Year == DateTime.Now.Year).ToList();
                                    break;
                                case StatRange.LastYear:
                                    dutyResults = dutyResults.Where(dr => dr.Time.Year == DateTime.Now.AddYears(-1).Year).ToList();
                                    maps = maps.Where(m => m.Time.Year == DateTime.Now.AddYears(-1).Year).ToList();
                                    imports = imports.Where(i => i.Time.Year == DateTime.Now.AddYears(-1).Year).ToList();
                                    break;
                                case StatRange.SinceLastClear:
                                    foreach(var duty in _plugin.DutyManager.Duties.Where(d => dutyFilter.FilterState[d.Key])) {
                                        var lastClear = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete && dr.DutyId == duty.Key).OrderBy(dr => dr.Time).ToList()
                                            .Where(dr => dr.CheckpointResults.Count == _plugin.DutyManager.Duties[dr.DutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                                        if(lastClear != null) {
                                            dutyResults = dutyResults.Where(dr => dr.DutyId != duty.Key || dr.Time > lastClear.Time).ToList();
                                            imports = imports.Where(i => i.DutyId != duty.Key || i.Time > lastClear.Time).ToList();
                                            //this will default to the latest clear of last dungeon...
                                            maps = maps.Where(m => m.Time > lastClear.Time).ToList();
                                        }
                                    }
                                    break;
                                case StatRange.Custom:
                                    dutyResults = dutyResults.Where(dr => dr.Time > timeFilter.StartTime && dr.Time < timeFilter.EndTime).ToList();
                                    maps = maps.Where(m => m.Time > timeFilter.StartTime && m.Time < timeFilter.EndTime).ToList();
                                    imports = imports.Where(i => i.Time > timeFilter.StartTime && i.Time < timeFilter.EndTime).ToList();
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
                        case Type _ when filter.GetType() == typeof(ImportFilter):
                            var importFilter = (ImportFilter)filter;
                            if(!importFilter.IncludeImports) {
                                imports = new();
                            }
                            _plugin.Configuration.StatsWindowFilters.ImportFilter = importFilter;
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
            //if(!ImGui.Begin(WindowName)) {
            //    ImGui.End();
            //    return;
            //}

            if(ImGui.BeginMenuBar()) {
                try {
                    if(ImGui.BeginMenu("Windows")) {
                        try {
                            if(ImGui.MenuItem("Map Tracker", null, _plugin.MainWindow.IsOpen)) {
                                OpenMapWindow();
                            }
                        } finally {
                            ImGui.EndMenu();
                        }
                    }
                    if(ImGui.BeginMenu("Options")) {
                        try {
                            if(ImGui.MenuItem("Manage Imports")) {
                                OpenImportsWindow();
                            }
                            if(ImGui.MenuItem("Settings")) {
                                OpenConfigWindow();
                            }
                        } finally {
                            ImGui.EndMenu();
                        }
                    }
                } finally {
                    ImGui.EndMenuBar();
                }
            }

            if(!_collapseFilters) {
                using(var child = ImRaii.Child("FilterChild", new Vector2(ImGui.GetContentRegionAvail().X, float.Max(ImGuiHelpers.GlobalScale * 150, ImGui.GetWindowHeight() / 4f)), true, ImGuiWindowFlags.AlwaysAutoResize)) {
                    using(var table = ImRaii.Table("FilterTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH)) {
                        ImGui.TableSetupColumn("filterName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 110f);
                        ImGui.TableSetupColumn($"filters", ImGuiTableColumnFlags.WidthStretch);
                        //ImGui.TableNextRow();
                        foreach(var filter in Filters) {
                            ImGui.TableNextColumn();

                            if(filter.HelpMessage != null) {
                                ImGui.AlignTextToFramePadding();
                                ImGuiHelper.HelpMarker(filter.HelpMessage, false);
                                ImGui.SameLine();
                            }
                            string nameText = $"{filter.Name}:";
                            ImGuiHelper.RightAlignCursor2(nameText, -5f * ImGuiHelpers.GlobalScale);
                            ImGui.AlignTextToFramePadding();
                            //ImGui.SetCursorPosX(ImGui.GetCursorPosX() + float.Max(0, 16f - 4f * ImGuiHelpers.GlobalScale));
                            ImGui.TextUnformatted(nameText);
                            ImGui.TableNextColumn();
                            filter.Draw();
                        }
                    }
                }
            }
            //hide filter button
            try {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, -5 * ImGui.GetIO().FontGlobalScale));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                if(ImGui.Button($"{(_collapseFilters ? (char)FontAwesomeIcon.CaretDown : (char)FontAwesomeIcon.CaretUp)}", new Vector2(-1, 10 * ImGui.GetIO().FontGlobalScale))) {
                    _collapseFilters = !_collapseFilters;
                }
                ImGui.PopStyleVar(2);
            } finally {
                ImGui.PopFont();
            }
            ImGuiHelper.WrappedTooltip($"{(_collapseFilters ? "Show filters" : "Hide filters")}");

            using(var tabBar = ImRaii.TabBar("TabBar", ImGuiTabBarFlags.None)) {
                if(tabBar) {
                    using(var tab1 = ImRaii.TabItem("Duty Progress Summary")) {
                        if(tab1) {
                            using(var child = ImRaii.Child("DungeonSummaryChild")) {
                                _dutySummary.Draw();
                            }
                        }
                    }
                    using(var tab2 = ImRaii.TabItem("Loot")) {
                        if(tab2) {
                            using(var child = ImRaii.Child("LootResultsChild")) {
                                _lootSummary.Draw();
                            }
                        }
                    }
                    using(var tab3 = ImRaii.TabItem("Maps")) {
                        if(tab3) {
                            using(var child = ImRaii.Child("Maps")) {
                                _mapList.Draw();
                            }
                        }
                    }
                    using(var tab4 = ImRaii.TabItem("Duties")) {
                        if(tab4) {
                            using(var child = ImRaii.Child("DutyResultsChild")) {
                                _dutyResultsList.Draw();
                            }
                        }
                    }
                }
            }
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
