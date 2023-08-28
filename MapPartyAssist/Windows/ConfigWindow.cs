using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
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

            bool hideZoneTable = Plugin.Configuration.HideZoneTableWhenEmpty;
            if(ImGui.Checkbox("Hide 'Map Links by Zone' when empty", ref hideZoneTable)) {
                Plugin.Configuration.HideZoneTableWhenEmpty = hideZoneTable;
                Plugin.Save();
            }

            bool requireDoubleTap = Plugin.Configuration.RequireDoubleClickOnClearAll;
            if(ImGui.Checkbox("Require double click on 'Clear All'", ref requireDoubleTap)) {
                Plugin.Configuration.RequireDoubleClickOnClearAll = requireDoubleTap;
                Plugin.Save();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Stats Window");

            bool separateStatsByPlayer = Plugin.Configuration.CurrentCharacterStatsOnly;
            if(ImGui.Checkbox("Only include stats for current character", ref separateStatsByPlayer)) {
                Plugin.Configuration.CurrentCharacterStatsOnly = separateStatsByPlayer;
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
