using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class ConfigWindow : Window, IDisposable {

        private MainWindow MainWindow { get; set; }
        private Plugin Plugin;

        public ConfigWindow(Plugin plugin) : base("Map Party Assist Settings") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {
            this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y + MainWindow.CurrentSize.Y);
            bool enableSolo = Plugin.Configuration.EnableWhileSolo;
            if(ImGui.Checkbox("Enable While Solo", ref enableSolo)) {
                Plugin.ToggleEnableSolo(enableSolo);
            }
        }
    }
}
