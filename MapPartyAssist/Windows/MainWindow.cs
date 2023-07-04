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

namespace MapPartyAssist.Windows;

public class MainWindow : Window, IDisposable {
    private Plugin Plugin;

    private static int _maxMaps = 11;

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
    }

    public void Dispose() {
    }

    public override void OnClose() {
        Plugin.WindowSystem.GetWindow("Map Links by Zone").IsOpen = false;
        base.OnClose();
    }

    public override void PreDraw() {
        base.PreDraw();
        Plugin.WindowSystem.GetWindow("Map Links by Zone").IsOpen = false;
        //CurrentPosition = ImGui.GetWindowPos();
        //CurrentSize = ImGui.GetWindowSize();
    }

    public override void Draw() {
        CurrentPosition = ImGui.GetWindowPos();
        CurrentSize = ImGui.GetWindowSize();
        //if(ImGui.Button("Test Function")) {
        //    this.Plugin.TestFunction3();
        //}
        //if(ImGui.Button("Test Function2")) {
        //    this.Plugin.TestFunction2();
        //}


        if(ImGui.Button("Clear All")) {
            Plugin.MapManager.ClearAllMaps();
        }

        int totalMapsCurrent = 0;
        int totalMapsRecent = 0;
        int totalPortalsCurrent = 0;
        int totalPortalsRecent = 0;
        foreach(var p in Plugin.CurrentPartyList) {
            totalMapsCurrent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList().Count;
            totalPortalsCurrent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived && m.IsPortal).ToList().Count;
        }
        foreach(var p in Plugin.RecentPartyList) {
            totalMapsRecent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList().Count;
            totalPortalsRecent += p.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived && m.IsPortal).ToList().Count;
        }

        ImGui.SameLine();
        ImGui.Text($"Total Maps: {totalMapsCurrent}");
        if(ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text($"Including recent party members: {totalMapsCurrent + totalMapsRecent}");
            ImGui.EndTooltip();
        }
        ImGui.SameLine();
        ImGui.Text($"Total Portals: {totalPortalsCurrent}");
        if(ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text($"Including recent party members: {totalPortalsCurrent + totalPortalsRecent}");
            ImGui.EndTooltip();
        }
        //ImGui.Text($"{ImGuiHelpers.GlobalScale.ToString()}");

        if(Plugin.CurrentPartyList.Count <= 0) {
            ImGui.Text("No party members currently.");
        } else {
            MapTable(Plugin.CurrentPartyList);
        }


        //Plugin.WindowSystem.GetWindow("Map Links by Zone").IsOpen = Plugin.CurrentPartyList.Count > 0;
        Plugin.WindowSystem.GetWindow("Map Links by Zone").IsOpen = true;

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

        if(Plugin.RecentPartyList.Count > 0) {
            ImGui.Text("Recent party members:");
            MapTable(Plugin.RecentPartyList);
        }

        //ImGui.BeginChildFrame(111, new Vector2(200, 50), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        //ImGui.Text("Test2");
        ////ImGui.EndChild();
        //ImGui.EndChildFrame();

        //Plugin.WindowSystem.GetWindow("Map Links").IsOpen = true;
        //MapTable(Plugin.FakePartyList);
    }

    private void MapTable(Dictionary<string, MPAMember> list, bool readOnly = false) {

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
            foreach(var player in list.OrderBy(kvp => {
                if(kvp.Value.IsSelf) {
                    return "";
                } else {
                    return kvp.Key;
                }
            })) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if(player.Key.Equals(Plugin.MapManager.LastMapPlayerKey)) {
                    ImGui.TextColored(ImGuiColors.DalamudYellow, $"{player.Value.Name.PadRight(20)}");
                } else {
                    ImGui.Text($"{player.Value.Name.PadRight(20)}");
                }
                //ImGui.Text($"{player.Value.Name.PadRight(20)}");
                if(ImGui.BeginPopupContextItem($"##{player.Value.GetHashCode()}--NameContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                    if(ImGui.MenuItem($"Add map manually##{player.Value.GetHashCode()}--NameAddMap")) {
                        Plugin.MapManager.AddMap(player.Value, "", "Manually-added map.", true);
                    }
                    ImGui.EndPopup();
                }
                //ImGui.Text($"{player.Value.HomeWorld}");
                if(player.Value.MapLink != null) {
                    //ImGui.SameLine(ImGuiHelpers.GlobalScale * 151);
                    //need to fix this for larger scales...
                    ImGui.SameLine(ImGui.GetColumnWidth() - (158 - 151) * ImGuiHelpers.GlobalScale * ImGuiHelpers.GlobalScale);

                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudGrey, FontAwesomeIcon.Search.ToIconString());
                    ImGui.PopFont();
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        ImGui.Text($"{player.Value.MapLink.PlaceName} {player.Value.MapLink.CoordinateString}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.IsItemClicked()) {

                        Plugin.OpenMapLink(player.Value.MapLink);
                    }
                }
                ImGui.TableNextColumn();
                List<MPAMap> maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList();
                for(int i = 0; i < maps.Count() && i < _maxMaps; i++) {
                    ImGui.PushFont(UiBuilder.IconFont);
                    if(maps.ElementAt(i).IsManual) {
                        ImGui.TextColored(ImGuiColors.DalamudYellow, FontAwesomeIcon.Check.ToIconString());
                    } else {
                        ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
                    }
                    //ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
                    ImGui.PopFont();
                    if(ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();
                        if(!maps.ElementAt(i).DutyName.IsNullOrEmpty()) {
                            ImGui.Text($"{maps.ElementAt(i).DutyName}");
                        }
                        if(!maps.ElementAt(i).Name.IsNullOrEmpty()) {
                            ImGui.Text($"{maps.ElementAt(i).Name}");
                        }
                        if(!maps.ElementAt(i).Zone.IsNullOrEmpty()) {
                            ImGui.Text($"{maps.ElementAt(i).Zone}");
                        }
                        ImGui.Text($"{maps.ElementAt(i).Time}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.BeginPopupContextItem($"##{maps.ElementAt(i).GetHashCode()}--ContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Remove##{maps.ElementAt(i).GetHashCode()}")) {
                            toDelete.Add(maps[i]);
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();
                }
                for(int i = maps.Count(); i < _maxMaps; i++) {
                    ImGui.TableNextColumn();
                }

                if(maps.Count() > _maxMaps) {
                    ImGui.TextColored(ImGuiColors.ParsedGreen, $" +{maps.Count() - _maxMaps}");
                    if(ImGui.BeginPopupContextItem($"##{player.Value.GetHashCode()}--ExtraMapsContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Remove Last##{player.Value.GetHashCode()}--ExtraMapsRemove")) {
                            Plugin.MapManager.RemoveLastMap(player.Value);
                        }
                        ImGui.EndPopup();
                    }

                }
            }

            //todo move these to plugin layer
            foreach(var map in toArchive) {
                map.IsArchived = true;
                Plugin.Configuration.Save();
            }

            foreach(var map in toDelete) {
                map.IsDeleted = true;
                Plugin.Configuration.Save();
            }

            ImGui.EndTable();
        }
    }

    private void ZoneCountTable(Dictionary<string, MPAMember> list) {
        Dictionary<string, int> zones = new();
        foreach(MPAMember player in list.Values.Where(p => p.MapLink != null)) {
            if(zones.ContainsKey(player.MapLink!.PlaceName)) {
                zones[player.MapLink.PlaceName] += 1;
            } else {
                zones.Add(player.MapLink.PlaceName, 1);
            }
        }

        if(ImGui.BeginTable($"##{list.GetHashCode()}_Zone_Table", 2, ImGuiTableFlags.NoHostExtendX)) {
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed, 158f);
            ImGui.TableSetupColumn("count", ImGuiTableColumnFlags.WidthFixed, 15);
            foreach(var zone in zones) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{zone.Key}");
                ImGui.TableNextColumn();
                ImGui.Text($"{zone.Value}");
            }
            ImGui.EndTable();
        }
    }

    private void ContextMenuPopup(MPAMap map) {
        if(ImGui.BeginPopupContextItem()) {
            ImGui.Text("test");
            ImGui.EndPopup();
        }
    }
}
