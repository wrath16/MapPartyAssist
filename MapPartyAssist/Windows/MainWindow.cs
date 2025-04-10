using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using LiteDB;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MapPartyAssist.Windows;

internal class MainWindow : Window {
    internal int MaxMaps => 11;

    private Plugin _plugin;
    private ZoneCountWindow _zoneCountWindow;
    private StatusMessageWindow _statusMessageWindow;

    private Dictionary<MPAMember, List<MPAMap>> _currentPlayerMaps = new();
    private Dictionary<MPAMember, List<MPAMap>> _recentPlayerMaps = new();
    private List<MPAMap> _unknownOwnerMaps = new();
    private string? _lastMapPlayer;
    private string? _currentDragDropSource;

    private int _currentPortalCount;
    //private int _recentPortalCount;
    private int _currentMapCount;
    //private int _recentMapCount;

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
        try {
            _refreshLock.WaitAsync();
            _currentMapCount = 0;
            //_recentMapCount = 0;
            _currentPortalCount = 0;
            //_recentPortalCount = 0;

            //setup players independent of Plugin's recent and current lists
            _currentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
            foreach(var kvp in _plugin.GameStateManager.CurrentPartyList) {
                _currentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
            }

            _recentPlayerMaps = new Dictionary<MPAMember, List<MPAMap>>();
            foreach(var kvp in _plugin.GameStateManager.RecentPartyList) {
                _recentPlayerMaps.Add(kvp.Value, new List<MPAMap>());
            }
            _unknownOwnerMaps = new();
            var maps = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsArchived && !m.IsDeleted).OrderBy(m => m.Time).ToList();
            //_unknownOwnerMaps = maps.Where(m => m.Owner is null).OrderBy(m => m.Time).ToList();
            foreach(var map in maps) {

                if(map.Owner is null) {
                    _unknownOwnerMaps.Add(map);
                } else if(_plugin.GameStateManager.CurrentPartyList.ContainsKey(map.Owner)) {
                    _currentPlayerMaps[_plugin.GameStateManager.CurrentPartyList[map.Owner]].Add(map);
                } else if(_plugin.GameStateManager.RecentPartyList.ContainsKey(map.Owner)) {
                    _recentPlayerMaps[_plugin.GameStateManager.RecentPartyList[map.Owner]].Add(map);
                }

                _currentMapCount++;
                if(map.IsPortal) {
                    _currentPortalCount++;
                }
            }
            _lastMapPlayer = maps.LastOrDefault()?.Owner;
            _zoneCountWindow.Refresh();
        } finally {
            _refreshLock.Release();
        }
    }

    public override void OnClose() {
        if(!_plugin.Configuration.UndockZoneWindow) {
            _zoneCountWindow.IsOpen = false;
        }
        _statusMessageWindow.IsOpen = false;
        base.OnClose();
    }

    public override void OnOpen() {
        base.OnOpen();
        _zoneCountWindow.IsOpen = true;
    }

    public override void PreDraw() {
        base.PreDraw();
        if(!_plugin.Configuration.UndockZoneWindow) {
            _zoneCountWindow.IsOpen = false;
        }
        _statusMessageWindow.IsOpen = false;
    }

    public override void Draw() {
        PositionCondition = ImGuiCond.Once;
        CurrentPosition = ImGui.GetWindowPos();
        CurrentSize = ImGui.GetWindowSize();

        if(!_plugin.Configuration.UndockZoneWindow && !_plugin.Configuration.HideZoneTable) {
            if(_plugin.Configuration.HideZoneTableWhenEmpty) {
                _zoneCountWindow.IsOpen = _zoneCountWindow.Zones.Count > 0;
            } else {
                _zoneCountWindow.IsOpen = true;
            }
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
        ImGui.Text($"Total Maps: {_currentMapCount}");
        //if(ImGui.IsItemHovered()) {
        //    ImGui.BeginTooltip();
        //    ImGui.Text($"Including recent party members: {_currentMapCount + _recentMapCount}");
        //    ImGui.EndTooltip();
        //}
        ImGui.SameLine();
        ImGui.Text($"Total Portals: {_currentPortalCount}");
        //if(ImGui.IsItemHovered()) {
        //    ImGui.BeginTooltip();
        //    ImGui.Text($"Including recent party members: {_currentPortalCount + _recentPortalCount}");
        //    ImGui.EndTooltip();
        //}
        //ImGui.Text($"{ImGuiHelpers.GlobalScale.ToString()}");

        ImGuiHelper.HelpMarker(() => {
            ImGui.TextColored(ImGuiColors.ParsedGreen, "Automatically added.");
            ImGui.TextColored(ImGuiColors.DalamudWhite, "Manually added.");
            ImGui.TextColored(ImGuiColors.ParsedBlue, "Manually re-assigned.");
            ImGui.TextColored(ImGuiColors.ParsedOrange, "Possibly wrong owner.");
            ImGui.TextColored(ImGuiColors.DalamudRed, "Unknown owner.");
        });

        //if(!_refreshLock.Wait(0)) {
        //    return;
        //}
        //try {
        //    if(_currentPlayerMaps.Count <= 0) {
        //        ImGui.Text("No party members currently.");
        //    } else {
        //        MapTable(_currentPlayerMaps);
        //    }

        //    if(_recentPlayerMaps.Count > 0) {
        //        ImGui.Text("Recent party members:");
        //        MapTable(_recentPlayerMaps);
        //    }

        //    if(_unknownOwnerMaps.Count > 0) {
        //        UnknownMapTable();
        //    }

        //} finally {
        //    _refreshLock.Release();
        //}

        bool refreshLockAcquired = _refreshLock.Wait(0);
        try {
            if(_currentPlayerMaps.Count <= 0) {
                ImGui.Text("No party members currently.");
            } else {
                MapTable(_currentPlayerMaps);
            }

            if(_recentPlayerMaps.Count > 0) {
                ImGui.Text("Recent party members:");
                MapTable(_recentPlayerMaps);
            }

            if(_unknownOwnerMaps.Count > 0) {
                UnknownMapTable();
            }
        } catch {
            //suppress all exceptions while a refresh is in progress
            if(refreshLockAcquired) {
                _plugin.Log.Debug("draw error on refresh lock acquired.");
                throw;
            }
        } finally {
            if(refreshLockAcquired) {
                _refreshLock.Release();
            }
        }
    }

    private void MapTable(Dictionary<MPAMember, List<MPAMap>> list, bool readOnly = false) {
        //List<MPAMap> toArchive = new();
        //List<MPAMap> toDelete = new();

        using var table = ImRaii.Table($"##{list.GetHashCode()}--MapsTable", MaxMaps + 2, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoKeepColumnsVisible);
        if(table) {
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
                    ImGuiHelper.WrappedTooltip("Used most recent map.");
                } else {
                    ImGui.Text($"{playerMaps.Key.Name.PadRight(20)}");
                }
                MapDragDropTarget(playerMaps.Key);

                using(var popup = ImRaii.ContextPopupItem($"##{playerMaps.Key.GetHashCode()}--NameContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                    if(popup) {
                        if(ImGui.MenuItem($"Announce map link to party chat##{playerMaps.Key.GetHashCode()}--NameLinkInChat")) {
                            var mapLink = playerMaps.Key.MapLink;
                            if(mapLink != null) {
                                try {
                                    //there's some rounding silliness here
                                    _plugin.Functions.SetFlagMarker(mapLink.TerritoryTypeId, mapLink.MapId, mapLink.RawX / 1000f, mapLink.RawY / 1000f);
                                    var message = _plugin.Configuration.MapLinkChat ?? "<flag>";
                                    message = message.Replace("<name>", playerMaps.Key.Name);
                                    message = message.Replace("<fullname>", playerMaps.Key.Key);
                                    message = message.Replace("<firstname>", playerMaps.Key.FirstName);
                                    _plugin.MapManager.AnnounceBlockUntil = DateTime.Now + TimeSpan.FromSeconds(1);
#if DEBUG
                                    if(_plugin.GameStateManager.CurrentPartyList.Count <= 1) {
                                        _plugin.Functions.SendChatMessage($"/say {message}");
                                    }
#endif
                                    _plugin.Functions.SendChatMessage($"/p {message}");
                                } catch(Exception ex) {
                                    _plugin.Log.Error(ex, "Unable to send chat message");
                                }
                            }
                        } else if(ImGui.MenuItem($"Add map manually##{playerMaps.Key.GetHashCode()}--NameAddMap")) {
                            _plugin.DataQueue.QueueDataOperation(() => _plugin.MapManager.AddMap(playerMaps.Key, null, "Manually-added map", true));
                        } else if(ImGui.MenuItem($"Restore last map link##{playerMaps.Key.GetHashCode()}--RestoreMapLink")) {
                            _plugin.DataQueue.QueueDataOperation(() => {
                                //I should make this value-type
                                var prevLink = playerMaps.Key.PreviousMapLink;
                                if(prevLink != null) {
                                    var prevLinkNew = new MPAMapLink(playerMaps.Key.PreviousMapLink!.TerritoryTypeId, playerMaps.Key.PreviousMapLink.MapId, playerMaps.Key.PreviousMapLink.RawX, playerMaps.Key.PreviousMapLink.RawY);
                                    playerMaps.Key.SetMapLink(prevLinkNew);
                                    _plugin.StorageManager.UpdatePlayer(playerMaps.Key);
                                }
                            });
                        } else if(ImGui.MenuItem($"Clear map link##{playerMaps.Key.GetHashCode()}--ClearMapLink")) {
                            _plugin.DataQueue.QueueDataOperation(() => {
                                playerMaps.Key.SetMapLink(null);
                                _plugin.StorageManager.UpdatePlayer(playerMaps.Key);
                            });
                        }
                    }
                }
                if(playerMaps.Key.MapLink != null) {
                    //need to fix this for >1 scales...
                    ImGui.SameLine(ImGui.GetColumnWidth() - (158 - 151) * ImGuiHelpers.GlobalScale * ImGuiHelpers.GlobalScale);
                    using(var font = ImRaii.PushFont(UiBuilder.IconFont)) {
                        var linkColor = ImGuiColors.DalamudGrey;
                        if(_plugin.Configuration.HighlightLinksInCurrentZone) {
                            linkColor = playerMaps.Key.MapLink.GetMapLinkPayload().TerritoryType.RowId == _plugin.GameStateManager.CurrentTerritory ? ImGuiColors.DalamudYellow : linkColor;
                        }
                        if(_plugin.Configuration.HighlightClosestLink) {
                            MPAMember? closestLink = _plugin.MapManager.GetPlayerWithClosestMapLink(_plugin.GameStateManager.CurrentPartyList.Values.ToList());
                            linkColor = closestLink != null && closestLink.Key == playerMaps.Key.Key ? ImGuiColors.DalamudOrange : linkColor;
                        }
                        ImGui.TextColored(linkColor, FontAwesomeIcon.Search.ToIconString());
                    }
                    ImGuiHelper.WrappedTooltip($"{playerMaps.Key.MapLink.GetMapLinkPayload().PlaceName} {playerMaps.Key.MapLink.GetMapLinkPayload().CoordinateString}");
                    if(ImGui.IsItemClicked()) {
                        _plugin.OpenMapLink(playerMaps.Key.MapLink.GetMapLinkPayload());
                    }
                }
                //List<MPAMap> maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList();
                for(int i = 0; i < playerMaps.Value.Count() && i < MaxMaps; i++) {
                    var currentMap = playerMaps.Value.ElementAt(i);
                    ImGui.TableNextColumn();
                    MapCell(currentMap);
                }
                for(int i = playerMaps.Value.Count(); i < MaxMaps; i++) {
                    ImGui.TableNextColumn();
                }

                if(playerMaps.Value.Count() > MaxMaps) {
                    ImGui.TableNextColumn();
                    var lastMap = playerMaps.Value.Last();
                    var color = ImGuiColors.ParsedGreen;
                    color = lastMap.IsAmbiguousOwner ? ImGuiColors.ParsedOrange : color;
                    color = lastMap.IsManual ? ImGuiColors.DalamudWhite : color;
                    color = lastMap.IsReassigned ? ImGuiColors.ParsedBlue : color;
                    color = lastMap.Owner == null ? ImGuiColors.DalamudRed : color;
                    ImGui.TextColored(color, $" +{playerMaps.Value.Count() - MaxMaps}");
                    using(var popup = ImRaii.ContextPopupItem($"##{playerMaps.Key.GetHashCode()}--ExtraMapsContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(popup) {
                            if(ImGui.MenuItem($"Archive Last##{playerMaps.Key.GetHashCode()}--ExtraMapsArchive")) {
                                _plugin.MapManager.ArchiveMaps([lastMap]);
                            }
                            if(ImGui.MenuItem($"Delete Last##{playerMaps.Key.GetHashCode()}--ExtraMapsDelete")) {
                                _plugin.MapManager.DeleteMaps([lastMap]);
                            }
                        }
                    }
                }
            }
        }
    }

    private void UnknownMapTable() {
        using var table = ImRaii.Table($"##UnknownMapTable", MaxMaps + 2, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoKeepColumnsVisible);
        if(table) {
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 158f);
            for(int i = 0; i < MaxMaps; i++) {
                ImGui.TableSetupColumn($"map{i + 1}", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 15f);
            }
            ImGui.TableSetupColumn($"extraMaps", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 24f);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if(_lastMapPlayer == null) {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "UNKNOWN");
                ImGuiHelper.WrappedTooltip("Used most recent map.");
            } else {
                ImGui.TextUnformatted($"UNKNOWN");
            }
            for(int i = 0; i < _unknownOwnerMaps.Count && i < MaxMaps; i++) {
                var currentMap = _unknownOwnerMaps.ElementAt(i);
                ImGui.TableNextColumn();
                MapCell(currentMap);
            }

            for(int i = _unknownOwnerMaps.Count; i < MaxMaps; i++) {
                ImGui.TableNextColumn();
            }

            if(_unknownOwnerMaps.Count > MaxMaps) {
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.DalamudRed, $" +{_unknownOwnerMaps.Count - MaxMaps}");
                using(var popup = ImRaii.ContextPopupItem($"##UnknownExtraMapsContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                    if(popup) {
                        if(ImGui.MenuItem($"Archive Last##UnknownExtraMapsArchive")) {
                            _plugin.MapManager.ArchiveMaps([_unknownOwnerMaps.Last()]);
                        }
                        if(ImGui.MenuItem($"Delete Last##UnknownExtraMapsDelete")) {
                            _plugin.MapManager.DeleteMaps([_unknownOwnerMaps.Last()]);
                        }
                    }
                }
            }
        }
    }

    private void MapCell(MPAMap map) {
        using(var font = ImRaii.PushFont(UiBuilder.IconFont)) {
            var color = ImGuiColors.ParsedGreen;
            color = map.IsAmbiguousOwner ? ImGuiColors.ParsedOrange : color;
            color = map.IsManual ? ImGuiColors.DalamudWhite : color;
            color = map.IsReassigned ? ImGuiColors.ParsedBlue : color;
            color = map.Owner == null ? ImGuiColors.DalamudRed : color;
            ImGui.TextColored(color, FontAwesomeIcon.Check.ToIconString());
        }
        if(!MapDragDropSource(map)) {
            MapTooltip(map);
        }

        using(var popup = ImRaii.ContextPopupItem($"##{map.GetHashCode()}--MapContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
            if(popup) {
                if(ImGui.MenuItem($"Archive##{map.GetHashCode()}--ArchiveMap")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        _plugin.MapManager.ArchiveMaps([map]);
                    });
                }
                if(ImGui.MenuItem($"Delete##{map.GetHashCode()}--DeleteMap")) {
                    _plugin.MapManager.DeleteMaps([map]);
                }
            }
        }
    }

    private void MapTooltip(MPAMap map) {
        string tooltip = map.Time.ToString() + "\n";
        if(!map.Name.IsNullOrEmpty()) {
            tooltip += map.Name + "\n";
        }
        if(map.IsAmbiguousOwner) {
            tooltip += "Owner may be incorrect\n";
        } else if(map.IsReassigned) {
            tooltip += "Manually re-assigned\n";
        }
        if(!map.Zone.IsNullOrEmpty()) {
            tooltip += map.Zone + "\n";
        }
        if(!map.DutyName.IsNullOrEmpty()) {
            tooltip += map.DutyName + "\n";
        }
        tooltip = tooltip.TrimEnd('\n');
        ImGuiHelper.WrappedTooltip(tooltip);
    }

    private unsafe bool MapDragDropSource(MPAMap map) {
        if(ImGui.GetDragDropPayload().NativePtr != null) {
            byte[] data = new byte[12];
            Marshal.Copy(ImGui.GetDragDropPayload().Data, data, 0, 12);
            if(data.SequenceEqual(map.Id.ToByteArray())) {
                //_plugin.Log.Debug($"{currentMap.Id} is being dragged!");
                //using var style = ImRaii.PushColor(ImGuiCol.TableRowBg, ImGuiColors.DalamudViolet);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiColors.DalamudYellow - new Vector4(0, 0, 0, 0.5f)));
            }
        }

        using var drag = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceAllowNullID);
        if(drag) {
            byte[] data = map.Id.ToByteArray();
            fixed(byte* dataPtr = data) {
                ImGui.SetDragDropPayload("map", (IntPtr)dataPtr, 0x12, ImGuiCond.Once);
            }
            ImGui.Text("Drag to name to re-assign...");
        }
        return drag.Success;
    }

    private unsafe bool MapDragDropTarget(MPAMember player) {
        using var drag = ImRaii.DragDropTarget();
        if(drag) {
            ImGuiPayloadPtr acceptPayload = ImGui.AcceptDragDropPayload("map");
            if(acceptPayload.NativePtr != null) {
                byte[] data = new byte[12];
                Marshal.Copy(acceptPayload.Data, data, 0, 12);

                _plugin.Log.Debug($"Assigning map to {player.Key}");
                _plugin.DataQueue.QueueDataOperation(() => {
                    var map = _plugin.StorageManager.Maps.Query().ToList().Where(x => x.Id.ToByteArray().SequenceEqual(data)).FirstOrDefault();
                    if(map != null) {
                        _plugin.MapManager.ReassignMap(map, player);
                    }
                });
            }
        }
        return drag.Success;
    }
}
