using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using MapPartyAssist.Helper;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace MapPartyAssist.Windows {
    internal class ViewDutyResultsImportsWindow : Window {
        private Plugin _plugin;
        private StatsWindow _statsWindow;
        private AddDutyResultsImportWindow _addImportWindow;
        private List<DutyResultsImport> _imports = new();
        private int _currentPage = 0;

        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        internal ViewDutyResultsImportsWindow(Plugin plugin, StatsWindow statsWindow) : base("Manage Imports") {
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(350, 150),
                MaximumSize = new Vector2(500, 800)
            };
            _plugin = plugin;
            PositionCondition = ImGuiCond.Appearing;
            _statsWindow = statsWindow;
            _addImportWindow = new AddDutyResultsImportWindow(plugin, this);
            _addImportWindow.IsOpen = false;
            _plugin.WindowSystem.AddWindow(_addImportWindow);
        }

        public void Refresh(int? pageIndex = 0) {
            pageIndex ??= _currentPage;
            _currentPage = (int)pageIndex;
            _imports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted).OrderByDescending(i => i.Time).Offset(_currentPage * 100).Limit(100).ToList();


            //return Task.Run(async () => {
            //    try {
            //        await _refreshLock.WaitAsync();
            //        //null index = stay on same page
            //        pageIndex ??= _currentPage;
            //        _currentPage = (int)pageIndex;
            //        _imports = _plugin.StorageManager.GetDutyResultsImports().Query().Where(i => !i.IsDeleted).OrderByDescending(i => i.Time).Offset(_currentPage * 100).Limit(100).ToList();
            //    } finally {
            //        _refreshLock.Release();
            //    }
            //});
        }

        public override void OnClose() {
            base.OnClose();
            _addImportWindow.IsOpen = false;
            _plugin.DataQueue.QueueDataOperation(() => {
                Refresh();
            });
        }

        public override void Draw() {
            if(ImGui.Button("New Import")) {
                _addImportWindow.BringToFront();
                if(!_addImportWindow.IsOpen) {
                    _addImportWindow.Position = new Vector2(ImGui.GetWindowPos().X + 50f * ImGuiHelpers.GlobalScale, ImGui.GetWindowPos().Y + 50f * ImGuiHelpers.GlobalScale);
                    _addImportWindow.Open();
                }
            }
            ImGui.SameLine();
            ImGuiHelper.HelpMarker("Use imports to include stats that were tracked when the plugin was unavailable.");

            if(_imports.Count > 0) {
                using(var child = ImRaii.Child("scrolling", new Vector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y) * ImGuiHelpers.GlobalScale), true)) {
                    if(child) {
                        using var table = ImRaii.Table($"AddTable", 4, ImGuiTableFlags.NoHostExtendX);
                        if(table) {
                            ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 130);
                            ImGui.TableSetupColumn("duty", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 200);
                            ImGui.TableSetupColumn("edit", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 30);
                            ImGui.TableSetupColumn("delete", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 50);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            foreach(var import in _imports) {
                                DrawImport(import);
                            }
                        }
                    }
                }
            }

            if(_currentPage > 0) {
                ImGui.SameLine();
                if(ImGui.Button("Previous 100")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        Refresh(_currentPage - 1);
                    });
                }
            }

            //not sure if need to protect boundary
            if(_imports.Count >= 100) {
                ImGui.SameLine();
                if(ImGui.Button("Next 100")) {
                    _plugin.DataQueue.QueueDataOperation(() => {
                        Refresh(_currentPage + 1);
                    });
                }

            }
        }

        private void DrawImport(DutyResultsImport import) {
            ImGui.Text($"{import.Time.ToString()}");
            ImGui.TableNextColumn();
            ImGui.Text($"{_plugin.DutyManager.Duties[import.DutyId].GetDisplayName()}");
            ImGui.TableNextColumn();
            if(ImGui.Button($"Edit##{import.Id.ToString()}")) {
                _addImportWindow.Open(import);
            }
            ImGui.TableNextColumn();
            using(var popup = ImRaii.Popup($"{import.Id.ToString()}-DeletePopup")) {
                if(popup) {
                    ImGui.Text("Are you sure?");
                    if(ImGui.Button($"Yes##{import.Id.ToString()}-ConfirmDelete")) {
                        _plugin.DataQueue.QueueDataOperation(async () => {
                            import.IsDeleted = true;
                            await _plugin.StorageManager.UpdateDutyResultsImport(import);
                            await _plugin.Refresh();
                        });
                    }
                    ImGui.SameLine();
                    if(ImGui.Button($"Cancel##{import.Id.ToString()}-CancelDelete")) {
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
            if(ImGui.Button($"Delete##{import.Id.ToString()}")) {
                ImGui.OpenPopup($"{import.Id.ToString()}-DeletePopup");
            }

            //only go to next column if not last
            if(_imports.IndexOf(import) < _imports.Count - 1) {
                ImGui.TableNextColumn();
            }
        }
    }
}
