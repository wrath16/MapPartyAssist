using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace MapPartyAssist.Windows;

internal class MainWindow : Window {
    internal int MaxMaps => 11;

    private Plugin _plugin;
    private ZoneCountWindow _zoneCountWindow;
    private StatusMessageWindow _statusMessageWindow;

    private Dictionary<MPAMember, List<MPAMap>> _currentPlayerMaps = new();
    private Dictionary<MPAMember, List<MPAMap>> _recentPlayerMaps = new();
    private string? _lastMapPlayer;

    private int _currentPortalCount;
    private int _recentPortalCount;
    private int _currentMapCount;
    private int _recentMapCount;

    internal Vector2 CurrentPosition { get; private set; }
    internal Vector2 CurrentSize { get; private set; }

    private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

    internal MainWindow(Plugin plugin) : base("Map Party Assist") {
        ForceMainWindow = true;
        PositionCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(200, 50),
            MaximumSize = new Vector2(500, 350)
        };
        _plugin = plugin;

        _zoneCountWindow = new ZoneCountWindow(_plugin, this);
        _plugin.WindowSystem.AddWindow(_zoneCountWindow);

        _statusMessageWindow = new StatusMessageWindow(_plugin, this);
        _plugin.WindowSystem.AddWindow(_statusMessageWindow);
    }

    internal void Refresh() {

        _currentMapCount = 0;
        _recentMapCount = 0;
        _currentPortalCount = 0;
        _recentPortalCount = 0;

        //setup players independent of Plugin's recent and current lists
        _currentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
        foreach(var kvp in _plugin.CurrentPartyList) {
            _currentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
        }

        _recentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
        foreach(var kvp in _plugin.RecentPartyList) {
            _recentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
        }

        var maps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted && m.Owner != null).OrderBy(m => m.Time).ToList();
        foreach(var map in maps) {
            if(_plugin.CurrentPartyList.ContainsKey(map.Owner!)) {
                _currentPlayerMaps[_plugin.CurrentPartyList[map.Owner!]].Add(map);
                _currentMapCount++;
                if(map.IsPortal) {
                    _currentPortalCount++;
                }
            } else if(_plugin.RecentPartyList.ContainsKey(map.Owner!)) {
                _recentPlayerMaps[_plugin.RecentPartyList[map.Owner!]].Add(map);
                _recentMapCount++;
                if(map.IsPortal) {
                    _recentPortalCount++;
                }
            }
        }
        _lastMapPlayer = maps.LastOrDefault()?.Owner;
        _zoneCountWindow.Refresh();
    }

    public override void OnClose() {
        _zoneCountWindow.IsOpen = false;
        _statusMessageWindow.IsOpen = false;
        base.OnClose();
    }

    public override void PreDraw() {
        base.PreDraw();
        _zoneCountWindow.IsOpen = false;
        _statusMessageWindow.IsOpen = false;
    }

    public override void Draw() {
        PositionCondition = ImGuiCond.Once;
        CurrentPosition = ImGui.GetWindowPos();
        CurrentSize = ImGui.GetWindowSize();

        //set zone count window visibility
        if(_plugin.Configuration.HideZoneTableWhenEmpty) {
            _zoneCountWindow.IsOpen = _zoneCountWindow.Zones.Count > 0;
        } else {
            _zoneCountWindow.IsOpen = true;
        }

        //set status message window visibility
        _statusMessageWindow.IsOpen = !_plugin.MapManager.StatusMessage.IsNullOrEmpty();

        if(!_plugin.IsLanguageSupported()) {
            ImGui.TextColored(ImGuiColors.DalamudRed, $"Unsupported language, automatic tracking unavailable.");
        }

        if(ImGui.Button("Clear All")) {
            if(!_plugin.Configuration.RequireDoubleClickOnClearAll) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    _plugin.MapManager.ClearAllMaps();
                });
            }
        }
        if(ImGui.IsItemHovered()) {
            //check for double clicks
            if(ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                if(_plugin.Configuration.RequireDoubleClickOnClearAll) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        _plugin.MapManager.ClearAllMaps();
                    });
                }
            }
            if(_plugin.Configuration.RequireDoubleClickOnClearAll) {
                ImGui.BeginTooltip();
                ImGui.Text($"Double click to clear all.");
                ImGui.EndTooltip();
            }
        }

        ImGui.SameLine();
        ImGui.Text($"Total Maps: {_currentMapCount + _recentMapCount}");
        //if(ImGui.IsItemHovered()) {
        //    ImGui.BeginTooltip();
        //    ImGui.Text($"Including recent party members: {_currentMapCount + _recentMapCount}");
        //    ImGui.EndTooltip();
        //}
        ImGui.SameLine();
        ImGui.Text($"Total Portals: {_currentPortalCount + _recentPortalCount}");
        //if(ImGui.IsItemHovered()) {
        //    ImGui.BeginTooltip();
        //    ImGui.Text($"Including recent party members: {_currentPortalCount + _recentPortalCount}");
        //    ImGui.EndTooltip();
        //}
        //ImGui.Text($"{ImGuiHelpers.GlobalScale.ToString()}");

        if(_currentPlayerMaps.Count <= 0) {
            ImGui.Text("No party members currently.");
        } else {
            MapTable(_currentPlayerMaps);
        }

        if(_recentPlayerMaps.Count > 0) {
            ImGui.Text("Recent party members:");
            MapTable(_recentPlayerMaps);
        }
    }

    private void MapTable(Dictionary<MPAMember, List<MPAMap>> list, bool readOnly = false) {
        List<MPAMap> toArchive = new();
        List<MPAMap> toDelete = new();
        if(ImGui.BeginTable($"##{list.GetHashCode()}--MapsTable", MaxMaps + 2, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoKeepColumnsVisible)) {
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
            for(int i = 0; i < MaxMaps; i++) {
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
                if(playerMaps.Key.Equals(_lastMapPlayer)) {
                    ImGui.TextColored(ImGuiColors.DalamudYellow, $"{playerMaps.Key.Name.PadRight(20)}");
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        ImGui.Text($"Used most recent map.");
                        ImGui.EndTooltip();
                    }
                } else {
                    ImGui.Text($"{playerMaps.Key.Name.PadRight(20)}");
                }
                if(ImGui.BeginPopupContextItem($"##{playerMaps.Key.GetHashCode()}--NameContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                    if(ImGui.MenuItem($"Add map manually##{playerMaps.Key.GetHashCode()}--NameAddMap")) {
                        _plugin.DataQueue.QueueDataOperation(() => _plugin.MapManager.AddMap(playerMaps.Key, null, "Manually-added map", true));
                    } else if(ImGui.MenuItem($"Clear map link##{playerMaps.Key.GetHashCode()}--ClearMapLink")) {
                        _plugin.DataQueue.QueueDataOperation(() => _plugin.MapManager.ClearMapLink(playerMaps.Key));
                    }
                    ImGui.EndPopup();
                }
                if(playerMaps.Key.MapLink != null) {
                    //need to fix this for >1 scales...
                    ImGui.SameLine(ImGui.GetColumnWidth() - (158 - 151) * ImGuiHelpers.GlobalScale * ImGuiHelpers.GlobalScale);
                    ImGui.PushFont(UiBuilder.IconFont);
                    var linkColor = ImGuiColors.DalamudGrey;
                    if(_plugin.Configuration.HighlightLinksInCurrentZone) {
                        linkColor = playerMaps.Key.MapLink.GetMapLinkPayload().TerritoryType.RowId == _plugin.GetCurrentTerritoryId() ? ImGuiColors.DalamudYellow : linkColor;
                    }
                    if(_plugin.Configuration.HighlightClosestLink) {
                        MPAMember? closestLink = _plugin.MapManager.GetPlayerWithClosestMapLink(_plugin.CurrentPartyList.Values.ToList());
                        linkColor = closestLink != null && closestLink.Key == playerMaps.Key.Key ? ImGuiColors.DalamudOrange : linkColor;
                    }
                    ImGui.TextColored(linkColor, FontAwesomeIcon.Search.ToIconString());
                    ImGui.PopFont();
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        ImGui.Text($"{playerMaps.Key.MapLink.GetMapLinkPayload().PlaceName} {playerMaps.Key.MapLink.GetMapLinkPayload().CoordinateString}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.IsItemClicked()) {
                        _plugin.OpenMapLink(playerMaps.Key.MapLink.GetMapLinkPayload());
                    }
                }
                ImGui.TableNextColumn();
                //List<MPAMap> maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList();
                for(int i = 0; i < playerMaps.Value.Count() && i < MaxMaps; i++) {
                    var currentMap = playerMaps.Value.ElementAt(i);
                    ImGui.PushFont(UiBuilder.IconFont);
                    if(currentMap.IsManual) {
                        ImGui.TextColored(ImGuiColors.DalamudWhite, FontAwesomeIcon.Check.ToIconString());
                    } else {
                        ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
                    }
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
                    if(ImGui.BeginPopupContextItem($"##{currentMap.GetHashCode()}--MapContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Archive##{currentMap.GetHashCode()}--ArchiveMap")) {
                            toArchive.Add(playerMaps.Value[i]);
                        }
                        if(ImGui.MenuItem($"Delete##{currentMap.GetHashCode()}--DeleteMap")) {
                            toDelete.Add(playerMaps.Value[i]);
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();
                }
                for(int i = playerMaps.Value.Count(); i < MaxMaps; i++) {
                    ImGui.TableNextColumn();
                }

                if(playerMaps.Value.Count() > MaxMaps) {
                    var color = playerMaps.Value.Last().IsManual ? ImGuiColors.DalamudYellow : ImGuiColors.ParsedGreen;
                    ImGui.TextColored(color, $" +{playerMaps.Value.Count() - MaxMaps}");
                    if(ImGui.BeginPopupContextItem($"##{playerMaps.Key.GetHashCode()}--ExtraMapsContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Archive Last##{playerMaps.Key.GetHashCode()}--ExtraMapsArchive")) {
                            toArchive.Add(playerMaps.Value.Last());
                        }
                        if(ImGui.MenuItem($"Delete Last##{playerMaps.Key.GetHashCode()}--ExtraMapsDelete")) {
                            toDelete.Add(playerMaps.Value.Last());
                        }
                        ImGui.EndPopup();
                    }
                }
            }
            ImGui.EndTable();
        }
        if(toArchive.Count > 0) {
            _plugin.DataQueue.QueueDataOperation(() => {
                _plugin.MapManager.ArchiveMaps(toArchive);
            });
        }

        if(toDelete.Count > 0) {
            _plugin.DataQueue.QueueDataOperation(() => {
                _plugin.MapManager.DeleteMaps(toDelete);
            });
        }
    }
}
