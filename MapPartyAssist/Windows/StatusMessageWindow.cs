using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class StatusMessageWindow : Window {

        private Plugin _plugin;
        private MainWindow _mainWindow;

        internal StatusMessageWindow(Plugin plugin, MainWindow mainWindow) : base("Status") {
            ShowCloseButton = false;
            CollapsedCondition = ImGuiCond.None;
            Flags = Flags | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar;
            PositionCondition = ImGuiCond.Always;
            //this.SizeConstraints = new WindowSizeConstraints {
            //    MinimumSize = new Vector2(150, 0),
            //    MaximumSize = new Vector2(500, 500)
            //};
            _mainWindow = mainWindow;
            _plugin = plugin;
        }

        internal void Refresh() {
        }

        public override void PreDraw() {
            base.PreDraw();
            Position = new Vector2(_mainWindow.CurrentPosition.X, _mainWindow.CurrentPosition.Y - 15f - 17f * ImGuiHelpers.GlobalScale);
        }

        public override void Draw() {
            Vector4 color;
            switch(_plugin.MapManager.Status) {
                case StatusLevel.OK:
                default:
                    color = ImGuiColors.DalamudWhite;
                    break;
                case StatusLevel.CAUTION:
                    color = ImGuiColors.DalamudOrange;
                    break;
                case StatusLevel.ERROR:
                    color = ImGuiColors.DalamudRed;
                    break;
            }

            if(_plugin.MapManager.Status != StatusLevel.OK) {
                ImGui.TextColored(color, _plugin.MapManager.StatusMessage);
            }
        }
    }
}
