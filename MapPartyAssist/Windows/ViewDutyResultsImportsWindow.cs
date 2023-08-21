using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class ViewDutyResultsImportsWindow : Window, IDisposable {
        private Plugin Plugin;
        private StatsWindow StatsWindow;
        private AddDutyResultsImportWindow AddDutyResultsImportWindow;
        private List<DutyResultsImport> _imports = new();
        int _currentPage = 0;

        public ViewDutyResultsImportsWindow(Plugin plugin, StatsWindow statsWindow) : base("Manage Imports") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 150),
                MaximumSize = new Vector2(800, 800)
            };
            Plugin = plugin;
            PositionCondition = ImGuiCond.Appearing;
            StatsWindow = statsWindow;
            AddDutyResultsImportWindow = new AddDutyResultsImportWindow(plugin);
            AddDutyResultsImportWindow.IsOpen = false;
            Plugin.WindowSystem.AddWindow(AddDutyResultsImportWindow);
        }

        public void Refresh(int pageIndex = 0) {
            //limit to last 5
            //_dutyResults = Plugin.StorageManager.GetDutyResults().Query().OrderByDescending(dr => dr.Time).Limit(10).ToList();
            _imports = Plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted).OrderByDescending(i => i.Time).Offset(pageIndex * 100).Limit(100).ToList();
            _currentPage = pageIndex;
        }

        public void Dispose() {
        }

        public override void OnClose() {
            base.OnClose();
            AddDutyResultsImportWindow.IsOpen = false;
            Refresh();
        }

        public override void Draw() {

            if(ImGui.Button("New Import")) {
                //if(!AddDutyResultsImportWindow.IsOpen) {
                //    AddDutyResultsImportWindow.ClearModel();
                //}
                AddDutyResultsImportWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                AddDutyResultsImportWindow.IsOpen = true;
            }


            if(_imports.Count > 0) {
                ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);

                ImGui.BeginTable($"AddTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX);
                ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 130);
                ImGui.TableSetupColumn("duty", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200);
                ImGui.TableSetupColumn("edit", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 30);
                ImGui.TableSetupColumn("delete", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 50);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();


                foreach(var import in _imports) {
                    //DrawImport(import);
                    ImGui.Text($"{import.Time.ToString()}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{Plugin.DutyManager.Duties[import.DutyId].Name}");
                    ImGui.TableNextColumn();
                    if(ImGui.Button($"Edit##{import.Id.ToString()}")) {
                        AddDutyResultsImportWindow.OpenAsEdit(import);
                    }
                    ImGui.TableNextColumn();
                    if(ImGui.BeginPopup($"{import.Id.ToString()}-DeletePopup")) {
                        ImGui.Text("Are you sure?");
                        if(ImGui.Button($"Yes##{import.Id.ToString()}-ConfirmDelete")) {
                            import.IsDeleted = true;
                            Plugin.StorageManager.UpdateDutyResultsImport(import);
                        }
                        ImGui.SameLine();
                        if(ImGui.Button($"Cancel##{import.Id.ToString()}-CancelDelete")) {
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                    if(ImGui.Button($"Delete##{import.Id.ToString()}")) {
                        ImGui.OpenPopup($"{import.Id.ToString()}-DeletePopup");
                    }
                    ImGui.TableNextColumn();


                }


                ImGui.EndTable();




                ImGui.EndChild();

                //if(ImGui.Button("Save")) {
                //    //Plugin.StorageManager.UpdateDutyResults(_dutyResults.Where(dr => dr.IsEdited));
                //}
            }

            if(_currentPage > 0) {
                ImGui.SameLine();
                if(ImGui.Button("Previous 100")) {
                    Refresh(_currentPage - 1);
                }
            }

            //not sure if need to protect boundary

            if(_imports.Count >= 100) {
                ImGui.SameLine();
                if(ImGui.Button("Next 100")) {
                    Refresh(_currentPage + 1);
                }
            }


        }

        public void DrawImport(DutyResultsImport import) {
            //List<string> lastCheckpoints = new() {
            //    "None"
            //};
            //var owner = dutyResults.Owner;
            //var gil = dutyResults.TotalGil.ToString();
            //var isCompleted = dutyResults.IsComplete;
            //foreach(var checkpoint in Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints!) {
            //    lastCheckpoints.Add(checkpoint.Name);
            //}

            if(ImGui.CollapsingHeader(String.Format("{0:-23}     {1:-40}", import.Time.ToString(), Plugin.DutyManager.Duties[import.DutyId].Name))) {
                //if(ImGui.Checkbox($"Completed##{dutyResults.Id}", ref isCompleted)) {
                //    dutyResults.IsEdited = true;
                //    dutyResults.IsComplete = isCompleted;
                //}
                //if(ImGui.InputText($"Owner##{dutyResults.Id}", ref owner, 50, ImGuiInputTextFlags.AutoSelectAll)) {
                //    dutyResults.IsEdited = true;
                //    dutyResults.Owner = owner;
                //}
                //if(ImGui.InputText($"Total Gil##{dutyResults.Id}", ref gil, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                //    int gilInt;
                //    if(int.TryParse(gil, out gilInt)) {
                //        dutyResults.IsEdited = true;
                //        dutyResults.TotalGil = gilInt;
                //    }
                //}
                //var currentLastCheckpointIndex = dutyResults.CheckpointResults.Count;
                ////ImGui.Text($"Last checkpoint: {dutyResults.CheckpointResults.Last().Checkpoint.Name}");
                //if(ImGui.Combo($"Last Checkpoint##{dutyResults.Id}", ref currentLastCheckpointIndex, lastCheckpoints.ToArray(), lastCheckpoints.Count)) {
                //    if(currentLastCheckpointIndex > dutyResults.CheckpointResults.Count) {
                //        dutyResults.IsEdited = true;
                //        for(int i = dutyResults.CheckpointResults.Count; i <= currentLastCheckpointIndex - 1; i++) {
                //            dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints![i], true));
                //        }
                //    } else if(currentLastCheckpointIndex < dutyResults.CheckpointResults.Count) {
                //        dutyResults.IsEdited = true;
                //        for(int i = dutyResults.CheckpointResults.Count - 1; i >= currentLastCheckpointIndex; i--) {
                //            dutyResults.CheckpointResults.RemoveAt(i);
                //        }
                //    }

                //    //dutyResults.CheckpointResults = new();
                //    //for(int i = 0; i <= currentLastCheckpointIndex; i++) {
                //    //    dutyResults.CheckpointResults.Add(new CheckpointResults(Plugin.DutyManager.Duties[dutyResults.DutyId].Checkpoints[i], true));
                //    //}
                //}
                //if(Plugin.DutyManager.Duties[dutyResults.DutyId].Structure == DutyStructure.Roulette) {
                //    string[] summons = { "Lesser", "Greater", "Elder", "Abomination", "Circle Shift" };
                //    var summonCheckpoints = dutyResults.CheckpointResults.Where(cr => cr.Checkpoint.Name.StartsWith("Complete")).ToList();
                //    for(int i = 0; i < summonCheckpoints.Count(); i++) {
                //        var summonIndex = summonCheckpoints[i].SummonType == null ? 3 : (int)summonCheckpoints[i].SummonType;
                //        //ImGui.Text($"{summons[(int)summonCheckpoints[i].SummonType]}");
                //        if(ImGui.Combo($"{StatsWindow.AddOrdinal(i + 1)} Summon##{summonCheckpoints[i].GetHashCode()}", ref summonIndex, summons, summons.Length)) {
                //            dutyResults.IsEdited = true;
                //            summonCheckpoints[i].SummonType = (Summon)summonIndex;
                //        }
                //    }
                //}
            }
        }
    }
}
