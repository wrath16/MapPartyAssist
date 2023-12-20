using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Settings;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class ConfigWindow : Window {

        private Plugin _plugin;

        internal ConfigWindow(Plugin plugin) : base("Map Party Assist Settings") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(400, 800)
            };
            _plugin = plugin;
        }

        public override void Draw() {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Map Window");
            ImGui.Separator();

            bool requireDoubleTap = _plugin.Configuration.RequireDoubleClickOnClearAll;
            if(ImGui.Checkbox("Require double click on 'Clear All'", ref requireDoubleTap)) {
                _plugin.Configuration.RequireDoubleClickOnClearAll = requireDoubleTap;
                _plugin.Save();
            }

            bool hideZoneTable = _plugin.Configuration.HideZoneTableWhenEmpty;
            if(ImGui.Checkbox("Hide 'Map Links by Zone' when empty", ref hideZoneTable)) {
                _plugin.Configuration.HideZoneTableWhenEmpty = hideZoneTable;
                _plugin.Save();
            }

            bool noOverwriteMapLink = _plugin.Configuration.NoOverwriteMapLink;
            if(ImGui.Checkbox("Don't overwrite map links", ref noOverwriteMapLink)) {
                _plugin.Configuration.NoOverwriteMapLink = noOverwriteMapLink;
                _plugin.Save();
            }
            ImGuiComponents.HelpMarker("Will only clear map link on new treasure map added to player \nor manual removal.");

            bool highlightCurrentZoneLinks = _plugin.Configuration.HighlightLinksInCurrentZone;
            if(ImGui.Checkbox("Highlight map links in current zone (yellow)", ref highlightCurrentZoneLinks)) {
                _plugin.Configuration.HighlightLinksInCurrentZone = highlightCurrentZoneLinks;
                _plugin.Save();
            }

            bool highlightClosestLink = _plugin.Configuration.HighlightClosestLink;
            if(ImGui.Checkbox("Highlight closest map link (orange)", ref highlightClosestLink)) {
                _plugin.Configuration.HighlightClosestLink = highlightClosestLink;
                _plugin.Save();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Stats Window");
            ImGui.Separator();

            bool showTooltips = _plugin.Configuration.ShowStatsWindowTooltips;
            if(ImGui.Checkbox("Show explanatory tooltips", ref showTooltips)) {
                _plugin.Configuration.ShowStatsWindowTooltips = showTooltips;
                _plugin.Save();
            }

            //bool showAdvancedFilters = _plugin.Configuration.ShowAdvancedFilters;
            //if(ImGui.Checkbox("Show more filters", ref showAdvancedFilters)) {
            //    _plugin.Configuration.ShowAdvancedFilters = showAdvancedFilters;
            //    _plugin.Save();
            //}
            ImGuiComponents.HelpMarker("Filter stats by party members and map owner.");

            bool separateStatsByPlayer = _plugin.Configuration.CurrentCharacterStatsOnly;
            if(ImGui.Checkbox("Only include stats for current character", ref separateStatsByPlayer)) {
                _plugin.Configuration.CurrentCharacterStatsOnly = separateStatsByPlayer;
                _plugin.Save();
            }

            //bool showKicks = Plugin.Configuration.KicksProgressTable;
            //if(ImGui.Checkbox("Calculate progress table by failed checkpoint", ref showKicks)) {
            //    Plugin.Configuration.KicksProgressTable = showKicks;
            //    Plugin.Save();
            //}
            //ImGuiComponents.HelpMarker("If unchecked, will include all ");

            int progressCountToInt = (int)_plugin.Configuration.ProgressTableCount;
            string[] progressCountOptions = { "By all occurences", "By last checkpoint only" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Tally checkpoint totals##CountCombo", ref progressCountToInt, progressCountOptions, 2)) {
                _plugin.Configuration.ProgressTableCount = (ProgressTableCount)progressCountToInt;
                _plugin.Save();
            }

            int progressRateToInt = (int)_plugin.Configuration.ProgressTableRate;
            string[] progressRateOptions = { "By total runs", "By previous stage" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Divide progress rates##RateCombo", ref progressRateToInt, progressRateOptions, 2)) {
                _plugin.Configuration.ProgressTableRate = (ProgressTableRate)progressRateToInt;
                _plugin.Save();
            }

            int clearSequenceToInt = (int)_plugin.Configuration.ClearSequenceCount;
            string[] clearSequenceOptions = { "By total runs", "Since last clear" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Tally clear sequence##ClearSequenceCombo", ref clearSequenceToInt, clearSequenceOptions, 2)) {
                _plugin.Configuration.ClearSequenceCount = (ClearSequenceCount)clearSequenceToInt;
                _plugin.Save();
            }

            ImGui.Text("All duties:");

            bool allDeaths = true;
            bool allSequences = true;
            bool allZeroOmit = true;
            foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                allDeaths = allDeaths && dutyConfig.Value.DisplayDeaths;
                allSequences = allSequences && dutyConfig.Value.DisplayClearSequence;
                allZeroOmit = allZeroOmit && dutyConfig.Value.OmitZeroCheckpoints;
            }

            if(ImGui.BeginTable($"##allDutiesConfigTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX)) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Display clear sequence", ref allSequences)) {
                    foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.DisplayClearSequence = allSequences;
                    }
                    _plugin.Save();
                }
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Display wipes", ref allDeaths)) {
                    foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.DisplayDeaths = allDeaths;
                    }
                    _plugin.Save();
                }
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Omit no checkpoints", ref allZeroOmit)) {
                    foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.OmitZeroCheckpoints = allZeroOmit;
                    }
                    _plugin.Save();
                }
                ImGuiComponents.HelpMarker("Runs where no checkpoints were reached will be omitted from stats.");
            }
            ImGui.EndTable();

            foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                if(ImGui.CollapsingHeader($"{_plugin.DutyManager.Duties[dutyConfig.Key].GetDisplayName()}##Header")) {
                    if(ImGui.BeginTable($"##{dutyConfig.Key}--ConfigTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.NoHostExtendX)) {
                        //ImGui.TableSetupColumn("config1", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200f);
                        //ImGui.TableSetupColumn($"config2", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200f);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        bool displayClearSequence = dutyConfig.Value.DisplayClearSequence;
                        if(ImGui.Checkbox($"Display clear sequence##{dutyConfig.Key}--ClearSequence", ref displayClearSequence)) {
                            dutyConfig.Value.DisplayClearSequence = displayClearSequence;
                            _plugin.Save();
                        }
                        ImGui.TableNextColumn();
                        bool showDeaths = dutyConfig.Value.DisplayDeaths;
                        if(ImGui.Checkbox($"Display wipes##{dutyConfig.Key}--Wipes", ref showDeaths)) {
                            dutyConfig.Value.DisplayDeaths = showDeaths;
                            _plugin.Save();
                        }
                        ImGui.TableNextColumn();
                        bool omitZeroCheckpoints = dutyConfig.Value.OmitZeroCheckpoints;
                        if(ImGui.Checkbox($"Omit no checkpoints##{dutyConfig.Key}--NoCheckpoints", ref omitZeroCheckpoints)) {
                            dutyConfig.Value.OmitZeroCheckpoints = omitZeroCheckpoints;
                            _plugin.Save();
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }
    }
}
