using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class StatusMessageWindow : Window, IDisposable {

        private MainWindow MainWindow { get; set; }
        private Plugin Plugin;
        public Dictionary<string, int> Zones { get; set; } = new();

        public StatusMessageWindow(Plugin plugin, MainWindow mainWindow) : base("Status") {
            this.ShowCloseButton = false;
            this.CollapsedCondition = ImGuiCond.None;
            this.Flags = Flags | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar;
            this.PositionCondition = ImGuiCond.Always;
            //this.SizeConstraints = new WindowSizeConstraints {
            //    MinimumSize = new Vector2(150, 0),
            //    MaximumSize = new Vector2(500, 500)
            //};
            this.MainWindow = mainWindow;
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public void Refresh() {

        }

        public override void PreDraw() {
            base.PreDraw();
            this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y - 15f - 17f * ImGuiHelpers.GlobalScale);
        }

        public override void Draw() {
            Vector4 color;
            switch(Plugin.MapManager.Status) {
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


            if(Plugin.MapManager.Status != StatusLevel.OK) {
                //var color = Plugin.MapManager.Status == StatusLevel.CAUTION ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudRed;
                ImGui.TextColored(color, Plugin.MapManager.StatusMessage);
            }
        }

    }
}
