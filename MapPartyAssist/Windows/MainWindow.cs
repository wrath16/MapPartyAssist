using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
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

    private int _maxMaps = 10;

    public MainWindow(Plugin plugin) : base(
        "Map Party Assist") {
        this.ForceMainWindow = true;
        this.SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(500, 250),
            MaximumSize = new Vector2(500, 350)
        };
        this.Plugin = plugin;
    }

    public void Dispose() {
    }

    public override void Draw() {
        //if(ImGui.Button("Test Function")) {
        //    this.Plugin.TestFunction3();
        //}
        //if(ImGui.Button("Test Function2")) {
        //    this.Plugin.TestFunction2();
        //}

        if(ImGui.Button("Clear All")) {
            Plugin.ClearAllMaps();
        }


        if(Plugin.CurrentPartyList.Count <= 0) {
            ImGui.Text("No party members currently.");
        } else {
            MapTable(Plugin.CurrentPartyList);
        }

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

        MapTable(Plugin.FakePartyList);
    }

    private void MapTable(Dictionary<string, MPAMember> list, bool readOnly = false) {

        if(ImGui.BeginTable($"##{list.GetHashCode()}_Maps_Table", _maxMaps + 4, ImGuiTableFlags.Borders)) {
            List<MPAMap> toArchive = new();
            List<MPAMap> toDelete = new();
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, 6f);
            ImGui.TableSetupColumn("addNew", ImGuiTableColumnFlags.WidthFixed, 22f);
            ImGui.TableSetupColumn("mapLink", ImGuiTableColumnFlags.WidthFixed, 15f);
            for(int i = 0; i < _maxMaps; i++) {
                ImGui.TableSetupColumn($"map{i + 3}", ImGuiTableColumnFlags.WidthFixed, 15f);
            }
            foreach(var player in list.OrderBy(kvp => {
                if(kvp.Value.IsSelf) {
                    return "";
                } else {
                    return kvp.Key;
                }
            })) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{player.Value.Name}");
                //ImGui.Text($"{player.Value.HomeWorld}");
                ImGui.TableNextColumn();
                ImGui.PushFont(UiBuilder.IconFont);
                if(ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{player.GetHashCode()}--AddMap")) {
                    //PluginLog.Log($"Adding new map to {player.Key}");
                    Plugin.AddMap(player.Value, "", "Manually-added map.", true);
                    //var newMap = new MPAMap("Manually-added map", DateTime.Now, "", false, true);
                    //player.Value.Maps.Add(newMap);
                    //this.Plugin.Configuration.Save();
                }
                ImGui.PopFont();
                if(ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.Text("Add map manually.");
                    ImGui.EndTooltip();
                }
                ImGui.TableNextColumn();
                if(player.Value.MapLink != null) {
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
                //int count = 0;
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
                        if(!maps.ElementAt(i).Name.IsNullOrEmpty()) {
                            ImGui.Text($"{maps.ElementAt(i).Name}");
                        }
                        if(!maps.ElementAt(i).Zone.IsNullOrEmpty()) {
                            ImGui.Text($"{maps.ElementAt(i).Zone}");
                        }
                        ImGui.Text($"{maps.ElementAt(i).Time.ToString()}");
                        ImGui.EndTooltip();
                    }
                    if(ImGui.BeginPopupContextItem($"##{maps.ElementAt(i).GetHashCode()}--ContextMenu", ImGuiPopupFlags.MouseButtonRight)) {
                        if(ImGui.MenuItem($"Remove##{maps.ElementAt(i).GetHashCode()}")) {
                            toArchive.Add(maps[i]);
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
                            Plugin.RemoveLastMap(player.Value);
                        }
                        ImGui.EndPopup();
                    }

                }
            }

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

    private void ContextMenuPopup(MPAMap map) {
        if(ImGui.BeginPopupContextItem()) {
            ImGui.Text("test");
            ImGui.EndPopup();
        }
    }
}
