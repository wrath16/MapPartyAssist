using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Helper;
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
            PositionCondition = ImGuiCond.Appearing;
            _plugin = plugin;
        }

        public override void Draw() {

            if(ImGui.BeginTabBar("SettingsTabBar")) {
                if(ImGui.BeginTabItem("General Stats")) {
                    DrawStatsSettings();
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("Map Tracker")) {
                    DrawMapSettings();
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("Duty Progress")) {
                    DrawDutySettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawMapSettings() {
            bool requireDoubleTap = _plugin.Configuration.RequireDoubleClickOnClearAll;
            if(ImGui.Checkbox("Require double click on 'Clear All'", ref requireDoubleTap)) {
                _plugin.Configuration.RequireDoubleClickOnClearAll = requireDoubleTap;
                _plugin.Configuration.Save();
            }

            bool hideZoneTable = _plugin.Configuration.HideZoneTable;
            if(ImGui.Checkbox("Hide 'Map Links by Zone'", ref hideZoneTable)) {
                _plugin.Configuration.HideZoneTable = hideZoneTable;
                _plugin.Configuration.Save();
            }

            bool hideZoneTableEmpty = _plugin.Configuration.HideZoneTableWhenEmpty;
            if(ImGui.Checkbox("Hide 'Map Links by Zone' only when empty", ref hideZoneTableEmpty)) {
                _plugin.Configuration.HideZoneTableWhenEmpty = hideZoneTableEmpty;
                _plugin.Configuration.Save();
            }

            bool undockZoneWindow = _plugin.Configuration.UndockZoneWindow;
            if(ImGui.Checkbox("Undock 'Map Links by Zone' window", ref undockZoneWindow)) {
                _plugin.Configuration.UndockZoneWindow = undockZoneWindow;
                _plugin.Configuration.Save();
            }

            bool noOverwriteMapLink = _plugin.Configuration.NoOverwriteMapLink;
            if(ImGui.Checkbox("Don't overwrite map links", ref noOverwriteMapLink)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    _plugin.Configuration.NoOverwriteMapLink = noOverwriteMapLink;
                    _plugin.Configuration.Save();
                });
            }
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Will only clear map link on new treasure map added to player \nor manual removal.");

            bool highlightCurrentZoneLinks = _plugin.Configuration.HighlightLinksInCurrentZone;
            if(ImGui.Checkbox("Highlight map links in current zone (yellow)", ref highlightCurrentZoneLinks)) {
                _plugin.Configuration.HighlightLinksInCurrentZone = highlightCurrentZoneLinks;
                _plugin.Configuration.Save();
            }

            bool highlightClosestLink = _plugin.Configuration.HighlightClosestLink;
            if(ImGui.Checkbox("Highlight closest map link (orange)", ref highlightClosestLink)) {
                _plugin.Configuration.HighlightClosestLink = highlightClosestLink;
                _plugin.Configuration.Save();
            }
        }

        private void DrawDutySettings() {
            bool showTooltips = _plugin.Configuration.ShowStatsWindowTooltips;
            if(ImGui.Checkbox("Show explanatory tooltips", ref showTooltips)) {
                _plugin.Configuration.ShowStatsWindowTooltips = showTooltips;
                _plugin.Configuration.Save();
            }

            int progressCountToInt = (int)_plugin.Configuration.ProgressTableCount;
            string[] progressCountOptions = { "By all occurences", "By last checkpoint only" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Tally checkpoint totals##CountCombo", ref progressCountToInt, progressCountOptions, 2)) {
                _plugin.Configuration.ProgressTableCount = (ProgressTableCount)progressCountToInt;
                _plugin.Configuration.Save();
            }

            int progressRateToInt = (int)_plugin.Configuration.ProgressTableRate;
            string[] progressRateOptions = { "By total runs", "By previous stage" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Divide progress rates##RateCombo", ref progressRateToInt, progressRateOptions, 2)) {
                _plugin.Configuration.ProgressTableRate = (ProgressTableRate)progressRateToInt;
                _plugin.Configuration.Save();
            }

            int clearSequenceToInt = (int)_plugin.Configuration.ClearSequenceCount;
            string[] clearSequenceOptions = { "By total runs", "Since last clear" };
            ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
            if(ImGui.Combo($"Tally clear sequence##ClearSequenceCombo", ref clearSequenceToInt, clearSequenceOptions, 2)) {
                _plugin.Configuration.ClearSequenceCount = (ClearSequenceCount)clearSequenceToInt;
                _plugin.Configuration.Save();
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
                    _plugin.Configuration.Save();
                }
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Display wipes", ref allDeaths)) {
                    foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.DisplayDeaths = allDeaths;
                    }
                    _plugin.Configuration.Save();
                }
                ImGui.TableNextColumn();
                if(ImGui.Checkbox("Omit no checkpoints", ref allZeroOmit)) {
                    foreach(var dutyConfig in _plugin.Configuration.DutyConfigurations) {
                        dutyConfig.Value.OmitZeroCheckpoints = allZeroOmit;
                    }
                    _plugin.Configuration.Save();
                }
                ImGui.SameLine();
                ImGuiHelper.HelpMarker("Runs where no checkpoints were reached will be omitted from stats.");
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
                            _plugin.Configuration.Save();
                        }
                        ImGui.TableNextColumn();
                        bool showDeaths = dutyConfig.Value.DisplayDeaths;
                        if(ImGui.Checkbox($"Display wipes##{dutyConfig.Key}--Wipes", ref showDeaths)) {
                            dutyConfig.Value.DisplayDeaths = showDeaths;
                            _plugin.Configuration.Save();
                        }
                        ImGui.TableNextColumn();
                        bool omitZeroCheckpoints = dutyConfig.Value.OmitZeroCheckpoints;
                        if(ImGui.Checkbox($"Omit no checkpoints##{dutyConfig.Key}--NoCheckpoints", ref omitZeroCheckpoints)) {
                            dutyConfig.Value.OmitZeroCheckpoints = omitZeroCheckpoints;
                            _plugin.Configuration.Save();
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }

        private void DrawStatsSettings() {
            bool separateStatsByPlayer = _plugin.Configuration.CurrentCharacterStatsOnly;
            if(ImGui.Checkbox("Only include stats for current character", ref separateStatsByPlayer)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    _plugin.Configuration.CurrentCharacterStatsOnly = separateStatsByPlayer;
                    _plugin.Configuration.Save();
                    _plugin.Refresh();
                });
            }
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Only counts maps/duties in stats where the currently logged-in character was present, name-changes not-withstanding.");

            bool enablePriceCheck = _plugin.Configuration.EnablePriceCheck;
            if(ImGui.Checkbox("Enable market board pricing", ref enablePriceCheck)) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    _plugin.Configuration.EnablePriceCheck = enablePriceCheck;
                    if(enablePriceCheck) {
                        _plugin.PriceHistory.EnablePolling();
                    } else {
                        _plugin.PriceHistory.DisablePolling();
                    }
                    _plugin.Configuration.Save();
                    _plugin.Refresh();
                });
            }
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Uses Universalis API.");
        }
    }
}
