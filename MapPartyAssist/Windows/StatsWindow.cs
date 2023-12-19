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
using System.Threading.Tasks;

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
        internal List<DataFilter> Filters { get; private set; } = new();

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        internal StatsWindow(Plugin plugin) : base("Treasure Map Statistics") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 400),
                MaximumSize = new Vector2(800, 1080)
            };
            _plugin = plugin;
            _viewImportsWindow = new ViewDutyResultsImportsWindow(plugin, this);
            _viewImportsWindow.IsOpen = false;
            _plugin.WindowSystem.AddWindow(_viewImportsWindow);

            Filters.Add(new MapFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.MapFilter));
            Filters.Add(new DutyFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.DutyFilter));
            Filters.Add(new TimeFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.TimeFilter));
            Filters.Add(new OwnerFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.OwnerFilter));
            Filters.Add(new PartyMemberFilter(plugin, Refresh, _plugin.Configuration.StatsWindowFilters.PartyMemberFilter));
            _lootSummary = new(plugin);
            _dutySummary = new(_plugin, this);
            //_lootSummary.Refresh(_dutyResults);
            _plugin.DataQueue.QueueDataOperation(Refresh);
        }

        public void Refresh() {
            var dutyResults = _plugin.StorageManager.GetDutyResults().Query().Include(dr => dr.Map).Where(dr => dr.IsComplete).OrderBy(dr => dr.Time).ToList();
            var maps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted).OrderBy(m => m.Time).ToList();

            if(_plugin.Configuration.CurrentCharacterStatsOnly && !_plugin.GetCurrentPlayer().IsNullOrEmpty()) {
                dutyResults = dutyResults.Where(dr => dr.Players.Contains(_plugin.GetCurrentPlayer())).ToList();
            }

            //apply filters
            foreach(var filter in Filters) {
                switch(filter.GetType()) {
                    case Type _ when filter.GetType() == typeof(DutyFilter):
                        var dutyFilter = (DutyFilter)filter;
                        dutyResults = dutyResults.Where(dr => dutyFilter.FilterState[dr.DutyId]).ToList();
                        //apply omit zero checkpoints
                        dutyResults = dutyResults.Where(dr => !_plugin.Configuration.DutyConfigurations[dr.DutyId].OmitZeroCheckpoints || dr.CheckpointResults.Count > 0).ToList();
                        _plugin.Configuration.StatsWindowFilters.DutyFilter = dutyFilter;
                        break;
                    case Type _ when filter.GetType() == typeof(MapFilter):
                        var mapFilter = (MapFilter)filter;
                        if(!mapFilter.IncludeMaps) {
                            maps = new();
                        }
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
                        if(partyMemberFilter.PartyMembers.Length <= 0) {
                            break;
                        }
#if DEBUG
                        foreach(var pm in partyMemberFilter.PartyMembers) {
                            _plugin.Log.Debug($"party member filter:|{pm}|");
                        }
#endif
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
                                var dutyFilter2 = (DutyFilter)Filters.Where(f => f.GetType() == typeof(DutyFilter)).First();
                                var lastClear = _plugin.StorageManager.GetDutyResults().Query().Where(dr => dr.IsComplete).OrderBy(dr => dr.Time).ToList()
                                    .Where(dr => dutyFilter2.FilterState[dr.DutyId] && dr.CheckpointResults.Count == _plugin.DutyManager.Duties[dr.DutyId].Checkpoints!.Count && dr.CheckpointResults.Last().IsReached).LastOrDefault();
                                if(lastClear != null) {
                                    dutyResults = dutyResults.Where(dr => dr.Time > lastClear.Time).ToList();
                                    maps = maps.Where(m => m.Time > lastClear.Time).ToList();
                                }
                                break;
                            case StatRange.AllLegacy:
                            case StatRange.All:
                            default:
                                break;
                        }
                        _plugin.Configuration.StatsWindowFilters.TimeFilter = timeFilter;
                        break;
                    default:
                        break;
                }
            }

            _lootSummary.Refresh(dutyResults, maps);
            _dutySummary.Refresh(dutyResults);
            //set configuration filters
            //foreach(var filter in Filters) {
            //    switch(filter.GetType()) {
            //        case Type _ when filter.GetType() == typeof(DutyFilter):
            //            var dutyFilter = (DutyFilter)filter;
            //            _plugin.Configuration.StatsWindowFilters.DutyFilter = dutyFilter;
            //            break;
            //        case Type _ when filter.GetType() == typeof(MapFilter):
            //            var mapFilter = (MapFilter)filter;
            //            _plugin.Configuration.StatsWindowFilters.MapFilter = mapFilter;
            //            break;
            //        case Type _ when filter.GetType() == typeof(OwnerFilter):
            //            var ownerFilter = (OwnerFilter)filter;
            //            _plugin.Configuration.StatsWindowFilters.OwnerFilter = ownerFilter;
            //            break;
            //        case Type _ when filter.GetType() == typeof(PartyMemberFilter):
            //            var pmFilter = (PartyMemberFilter)filter;
            //            _plugin.Configuration.StatsWindowFilters.PartyMemberFilter = pmFilter;
            //            break;
            //        case Type _ when filter.GetType() == typeof(TimeFilter):
            //            var timeFilter = (TimeFilter)filter;
            //            _plugin.Configuration.StatsWindowFilters.TimeFilter = timeFilter;
            //            break;
            //        default:
            //            break;
            //    }
            //}
            _plugin.Configuration.Save();
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

            ImGui.BeginChild("FilterChild", new Vector2(ImGui.GetContentRegionAvail().X, float.Max(ImGuiHelpers.GlobalScale * 150, ImGui.GetWindowHeight() / 4f)), true, ImGuiWindowFlags.AlwaysAutoResize);

            if(ImGui.BeginTable("FilterTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH)) {
                ImGui.BeginTable("FilterTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInner);
                ImGui.TableSetupColumn("filterName", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 110f);
                ImGui.TableSetupColumn($"filters", ImGuiTableColumnFlags.WidthStretch);
                //ImGui.TableNextRow();

                foreach(var filter in Filters) {
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
                    ImGui.Text($"   {nameText}");
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
                if(ImGui.BeginTabItem("Treasure Dungeon Summary")) {
                    if(ImGui.BeginChild("DutyResultsChild")) {
                        _dutySummary.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }

                if(ImGui.BeginTabItem("Loot Results")) {
                    if(ImGui.BeginChild("LootResultsChild")) {
                        _lootSummary.Draw();
                        ImGui.EndChild();
                    }
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

        }
    }
}
