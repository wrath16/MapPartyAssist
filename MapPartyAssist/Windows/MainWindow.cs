using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace MapPartyAssist.Windows;

public class MainWindow : Window, IDisposable {
    private Plugin Plugin;
    private ZoneCountWindow ZoneCountWindow;

    private static int _maxMaps = 11;


    private Dictionary<MPAMember, List<MPAMap>> _currentPlayerMaps;
    private Dictionary<MPAMember, List<MPAMap>> _recentPlayerMaps;

    private int _currentPortalCount;
    private int _recentPortalCount;
    private int _currentMapCount;
    private int _recentMapCount;

    private SemaphoreSlim _updateMapsLock;

    public Vector2 CurrentPosition { get; private set; }
    public Vector2 CurrentSize { get; private set; }

    public MainWindow(Plugin plugin) : base(
        "Map Party Assist") {
        this.ForceMainWindow = true;
        this.PositionCondition = ImGuiCond.Always;
        this.SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(200, 100),
            MaximumSize = new Vector2(500, 350)
        };
        this.Plugin = plugin;
        //create new zoneCountWindow
        ZoneCountWindow = new ZoneCountWindow(Plugin, this);
        Plugin.WindowSystem.AddWindow(ZoneCountWindow);

        _updateMapsLock = new SemaphoreSlim(1, 1);
    }

    public void Dispose() {
    }

    public void Refresh() {
        UpdateMaps();
    }

    //private string SortList(KeyValuePair<MPAMember, List<MPAMap>> kvp) {
    //    if(kvp.Key.IsSelf) {
    //        return "";
    //    } else {
    //        return kvp.Key.Key;
    //    }
    //}

    private void UpdateMaps() {

        _updateMapsLock.Wait();
        //PluginLog.Debug("Updating maps windoW!");


        _currentMapCount = 0;
        _recentMapCount = 0;
        _currentPortalCount = 0;
        _recentPortalCount = 0;

        //setup players independent of Plugin's recent and current lists
        _currentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
        foreach(var kvp in Plugin.CurrentPartyList) {
            _currentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
        }
        //_currentPlayerMaps = _currentPlayerMaps.OrderBy(kvp => {
        //    if(kvp.Key.IsSelf) {
        //        return "";
        //    } else {
        //        return kvp.Key.Key;
        //    }
        //}).ToDictionary<MPAMember, List<MPAMap>>((x => x.Key, x => x.Value));

        _recentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
        foreach(var kvp in Plugin.RecentPartyList) {
            _recentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
        }
        //_recentPlayerMaps = _recentPlayerMaps.OrderBy(kvp => {
        //    if(kvp.Key.IsSelf) {
        //        return "";
        //    } else {
        //        return kvp.Key.Key;
        //    }
        //}).ToDictionary<MPAMember, List<MPAMap>>(kvp => kvp);

        var maps = Plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted && m.Owner != null).OrderBy(m => m.Time).ToList();
        foreach(var map in maps) {
            if(Plugin.CurrentPartyList.ContainsKey(map.Owner)) {
                _currentPlayerMaps[Plugin.CurrentPartyList[map.Owner]].Add(map);
                _currentMapCount++;
                if(map.IsPortal) {
                    _currentPortalCount++;
                }
            } else if(Plugin.RecentPartyList.ContainsKey(map.Owner)) {
                _recentPlayerMaps[Plugin.RecentPartyList[map.Owner]].Add(map);
                _recentMapCount++;
                if(map.IsPortal) {
                    _recentPortalCount++;
                }
            }
        }

        //PluginLog.Debug($"total maps: {_currentMapCount}");
        _updateMapsLock.Release();
    }

    public override void OnClose() {
        ZoneCountWindow.IsOpen = false;
        base.OnClose();
    }

    public override void PreDraw() {
        base.PreDraw();
        ZoneCountWindow.IsOpen = false;
        //CurrentPosition = ImGui.GetWindowPos();
        //CurrentSize = ImGui.GetWindowSize();
    }

    public override void Draw() {
        CurrentPosition = ImGui.GetWindowPos();
        CurrentSize = ImGui.GetWindowSize();

        if(ImGui.Button("Clear All")) {
            Plugin.MapManager.ClearAllMaps();
        }

        //int totalMapsCurrent = 0;
        //int totalMapsRecent = 0;
        //int totalPortalsCurrent = 0;
        //int totalPortalsRecent = 0;
        //foreach(var p in Plugin.CurrentPartyList) {
        //    totalMapsCurrent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList().Count;
        //    totalPortalsCurrent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived && m.IsPortal).ToList().Count;
        //}
        //foreach(var p in Plugin.RecentPartyList) {
        //    totalMapsRecent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList().Count;
        //    totalPortalsRecent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived && m.IsPortal).ToList().Count;
        //}

        ImGui.SameLine();
        ImGui.Text($"Total Maps: {_currentMapCount}");
        if(ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text($"Including recent party members: {_currentMapCount + _recentMapCount}");
            ImGui.EndTooltip();
        }
        ImGui.SameLine();
        ImGui.Text($"Total Portals: {_currentPortalCount}");
        if(ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text($"Including recent party members: {_currentPortalCount + _recentPortalCount}");
            ImGui.EndTooltip();
        }
        //ImGui.Text($"{ImGuiHelpers.GlobalScale.ToString()}");

        if(_currentPlayerMaps.Count <= 0) {
            ImGui.Text("No party members currently.");
        } else {
            MapTable(_currentPlayerMaps);
        }


        //Plugin.WindowSystem.GetWindow("Map Links by Zone").IsOpen = Plugin.CurrentPartyList.Count > 0;
        ZoneCountWindow.IsOpen = true;

        //var recentPartyList = Plugin.Configuration.RecentPartyList.Where(p => {
        //    TimeSpan timeSpan = DateTime.Now - p.Value.LastJoined;
        //    var isRecent = timeSpan.TotalHours <= Plugin.Configuration.ArchiveThresholdHours;
        //    var hasMaps = false;
        //    foreach(var m in p.Value.Maps) {
        //        if(!m.IsArchived && !m.IsDeleted) {
        //            hasMaps = true;
        //            break;
        //        }
        //    }
        //    var notCurrent = !Plugin.CurrentPartyList.ContainsKey(p.Key);
        //    var notSelf = !p.Value.IsSelf;
        //    return isRecent && hasMaps && notCurrent && notSelf;
        //}).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if(_recentPlayerMaps.Count > 0) {
            ImGui.Text("Recent party members:");
            MapTable(_recentPlayerMaps);
        }

        //ImGui.BeginChildFrame(111, new Vector2(200, 50), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        //ImGui.Text("Test2");
        ////ImGui.EndChild();
        //ImGui.EndChildFrame();

        //Plugin.WindowSystem.GetWindow("Map Links").IsOpen = true;
        //MapTable(Plugin.FakePartyList);
    }

    private void MapTable(Dictionary<MPAMember, List<MPAMap>> list, bool readOnly = false) {

        if(ImGui.BeginTable($"##{list.GetHashCode()}_Maps_Table", _maxMaps + 2, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoKeepColumnsVisible)) {
            List<MPAMap> toArchive = new();
            List<MPAMap> toDelete = new();
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
            //ImGui.TableSetupColumn("addNew", ImGuiTableColumnFlags.WidthFixed, 22f);
            //ImGui.TableSetupColumn("mapLink", ImGuiTableColumnFlags.WidthFixed, 15f);
            for(int i = 0; i < _maxMaps; i++) {
                ImGui.TableSetupColumn($"map{i + 1}", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 15f);
            }
            ImGui.TableSetupColumn($"extraMaps", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 24f);
            foreach(var playerMaps in list.OrderBy(kvp => {
                if(kvp.Key.IsSelf) {
                    return "";
                } else {
                    return kvp.Key.Key;
                }
            })) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if(playerMaps.Key.Equals(Plugin.MapManager.LastMapPlayerKey)) {
                    ImGui.TextColored(ImGuiColors.DalamudYellow, $"{playerMaps.Key.Name.PadRight(20)}");
                } else {
                    ImGui.Text($"{playerMaps.Key.Name.PadRight(20)}");
                }
                //ImGui.Text($"{player.Value.Name.PadRight(20)}");
                if(ImGui.BeginPopupContextItem($"##{playerMaps.Key.GetHashCode()}--NameContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                    if(ImGui.MenuItem($"Add map manually##{playerMaps.Key.GetHashCode()}--NameAddMap")) {
                        Plugin.MapManager.AddMap(playerMaps.Key, "", "Manually-added map.", true);
                    }
                    ImGui.EndPopup();
                }
                //ImGui.Text($"{player.Value.HomeWorld}");
                if(playerMaps.Key.MapLink != null) {
                    //ImGui.SameLine(ImGuiHelpers.GlobalScale * 151);
                    //need to fix this for larger scales...
                    ImGui.SameLine(ImGui.GetColumnWidth() - (158 - 151) * ImGuiHelpers.GlobalScale * ImGuiHelpers.GlobalScale);

                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudGrey, FontAwesomeIcon.Search.ToIconString());
                    ImGui.PopFont();
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        ImGui.Text($"{playerMaps.Key.MapLink.GetMapLinkPayload().PlaceName} {playerMaps.Key.MapLink.GetMapLinkPayload().CoordinateString}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.IsItemClicked()) {

                        Plugin.OpenMapLink(playerMaps.Key.MapLink.GetMapLinkPayload());
                    }
                }
                ImGui.TableNextColumn();
                //List<MPAMap> maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList();
                for(int i = 0; i < playerMaps.Value.Count() && i < _maxMaps; i++) {
                    var currentMap = playerMaps.Value.ElementAt(i);
                    ImGui.PushFont(UiBuilder.IconFont);
                    if(currentMap.IsManual) {
                        ImGui.TextColored(ImGuiColors.DalamudYellow, FontAwesomeIcon.Check.ToIconString());
                    } else {
                        ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
                    }
                    //ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
                    ImGui.PopFont();
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        if(!currentMap.DutyName.IsNullOrEmpty()) {
                            ImGui.Text($"{currentMap.DutyName}");
                        }
                        if(!currentMap.Name.IsNullOrEmpty()) {
                            ImGui.Text($"{currentMap.Name}");
                        }
                        if(!currentMap.Zone.IsNullOrEmpty()) {
                            ImGui.Text($"{currentMap.Zone}");
                        }
                        ImGui.Text($"{currentMap.Time}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.BeginPopupContextItem($"##{currentMap.GetHashCode()}--ContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Archive##{currentMap.GetHashCode()}")) {
                            toArchive.Add(playerMaps.Value[i]);
                        }
                        if(ImGui.MenuItem($"Delete##{currentMap.GetHashCode()}")) {
                            toDelete.Add(playerMaps.Value[i]);
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();
                }
                for(int i = playerMaps.Value.Count(); i < _maxMaps; i++) {
                    ImGui.TableNextColumn();
                }

                if(playerMaps.Value.Count() > _maxMaps) {
                    var color = playerMaps.Value.Last().IsManual ? ImGuiColors.DalamudYellow : ImGuiColors.ParsedGreen;
                    ImGui.TextColored(color, $" +{playerMaps.Value.Count() - _maxMaps}");
                    if(ImGui.BeginPopupContextItem($"##{playerMaps.Key.GetHashCode()}--ExtraMapsContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Archive Last##{playerMaps.Key.GetHashCode()}--ExtraMapsArchive")) {
                            //Plugin.MapManager.RemoveLastMap(playerMaps.Key);
                            toArchive.Add(playerMaps.Value.Last());
                        }
                        if(ImGui.MenuItem($"Delete Last##{playerMaps.Key.GetHashCode()}--ExtraMapsDelete")) {
                            //Plugin.MapManager.RemoveLastMap(playerMaps.Key);
                            toDelete.Add(playerMaps.Value.Last());
                        }
                        ImGui.EndPopup();
                    }
                }
            }

            //todo move these to plugin layer
            //foreach(var map in toArchive) {
            //    map.IsArchived = true;
            //}

            //foreach(var map in toDelete) {
            //    map.IsDeleted = true;
            //}

            if(toArchive.Count > 0) {
                Plugin.MapManager.ArchiveMaps(toArchive);
            }

            if(toDelete.Count > 0) {
                Plugin.MapManager.DeleteMaps(toDelete);
            }


            //if(toArchive.Count > 0 || toDelete.Count > 0) {
            //    Plugin.MapManager.DeleteMaps(toDelete);
            //    Plugin.MapManager.ArchiveMaps(toArchive);
            //    Plugin.Save();
            //}
            ImGui.EndTable();
        }
    }
}
