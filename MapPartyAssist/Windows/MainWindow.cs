using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
//using static TitleRoulette.Configuration;

namespace MapPartyAssist.Windows;

public class MainWindow : Window, IDisposable {
    private Plugin Plugin;

    private int _maxMaps = 10;

    public MainWindow(Plugin plugin) : base(
        "Map Party Assist") {
        this.SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(500, 350),
            MaximumSize = new Vector2(500, 350)
        };
        this.Plugin = plugin;
    }

    public void Dispose() {
    }

    public override void Draw() {


        //ImGui.Text($"The random config bool is {this.Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");
        ImGui.CreateContext();
        if(ImGui.Button("Test Function")) {
            this.Plugin.TestFunction();
        }
        if(ImGui.BeginPopupContextItem()) {
            ImGui.Text("Test Function");
            ImGui.EndPopup();
        }
        if(ImGui.Button("Test Function2")) {
            this.Plugin.TestFunction2();
        }
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
        ImGui.PopFont();


        if(Plugin.CurrentPartyList.Count <= 0) {
            ImGui.Text("No party members currently.");
        } else {
            MapTable(Plugin.CurrentPartyList);
            ////int maxMaps = 5;
            //ImGui.Columns(_maxMaps + 2, "###MapPartyAssist_Maps_Table", false);
            //var baseWidth = ImGui.GetWindowSize().X / 4 * ImGuiHelpers.GlobalScale;
            //ImGui.SetColumnWidth(0, baseWidth + 100f);                 // name
            //for(int i = 1; i <= _maxMaps; i++) {
            //    ImGui.SetColumnWidth(i, ImGuiHelpers.GlobalScale * 30f);
            //}
            //ImGui.SetColumnWidth(_maxMaps + 1, ImGuiHelpers.GlobalScale * 40f);

            //foreach(var player in this.Plugin.CurrentPartyList) {
            //    ImGui.Text($"{player.Value.Name}");
            //    //ImGui.Text($"{player.Value.HomeWorld}");
            //    ImGui.NextColumn();
            //    int count = 0;
            //    //var map in player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived)
            //    var maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived);
            //    for(int i = 0; i < maps.Count() && i < _maxMaps; i++) {
            //        ImGui.PushFont(UiBuilder.IconFont);
            //        ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
            //        ImGui.PopFont();
            //        ImGui.NextColumn();
            //    }

            //    for(int i = maps.Count(); i < _maxMaps; i++) {
            //        //ImGui.PushFont(UiBuilder.IconFont);
            //        //ImGui.TextColored(ImGuiColors.DalamudRed, FontAwesomeIcon.Check.ToIconString());
            //        //ImGui.PopFont();
            //        ImGui.NextColumn();
            //    }

            //    if(maps.Count() > _maxMaps) {
            //        ImGui.TextColored(ImGuiColors.ParsedGreen, $" +{maps.Count() - _maxMaps}");
            //    }
            //    ImGui.NextColumn();
            //}

            



            //foreach(var map in maps) {
            //    ImGui.PushFont(UiBuilder.IconFont);
            //    ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
            //    ImGui.PopFont();
            //    ImGui.NextColumn();
            //    count++;
            //}
            //for(int i = count; i <= maxMaps; i++) {
            //    ImGui.PushFont(UiBuilder.IconFont);
            //    ImGui.TextColored(ImGuiColors.DalamudRed, FontAwesomeIcon.Check.ToIconString());
            //    //ImGui.Text("xx");
            //    ImGui.PopFont();
            //    ImGui.NextColumn();
            //}
        }

        MapTable(Plugin.FakePartyList);

        //if(ImGui.BeginTable("testTAble", 4, ImGuiTableFlags.Borders)) {
        //    ImGui.TableSetupColumn("ass", ImGuiTableColumnFlags.WidthStretch, 50f);
        //    ImGui.TableSetupColumn("penis", ImGuiTableColumnFlags.WidthFixed, 20f);
        //    ImGui.TableSetupColumn("vagina", ImGuiTableColumnFlags.WidthFixed, 20f);
        //    ImGui.TableSetupColumn("boobs", ImGuiTableColumnFlags.WidthFixed, 20f);
        //    ImGui.TableHeadersRow();
        //    for(int i = 0; i < 4; i++) {
        //        ImGui.TableNextColumn();
        //        ImGui.Text("spankable");

        //    }
        //}

        //ImGui.Spacing();

        //ImGui.Text("Have a goat:");
        //ImGui.Indent(55);
        //ImGui.Image(this.GoatImage.ImGuiHandle, new Vector2(this.GoatImage.Width, this.GoatImage.Height));
        //ImGui.Unindent(55);
    }

    private void MapTable(Dictionary<string, MPAMember> list) {

        if(ImGui.BeginTable($"##{list.GetHashCode()}_Maps_Table", _maxMaps + 4, ImGuiTableFlags.Borders)) {
            List<MPAMap> toArchive = new();
            List<MPAMap> toDelete = new();
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, 6f);
            ImGui.TableSetupColumn("addNew", ImGuiTableColumnFlags.WidthFixed, 22f);
            ImGui.TableSetupColumn("mapLink", ImGuiTableColumnFlags.WidthFixed, 15f);
            for(int i = 0; i < _maxMaps; i++) {
                ImGui.TableSetupColumn($"map{i + 3}", ImGuiTableColumnFlags.WidthFixed, 15f);
            }
            foreach(var player in list) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{player.Value.Name}");
                //ImGui.Text($"{player.Value.HomeWorld}");
                ImGui.TableNextColumn();
                ImGui.PushFont(UiBuilder.IconFont);
                if(ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{player.GetHashCode()}--AddMap")) {
                    PluginLog.Log($"Adding new map to {player.Key}");
                    var newMap = new MPAMap("Manually-added map", DateTime.Now, "", true);
                    player.Value.Maps.Add(newMap);
                    this.Plugin.Configuration.Save();
                }
                ImGui.PopFont();
                if(ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.Text("Add map manually.");
                    ImGui.EndTooltip();
                }
                ImGui.TableNextColumn();
                if(player.Value.MapLink != null) {

                }
                ImGui.TableNextColumn();
                //int count = 0;
                List<MPAMap> maps = player.Value.Maps.Where(m => !m.IsDeleted && !m.IsArchived).ToList();
                for(int i = 0; i < maps.Count() && i < _maxMaps; i++) {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.ParsedGreen, FontAwesomeIcon.Check.ToIconString());
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
                        //if(ImGui.MenuItem($"Delete##{maps.ElementAt(i).GetHashCode()}")) {
                        //    maps.RemoveAt(i);
                        //    toDelete.Add(maps[i]);
                        //}
                        //ImGui.Text($"testing{i}");
                        //ImGui.Text($"{maps.ElementAt(i).Time.ToString()}");
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();
                }
                for(int i = maps.Count(); i < _maxMaps; i++) {
                    //ImGui.PushFont(UiBuilder.IconFont);
                    //ImGui.TextColored(ImGuiColors.DalamudRed, FontAwesomeIcon.Letter.ToIconString());
                    //ImGui.PopFont();
                    ImGui.TableNextColumn();
                }

                if(maps.Count() > _maxMaps) {
                    ImGui.TextColored(ImGuiColors.ParsedGreen, $" +{maps.Count() - _maxMaps}");
                }
                //ImGui.TextColored(ImGuiColors.ParsedGreen, $" +{maps.Count() - _maxMaps}");
                //ImGui.TableNextRow();
            }

            foreach(var map in toArchive) {
                map.IsArchived = true;
            }

            foreach(var map in toDelete) {
                map.IsDeleted = true;
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
