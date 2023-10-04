using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class DutyResultsWindow : Window {

        private Plugin _plugin;
        private List<DutyResults> _dutyResults = new();
        private int _currentPage = 0;
        private bool _collapseAll = false;

        internal DutyResultsWindow(Plugin plugin) : base("Edit Duty Results") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 800)
            };
            _plugin = plugin;
        }

        internal void Refresh(int? pageIndex = null) {
            //null index = stay on same page
            pageIndex ??= _currentPage;
            _collapseAll = true;
            _dutyResults = _plugin.StorageManager.GetDutyResults().Query().OrderByDescending(dr => dr.Time).Offset((int)pageIndex * 100).Limit(100).ToList();
            _currentPage = (int)pageIndex;
        }

        public override void OnClose() {
            base.OnClose();
            Refresh(0);
        }

        public override void Draw() {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudRed, $"EDIT AT YOUR OWN RISK");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(ImGuiColors.DalamudRed, $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()}");
            ImGui.PopFont();

            ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);
            foreach(var results in _dutyResults) {
                if(_collapseAll) {
                    ImGui.SetNextItemOpen(false);
                }
                DrawDutyResults(results);
            }
            _collapseAll = false;

            ImGui.EndChild();

            if(ImGui.Button("Save")) {
                //should this go in manager?
                _plugin.StorageManager.UpdateDutyResults(_dutyResults.Where(dr => dr.IsEdited)).ContinueWith(t => {
                    _plugin.DutyManager.RefreshCurrentDutyResults();
                });
            }

            ImGui.SameLine();
            if(ImGui.Button("Collapse All")) {
                _collapseAll = true;
            }

            if(_currentPage > 0) {
                ImGui.SameLine();
                if(ImGui.Button("Previous 100")) {
                    Refresh(_currentPage - 1);
                }
            }

            if(_dutyResults.Count >= 100) {
                ImGui.SameLine();
                if(ImGui.Button("Next 100")) {
                    Refresh(_currentPage + 1);
                }
            }
        }

        private void DrawDutyResults(DutyResults dutyResults) {
            List<string> lastCheckpoints = new() {
                "None"
            };
            string? owner = dutyResults.Owner ?? "";
            string? gil = dutyResults.TotalGil.ToString();
            bool isCompleted = dutyResults.IsComplete;
            foreach(var checkpoint in _plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!) {
                lastCheckpoints.Add(checkpoint.Name);
            }

            if(ImGui.CollapsingHeader(String.Format("{0:-23}     {1:-40}", dutyResults.Time.ToString(), dutyResults.DutyName))) {
                if(ImGui.Checkbox($"Completed##{dutyResults.Id}--Completed", ref isCompleted)) {
                    dutyResults.IsEdited = true;
                    dutyResults.IsComplete = isCompleted;
                }
                if(ImGui.InputText($"Owner##{dutyResults.Id}--Owner", ref owner, 50, ImGuiInputTextFlags.AutoSelectAll)) {
                    dutyResults.IsEdited = true;
                    dutyResults.Owner = owner;
                }
                if(ImGui.InputText($"Total Gil##{dutyResults.Id}--TotalGil", ref gil, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                    int gilInt;
                    if(int.TryParse(gil, out gilInt)) {
                        dutyResults.IsEdited = true;
                        dutyResults.TotalGil = gilInt;
                    }
                }
                var currentLastCheckpointIndex = dutyResults.CheckpointResults.Count;
                if(ImGui.Combo($"Last Checkpoint##{dutyResults.Id}--LastCheckpoint", ref currentLastCheckpointIndex, lastCheckpoints.ToArray(), lastCheckpoints.Count)) {
                    if(currentLastCheckpointIndex > dutyResults.CheckpointResults.Count) {
                        dutyResults.IsEdited = true;
                        for(int i = dutyResults.CheckpointResults.Count; i <= currentLastCheckpointIndex - 1; i++) {
                            dutyResults.CheckpointResults.Add(new CheckpointResults(_plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints![i], true));
                        }
                    } else if(currentLastCheckpointIndex < dutyResults.CheckpointResults.Count) {
                        dutyResults.IsEdited = true;
                        for(int i = dutyResults.CheckpointResults.Count - 1; i >= currentLastCheckpointIndex; i--) {
                            dutyResults.CheckpointResults.RemoveAt(i);
                        }
                    }
                }
                if(_plugin.DutyManager.Duties[dutyResults.DutyId].Structure == DutyStructure.Roulette) {
                    string[] summons = { "Lesser", "Greater", "Elder", "Abomination", "Circle Shift" };
                    var summonCheckpoints = dutyResults.CheckpointResults.Where(cr => cr.Checkpoint.Name.StartsWith("Complete")).ToList();
                    for(int i = 0; i < summonCheckpoints.Count(); i++) {
                        int summonIndex = (int?)summonCheckpoints[i].SummonType ?? 3;
                        if(ImGui.Combo($"{StatsWindow.AddOrdinal(i + 1)} Summon##{summonCheckpoints[i].GetHashCode()}-Summon", ref summonIndex, summons, summons.Length)) {
                            dutyResults.IsEdited = true;
                            summonCheckpoints[i].SummonType = (Summon)summonIndex;
                        }
                    }
                }
            }
        }
    }
}
