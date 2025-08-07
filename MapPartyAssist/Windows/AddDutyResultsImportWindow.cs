using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class AddDutyResultsImportWindow : Window {

        private Plugin _plugin;
        private ViewDutyResultsImportsWindow _viewWindow;
        private DutyResultsImport _model = new();

        private string _statusMessage = "";
        private int _selectedDuty = 0;
        private readonly int[] _dutyIdCombo = { 0, 179, 268, 276, 586, 688, 745, 819, 909, 993, 1060 };
        private readonly string[] _dutyNameCombo;

        private const float _inputWidth = 200f;

        internal AddDutyResultsImportWindow(Plugin plugin, ViewDutyResultsImportsWindow viewWindow) : base("Import Statistics") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 150),
                MaximumSize = new Vector2(500, 800)
            };
            _plugin = plugin;
            _viewWindow = viewWindow;
            PositionCondition = ImGuiCond.Appearing;
            Flags = Flags | ImGuiWindowFlags.NoCollapse;

            //setup duty name combo
            _dutyNameCombo = new string[_dutyIdCombo.Length];
            _dutyNameCombo[0] = "";
            for(int i = 1; i < _dutyIdCombo.Length; i++) {
                _dutyNameCombo[i] = _plugin.DutyManager.Duties[_dutyIdCombo[i]].GetDisplayName();
            }
        }

        public override void OnClose() {
            ClearModel();
            //clear edits to existing imports
            _viewWindow.Refresh();
            base.OnClose();
        }

        public override void Draw() {
            using(var child = ImRaii.Child("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true)) {
                if(child) {
                    using var table = ImRaii.Table($"AddTable", 3, ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip);
                    if(table) {
                        ImGui.TableSetupColumn("enabled", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 40);
                        ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200);
                        //ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        //ImGui.Text("Recorded?");
                        //ImGui.TableNextColumn();
                        //ImGui.TableNextColumn();
                        //ImGui.TableNextColumn();

                        if(ImGui.Button("Now")) {
                            _model.Time = DateTime.Now;
                        }

                        ImGui.TableNextColumn();
                        var timeString = _model.Time.ToString();
                        ImGui.SetNextItemWidth(_inputWidth);
                        if(ImGui.InputText($"##TimeInput", ref timeString, 30)) {
                            DateTime time;
                            if(DateTime.TryParse(timeString, out time)) {
                                _model.Time = time;
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Time");
                        ImGui.SameLine();
                        ImGuiHelper.HelpMarker("When to insert data. Auto-formats date\nusing your local timezone.");

                        //ImGui.TableNextColumn();
                        //ImGui.TableNextColumn();
                        //if(ImGui.Button("Now")) {
                        //    _model.Time = DateTime.Now;
                        //}
                        //ImGui.TableNextColumn();

                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(_inputWidth);
                        if(ImGui.Combo($"##DutyCombo", ref _selectedDuty, _dutyNameCombo, _dutyNameCombo.Length)) {
                            _model.DutyId = _dutyIdCombo[_selectedDuty];
                            _model.CheckpointTotals = null;
                            _model.SummonTotals = null;
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Duty");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();

                        var totalRuns = _model.TotalRuns.ToString();
                        ImGui.SetNextItemWidth(_inputWidth);
                        if(ImGui.InputText($"##RunsInput", ref totalRuns, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                            uint runsInt;
                            if(uint.TryParse(totalRuns, out runsInt)) {
                                _model.TotalRuns = runsInt;
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Total Runs");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();

                        var totalClears = _model.TotalClears.ToString();
                        ImGui.SetNextItemWidth(_inputWidth);
                        if(ImGui.InputText($"##ClearsInput", ref totalClears, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                            uint clearsInt;
                            if(uint.TryParse(totalClears, out clearsInt)) {
                                _model.TotalClears = clearsInt;
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Total Clears");
                        ImGui.TableNextColumn();

                        bool hasGil = _model.TotalGil != null;
                        if(ImGui.Checkbox($"##HasGil", ref hasGil)) {
                            if(!hasGil) {
                                _model.TotalGil = null;
                            } else {
                                _model.TotalGil = 0;
                            }
                        }
                        ImGui.TableNextColumn();
                        var gil = _model.TotalGil.ToString();
                        ImGui.SetNextItemWidth(_inputWidth);
                        if(ImGui.InputText($"##GilInput", ref gil, 10, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                            uint gilInt;
                            if(uint.TryParse(gil, out gilInt)) {
                                //_hasGil = true;
                                _model.TotalGil = gilInt;
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Total Gil");
                        ImGui.SameLine();
                        ImGuiHelper.HelpMarker("Check box if gil was tracked.");
                        ImGui.TableNextColumn();

                        bool hasFloors = _model.CheckpointTotals != null;
                        if(ImGui.Checkbox($"##HasFloors", ref hasFloors)) {
                            if(!hasFloors) {
                                _model.CheckpointTotals = null;
                            } else if(_model.DutyId == 0) {
                                hasFloors = false;
                            } else {
                                _plugin.ImportManager.SetupCheckpointTotals(_model);
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.Text("Checkpoint Totals");
                        ImGui.SameLine();
                        ImGuiHelper.HelpMarker("Total number of times each checkpoint was reached.\nCheck box if this was tracked, but select a duty first!");
                        ImGui.TableNextColumn();

                        if(hasFloors) {
                            for(int i = 0; i < _model.CheckpointTotals!.Count; i++) {
                                var checkpointTotal = _model.CheckpointTotals[i];
                                ImGui.TableNextColumn();
                                var reachedCount = checkpointTotal.ToString();
                                ImGui.SetNextItemWidth(_inputWidth);
                                if(ImGui.InputText($"##{_plugin.DutyManager.Duties[_model.DutyId].Checkpoints![i].GetHashCode()}-Input", ref reachedCount, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                                    uint reachedCountInt;
                                    if(uint.TryParse(reachedCount, out reachedCountInt)) {
                                        _model.CheckpointTotals[i] = reachedCountInt;
                                    }
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{_plugin.DutyManager.Duties[_model.DutyId].Checkpoints![i].Name}");
                                ImGui.TableNextColumn();
                            }
                        }

                        bool hasSequence = _model.ClearSequence != null;
                        if(ImGui.Checkbox($"##HasSequence", ref hasSequence)) {
                            if(!hasSequence) {
                                _model.ClearSequence = null;
                                _model.RunsSinceLastClear = null;
                            } else {
                                _model.ClearSequence = new() { 0 };
                                _model.RunsSinceLastClear = 0;
                            }
                        }
                        ImGui.TableNextColumn();
                        if(hasSequence) {
                            if(ImGui.Button("Add New Clear")) {
                                _model.ClearSequence!.Add(0);
                            }
                        }
                        ImGui.TableNextColumn();
                        ImGui.Text("Clear Sequence");
                        ImGui.SameLine();
                        ImGuiHelper.HelpMarker("Runs between each clear.\nCheck box if this was tracked.");
                        ImGui.TableNextColumn();
                        if(hasSequence) {
                            for(int i = 0; i < _model.ClearSequence!.Count; i++) {
                                var clear = _model.ClearSequence[i];
                                var clearString = clear.ToString();
                                //ImGui.SameLine();
                                try {
                                    ImGui.PushFont(UiBuilder.IconFont);
                                    if(i != 0) {
                                        if(ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{i}--DeleteClear")) {
                                            _model.ClearSequence.RemoveAt(i);
                                            //if(_model.ClearSequence.Count <= 0) {
                                            //    _model.ClearSequence = null;
                                            //}
                                        }
                                    }
                                } finally {
                                    ImGui.PopFont();
                                }
                                ImGui.TableNextColumn();
                                ImGui.SetNextItemWidth(_inputWidth);
                                if(ImGui.InputText($"##{i}--ClearInput", ref clearString, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                                    uint clearInt;
                                    if(uint.TryParse(clearString, out clearInt)) {
                                        _model.ClearSequence[i] = clearInt;
                                    }
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{StringHelper.AddOrdinal(i + 1)} clear");
                                ImGui.TableNextColumn();
                            }

                            if(_model.ClearSequence.Count > 0) {
                                ImGui.TableNextColumn();
                                var runsSinceLastClearString = _model.RunsSinceLastClear.ToString();
                                ImGui.SetNextItemWidth(_inputWidth);
                                if(ImGui.InputText($"##RunsSinceLastClear", ref runsSinceLastClearString, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                                    uint runsSinceLastClearInt;
                                    if(uint.TryParse(runsSinceLastClearString, out runsSinceLastClearInt)) {
                                        _model.RunsSinceLastClear = runsSinceLastClearInt;
                                    }
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text("Runs since last clear");
                                ImGui.TableNextColumn();
                            }
                        }

                        if(_model.DutyId != 0 && 
                            (_plugin.DutyManager.Duties[_model.DutyId].Structure == DutyStructure.Roulette || _plugin.DutyManager.Duties[_model.DutyId].Structure == DutyStructure.Slots)) {
                            bool hasSummons = _model.SummonTotals != null;
                            if(ImGui.Checkbox($"##HasSummons", ref hasSummons)) {
                                if(!hasSummons) {
                                    _model.SummonTotals = null;
                                } else {
                                    _model.InitializeSummonsTotals();
                                }
                            }
                            ImGui.TableNextColumn();
                            ImGui.TableNextColumn();
                            ImGui.Text("Total Summons");
                            ImGui.SameLine();
                            ImGuiHelper.HelpMarker("Total summons of each type.\nCheck box if this was tracked.");
                            ImGui.TableNextColumn();

                            if(hasSummons) {
                                foreach(var summon in _model.SummonTotals!) {
                                    ImGui.TableNextColumn();
                                    var summonString = summon.Value.ToString();
                                    ImGui.SetNextItemWidth(_inputWidth);
                                    if(ImGui.InputText($"##{summon.Key}--SummonInput", ref summonString, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                                        uint summonInt;
                                        if(uint.TryParse(summonString, out summonInt)) {
                                            _model.SummonTotals[summon.Key] = summonInt;
                                        }
                                    }
                                    ImGui.TableNextColumn();
                                    string label = "";
                                    switch(summon.Key) {
                                        case Summon.Lesser:
                                            label = "Lesser summons";
                                            break;
                                        case Summon.Greater:
                                            label = "Greater summons";
                                            break;
                                        case Summon.Elder:
                                            label = "Elder summons";
                                            break;
                                        case Summon.Gold:
                                            label = "Circle shifts";
                                            if(_plugin.DutyManager.Duties[_model.DutyId].Structure == DutyStructure.Slots) {
                                                label = "Final summons";
                                            }
                                            break;
                                        case Summon.Silver:
                                            label = "Abominations";
                                            if(_plugin.DutyManager.Duties[_model.DutyId].Structure == DutyStructure.Slots) {
                                                label = "Fever dreams";
                                            }
                                            break;
                                        default:
                                            label = "";
                                            break;
                                    }
                                    ImGui.Text($"{label}");
                                    ImGui.TableNextColumn();
                                }
                            }
                        }
                    }
                }
            }

            if(ImGui.Button("Save")) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    if(_plugin.ImportManager.ValidateImport(_model)) {
                        //save
                        _plugin.Log.Information("Valid Import");
                        _plugin.ImportManager.AddorEditImport(_model, false);
                        _statusMessage = "";
                        IsOpen = false;
                        //_plugin.Save();
                    } else {
                        _plugin.Log.Information("Invalid Import");
                        _statusMessage = "Invalid data, check numbers.";
                    }
                });
            }

            ImGui.SameLine();
            if(ImGui.Button("Cancel")) {
                ClearModel();
                IsOpen = false;
            }

            if(!_statusMessage.IsNullOrEmpty()) {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed, $"{_statusMessage}");
            }
        }

        private void ClearModel() {
            _model = new();
            _selectedDuty = 0;
        }

        internal void Open(DutyResultsImport? import = null, int? selectedDuty = null) {
            _model = import ?? new();
            if(import != null) {
                _selectedDuty = DutyIdToIndex(_model.DutyId);
            } else {
                _selectedDuty = selectedDuty ?? 0;
            }

            IsOpen = true;
        }

        private int DutyIdToIndex(int dutyId) {
            for(int i = 0; i < _dutyIdCombo.Length; i++) {
                if(dutyId == _dutyIdCombo[i]) {
                    return i;
                }
            }
            return 0;
        }
    }
}
