using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Settings;
using MapPartyAssist.Types;
using System;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class ConfigWindow : Window, IDisposable {

        //private MainWindow MainWindow { get; set; }
        private Plugin Plugin;

        public ConfigWindow(Plugin plugin) : base("Map Party Assist Settings") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(400, 800)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {
            //bool enableSolo = Plugin.Configuration.EnableWhileSolo;
            //if(ImGui.Checkbox("Enable While Solo", ref enableSolo)) {
            //    Plugin.ToggleEnableSolo(enableSolo);
            //}

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Map Window");
            ImGui.Separator();

            bool requireDoubleTap = Plugin.Configuration.RequireDoubleClickOnClearAll;
            if(ImGui.Checkbox("Require double click on 'Clear All'", ref requireDoubleTap)) {
                Plugin.Configuration.RequireDoubleClickOnClearAll = requireDoubleTap;
                Plugin.Save();
            }

            bool hideZoneTable = Plugin.Configuration.HideZoneTableWhenEmpty;
            if(ImGui.Checkbox("Hide 'Map Links by Zone' when empty", ref hideZoneTable)) {
                Plugin.Configuration.HideZoneTableWhenEmpty = hideZoneTable;
                Plugin.Save();
            }

            bool noOverwriteMapLink = Plugin.Configuration.NoOverwriteMapLink;
            if(ImGui.Checkbox("Don't overwrite map links", ref noOverwriteMapLink)) {
                Plugin.Configuration.NoOverwriteMapLink = noOverwriteMapLink;
                Plugin.Save();
            }
            ImGuiComponents.HelpMarker("Will only clear map link on new treasure map added to player \nor manual removal.");

            bool highlightCurrentZoneLinks = Plugin.Configuration.HighlightLinksInCurrentZone;
            if(ImGui.Checkbox("Highlight map links in current zone (yellow)", ref highlightCurrentZoneLinks)) {
                Plugin.Configuration.HighlightLinksInCurrentZone = highlightCurrentZoneLinks;
                Plugin.Save();
            }

            bool highlightClosestLink = Plugin.Configuration.HighlightClosestLink;
            if(ImGui.Checkbox("Highlight closest map link (orange)", ref highlightClosestLink)) {
                Plugin.Configuration.HighlightClosestLink = highlightClosestLink;
                Plugin.Save();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Stats Window");
            ImGui.Separator();

            bool showTooltips = Plugin.Configuration.ShowStatsWindowTooltips;
            if(ImGui.Checkbox("Show explanatory tooltips", ref showTooltips)) {
                Plugin.Configuration.ShowStatsWindowTooltips = showTooltips;
                Plugin.Save();
            }

            bool separateStatsByPlayer = Plugin.Configuration.CurrentCharacterStatsOnly;
            if(ImGui.Checkbox("Only include stats for current character", ref separateStatsByPlayer)) {
                Plugin.Configuration.CurrentCharacterStatsOnly = separateStatsByPlayer;
                Plugin.Save();
            }

            //bool showKicks = Plugin.Configuration.KicksProgressTable;
            //if(ImGui.Checkbox("Calculate progress table by failed checkpoint", ref showKicks)) {
            //    Plugin.Configuration.KicksProgressTable = showKicks;
            //    Plugin.Save();
            //}
            //ImGuiComponents.HelpMarker("If unchecked, will include all ");

            int progressCountToInt = (int)Plugin.Configuration.ProgressTableCount;
            string[] includes2 = { "By all occurences", "By last checkpoint only" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Tally checkpoint totals##CountCombo", ref progressCountToInt, includes2, 2)) {
                Plugin.Configuration.ProgressTableCount = (ProgressTableCount)progressCountToInt;
                Plugin.Save();
            }


            int progressRateToInt = (int)Plugin.Configuration.ProgressTableRate;
            string[] includes = { "By total runs", "By previous stage" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Divide progress rates##RateCombo", ref progressRateToInt, includes, 2)) {
                Plugin.Configuration.ProgressTableRate = (ProgressTableRate)progressRateToInt;
                Plugin.Save();
            }

            ImGui.Text("All duties:");

            bool allDeaths = true;
            bool allSequences = true;
            foreach(var dutyConfig in Plugin.Configuration.DutyConfigurations) {
                allDeaths = allDeaths && dutyConfig.Value.DisplayDeaths;
                allSequences = allSequences && dutyConfig.Value.DisplayClearSequence;
            }

            if(ImGui.BeginTable($"##allDutiesConfigTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX)) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Display clear sequence", ref allSequences)) {
                    foreach(var dutyConfig in Plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.DisplayClearSequence = allSequences;
                    }
                    Plugin.Save();
                }
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Display wipes", ref allDeaths)) {
                    foreach(var dutyConfig in Plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.DisplayDeaths = allDeaths;
                    }
                    Plugin.Save();
                }
            }
            ImGui.EndTable();

            foreach(var dutyConfig in Plugin.Configuration.DutyConfigurations) {
                if(ImGui.CollapsingHeader($"{Plugin.DutyManager.Duties[dutyConfig.Key].GetDisplayName()}##Header")) {
                    if(ImGui.BeginTable($"##{dutyConfig.Key}-ConfigTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX)) {
                        //ImGui.TableSetupColumn("config1", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200f);
                        //ImGui.TableSetupColumn($"config2", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200f);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        bool displayClearSequence = dutyConfig.Value.DisplayClearSequence;
                        if(ImGui.Checkbox($"Display clear sequence##{dutyConfig.Key}", ref displayClearSequence)) {
                            dutyConfig.Value.DisplayClearSequence = displayClearSequence;
                            Plugin.Save();
                        }
                        ImGui.TableNextColumn();
                        bool showDeaths = dutyConfig.Value.DisplayDeaths;
                        if(ImGui.Checkbox($"Display wipes##{dutyConfig.Key}", ref showDeaths)) {
                            dutyConfig.Value.DisplayDeaths = showDeaths;
                            Plugin.Save();
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }
    }
}
