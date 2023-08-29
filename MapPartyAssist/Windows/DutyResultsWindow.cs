using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class DutyResultsWindow : Window, IDisposable {

        private Plugin Plugin;
        private List<DutyResults> _dutyResults = new();
        int currentPage = 0;
        private bool _collapseAll = false;

        public DutyResultsWindow(Plugin plugin) : base("Edit Duty Results") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 800)
            };
            this.Plugin = plugin;
        }

        //null index = stay on same page
        public void Refresh(int? pageIndex = null) {
            if(pageIndex == null) {
                pageIndex = currentPage;
            } else {
                _collapseAll = true;
            }

            _dutyResults = Plugin.StorageManager.GetDutyResults().Query().OrderByDescending(dr => dr.Time).Offset((int)pageIndex * 100).Limit(100).ToList();
            currentPage = (int)pageIndex;
        }

        public void Dispose() {
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
                Plugin.StorageManager.UpdateDutyResults(_dutyResults.Where(dr => dr.IsEdited));
            }

            ImGui.SameLine();
            if(ImGui.Button("Collapse All")) {
                _collapseAll = true;
            }

            if(currentPage > 0) {
                ImGui.SameLine();
                if(ImGui.Button("Previous 100")) {
                    Refresh(currentPage - 1);
                }
            }

            //not sure if need to protect boundary

            if(_dutyResults.Count >= 100) {
                ImGui.SameLine();
                if(ImGui.Button("Next 100")) {
                    Refresh(currentPage + 1);
                }
            }
        }

        public void DrawDutyResults(DutyResults dutyResults) {
            List<string> lastCheckpoints = new() {
                "None"
            };
            var owner = dutyResults.Owner ?? "";
            var gil = dutyResults.TotalGil.ToString();
            var isCompleted = dutyResults.IsComplete;
            foreach(var checkpoint in Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!) {
                lastCheckpoints.Add(checkpoint.Name);
            }

            //ImGui.SetNextItemOpen(true, _openCond);

            if(ImGui.CollapsingHeader(String.Format("{0:-23}     {1:-40}", dutyResults.Time.ToString(), dutyResults.DutyName))) {
                //_openCond = ImGuiCond.None;
                if(ImGui.Checkbox($"Completed##{dutyResults.Id}", ref isCompleted)) {
                    dutyResults.IsEdited = true;
                    dutyResults.IsComplete = isCompleted;
                }
                if(ImGui.InputText($"Owner##{dutyResults.Id}", ref owner, 50, ImGuiInputTextFlags.AutoSelectAll)) {
                    dutyResults.IsEdited = true;
                    dutyResults.Owner = owner;
                }
                if(ImGui.InputText($"Total Gil##{dutyResults.Id}", ref gil, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                    int gilInt;
                    if(int.TryParse(gil, out gilInt)) {
                        dutyResults.IsEdited = true;
                        dutyResults.TotalGil = gilInt;
                    }
                }
                var currentLastCheckpointIndex = dutyResults.CheckpointResults.Count;
                //ImGui.Text($"Last checkpoint: {dutyResults.CheckpointResults.Last().Checkpoint.Name}");
                if(ImGui.Combo($"Last Checkpoint##{dutyResults.Id}", ref currentLastCheckpointIndex, lastCheckpoints.ToArray(), lastCheckpoints.Count)) {
                    if(currentLastCheckpointIndex > dutyResults.CheckpointResults.Count) {
                        dutyResults.IsEdited = true;
                        for(int i = dutyResults.CheckpointResults.Count; i <= currentLastCheckpointIndex - 1; i++) {
                            dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints![i], true));
                        }
                    } else if(currentLastCheckpointIndex < dutyResults.CheckpointResults.Count) {
                        dutyResults.IsEdited = true;
                        for(int i = dutyResults.CheckpointResults.Count - 1; i >= currentLastCheckpointIndex; i--) {
                            dutyResults.CheckpointResults.RemoveAt(i);
                        }
                    }

                    //dutyResults.CheckpointResults = new();
                    //for(int i = 0; i <= currentLastCheckpointIndex; i++) {
                    //    dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints[i], true));
                    //}
                }
                if(Plugin.DutyManager.Duties[dutyResults.DutyId].Structure == DutyStructure.Roulette) {
                    string[] summons = { "Lesser", "Greater", "Elder", "Abomination", "Circle Shift" };
                    var summonCheckpoints = dutyResults.CheckpointResults.Where(cr => cr.Checkpoint.Name.StartsWith("Complete")).ToList();
                    for(int i = 0; i < summonCheckpoints.Count(); i++) {
                        var summonIndex = summonCheckpoints[i].SummonType == null ? 3 : (int)summonCheckpoints[i].SummonType;
                        //ImGui.Text($"{summons[(int)summonCheckpoints[i].SummonType]}");
                        if(ImGui.Combo($"{StatsWindow.AddOrdinal(i + 1)} Summon##{summonCheckpoints[i].GetHashCode()}", ref summonIndex, summons, summons.Length)) {
                            dutyResults.IsEdited = true;
                            summonCheckpoints[i].SummonType = (Summon)summonIndex;
                        }
                    }
                }
            }
        }
    }
}
