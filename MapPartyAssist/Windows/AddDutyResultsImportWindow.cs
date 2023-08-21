using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Numerics;


namespace MapPartyAssist.Windows {
    internal class AddDutyResultsImportWindow : Window, IDisposable {

        private Plugin Plugin;
        private DutyResultsImport _model;
        private string _statusMessage;
        //private bool _hasClearSequence;
        //private bool _hasDoors;
        //private bool _hasSummons;
        //private bool _hasGil;
        private int _selectedDuty = 0;

        public AddDutyResultsImportWindow(Plugin plugin) : base("Import Statistics") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 150),
                MaximumSize = new Vector2(600, 800)
            };
            Plugin = plugin;
            _model = new();
            PositionCondition = ImGuiCond.Appearing;
            Flags = Flags | ImGuiWindowFlags.NoCollapse;
        }

        public void Dispose() {

        }

        public override void OnClose() {
            base.OnClose();
            ClearModel();
        }

        public override void Draw() {
            string[] duties = { "", "The Aquapolis", "The Lost Canals of Uznair", "The Hidden Canals of Uznair", "The Shifting Altars of Uznair", "The Dungeons of Lyhe Ghiah", "The Shifting Oubliettes of Lyhe Ghiah", "The Excitatron 6000", "The Shifting Gymnasion Agonon" };


            ImGui.BeginChild("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true);

            ImGui.BeginTable($"AddTable", 3, ImGuiTableFlags.BordersInner | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoClip);
            ImGui.TableSetupColumn("enabled", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 25);
            ImGui.TableSetupColumn("field", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 200);
            ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            //ImGui.Text("Recorded?");
            //ImGui.TableNextColumn();
            //ImGui.TableNextColumn();
            //ImGui.TableNextColumn();


            ImGui.TableNextColumn();
            var timeString = _model.Time.ToString();
            if(ImGui.InputText($"##TimeInput", ref timeString, 30)) {
                DateTime time;
                if(DateTime.TryParse(timeString, out time)) {
                    _model.Time = time;
                }
            }
            ImGui.TableNextColumn();
            ImGui.Text("Time");
            ImGuiComponents.HelpMarker("When to insert data. Auto-formats date\nusing your local timezone.");
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            if(ImGui.Combo($"##DutyCombo", ref _selectedDuty, duties, 9)) {
                switch(_selectedDuty) {
                    case 0:
                    default:
                        _model.DutyId = 0;
                        break;
                    case 1:
                        _model.DutyId = 179;
                        break;
                    case 2:
                        _model.DutyId = 268;
                        break;
                    case 3:
                        _model.DutyId = 276;
                        break;
                    case 4:
                        _model.DutyId = 586;
                        break;
                    case 5:
                        _model.DutyId = 688;
                        break;
                    case 6:
                        _model.DutyId = 745;
                        break;
                    case 7:
                        _model.DutyId = 819;
                        break;
                    case 8:
                        _model.DutyId = 909;
                        break;
                }
                _model.CheckpointTotals = null;
                _model.SummonTotals = null;
                //Plugin.ImportManager.SetupCheckpointTotals(_model);
            }
            ImGui.TableNextColumn();
            ImGui.Text("Duty");
            ImGui.TableNextColumn();


            ImGui.TableNextColumn();
            var totalRuns = _model.TotalRuns.ToString();
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
            if(ImGui.InputText($"##GilInput", ref gil, 10, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                uint gilInt;
                if(uint.TryParse(gil, out gilInt)) {
                    //_hasGil = true;
                    _model.TotalGil = gilInt;
                }
            }
            ImGui.TableNextColumn();
            ImGui.Text("Total Gil");
            ImGuiComponents.HelpMarker("Check box if gil was tracked.");
            ImGui.TableNextColumn();
            bool hasFloors = _model.CheckpointTotals != null;
            if(ImGui.Checkbox($"##HasFloors", ref hasFloors)) {
                if(!hasFloors) {
                    _model.CheckpointTotals = null;
                } else if(_model.DutyId == 0) {
                    hasFloors = false;
                } else {
                    Plugin.ImportManager.SetupCheckpointTotals(_model);
                }
            }
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.Text("Checkpoint Totals");
            ImGuiComponents.HelpMarker("Total number of times each checkpoint was reached.\nCheck box if this was tracked, but select a duty first!");
            ImGui.TableNextColumn();

            if(hasFloors) {

                for(int i = 0; i < _model.CheckpointTotals.Count; i++) {
                    var checkpointTotal = _model.CheckpointTotals[i];
                    ImGui.TableNextColumn();
                    var reachedCount = checkpointTotal.ToString();
                    if(ImGui.InputText($"##{Plugin.DutyManager.Duties[_model.DutyId].Checkpoints[i].GetHashCode()}-Input", ref reachedCount, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                        uint reachedCountInt;
                        if(uint.TryParse(reachedCount, out reachedCountInt)) {
                            _model.CheckpointTotals[i] = reachedCountInt;
                        }
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text($"{Plugin.DutyManager.Duties[_model.DutyId].Checkpoints[i].Name}");
                    ImGui.TableNextColumn();
                }

                //foreach(var checkpointTotal in _model.CheckpointTotals!) {
                //    ImGui.TableNextColumn();
                //    var reachedCount = checkpointTotal.ToString();
                //    if(ImGui.InputText($"##{checkpointTotal.GetHashCode()}-Input", ref reachedCount, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                //        uint reachedCountInt;
                //        if(uint.TryParse(reachedCount, out reachedCountInt)) {
                //            //_model.CheckpointTotals[checkpointTotal.Key] = reachedCountInt;
                //        }
                //    }
                //    ImGui.TableNextColumn();
                //    ImGui.Text($"{checkpointTotal.Key.Name}");
                //    ImGui.TableNextColumn();
                //}
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
            ImGuiComponents.HelpMarker("Runs between each clear.\nCheck box if this was tracked.");
            ImGui.TableNextColumn();

            if(hasSequence) {
                for(int i = 0; i < _model.ClearSequence!.Count; i++) {
                    var clear = _model.ClearSequence[i];
                    var clearString = clear.ToString();
                    ImGui.TableNextColumn();
                    if(ImGui.InputText($"##{i}-ClearInput", ref clearString, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
                        uint clearInt;
                        if(uint.TryParse(clearString, out clearInt)) {
                            _model.ClearSequence[i] = clearInt;
                        }
                    }
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if(i != 0) {
                        if(ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{i}--deleteClear")) {
                            _model.ClearSequence.RemoveAt(i);
                            //if(_model.ClearSequence.Count <= 0) {
                            //    _model.ClearSequence = null;
                            //}
                        }
                    }

                    ImGui.PopFont();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{StatsWindow.AddOrdinal(i + 1)} clear");
                    ImGui.TableNextColumn();
                }

                if(_model.ClearSequence.Count > 0) {
                    ImGui.TableNextColumn();
                    var runsSinceLastClearString = _model.RunsSinceLastClear.ToString();
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

            if(_model.DutyId != 0 && Plugin.DutyManager.Duties[_model.DutyId].Structure == DutyStructure.Roulette) {
                bool hasSummons = _model.SummonTotals != null;
                if(ImGui.Checkbox($"##HasSummons", ref hasSummons)) {
                    if(!hasSummons) {
                        _model.SummonTotals = null;
                    } else {
                        Plugin.ImportManager.SetupSummonsTotals(_model);
                    }
                }
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total Summons");
                ImGuiComponents.HelpMarker("Total summons of each type.\nCheck box if this was tracked.");
                ImGui.TableNextColumn();

                if(hasSummons) {
                    foreach(var summon in _model.SummonTotals!) {
                        ImGui.TableNextColumn();
                        var summonString = summon.Value.ToString();
                        if(ImGui.InputText($"##{summon.Key}-SummonInput", ref summonString, 9, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal)) {
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
                                break;
                            case Summon.Silver:
                                label = "Abominations";
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

            ImGui.EndTable();
            ImGui.EndChild();

            if(ImGui.Button("Save")) {
                if(Plugin.ImportManager.ValidateImport(_model)) {
                    //save
                    PluginLog.Debug("Valid Import");
                    Plugin.ImportManager.AddorEditImport(_model, false);
                    _statusMessage = "";
                    IsOpen = false;
                } else {
                    PluginLog.Debug("Invalid Import");
                    _statusMessage = "Invalid data, check numbers.";
                }
            }

            if(ImGui.Button("Cancel")) {
                ClearModel();
                IsOpen = false;
            }

            if(!_statusMessage.IsNullOrEmpty()) {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudRed, $"{_statusMessage}");
            }
        }


        public void ClearModel() {
            _model = new();
            _selectedDuty = 0;
            //_hasClearSequence = false;
            //_hasDoors = false;
            //_hasSummons = false;
            //_hasGil = false;
        }

        public void OpenAsEdit(DutyResultsImport import) {
            _model = import;
            switch(_model.DutyId) {
                case 0:
                default:
                    _selectedDuty = 0;
                    break;
                case 179:
                    _selectedDuty = 1;
                    break;
                case 268:
                    _selectedDuty = 2;
                    break;
                case 276:
                    _selectedDuty = 3;
                    break;
                case 586:
                    _selectedDuty = 4;
                    break;
                case 688:
                    _selectedDuty = 5;
                    break;
                case 745:
                    _selectedDuty = 6;
                    break;
                case 819:
                    _selectedDuty = 7;
                    break;
                case 909:
                    _selectedDuty = 8;
                    break;
            }
            this.IsOpen = true;
        }
    }
}
