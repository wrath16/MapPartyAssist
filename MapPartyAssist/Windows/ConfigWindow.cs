using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class ConfigWindow : Window, IDisposable {

        //private MainWindow MainWindow { get; set; }
        private Plugin Plugin;

        public ConfigWindow(Plugin plugin) : base("Map Party Assist Settings") {
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(300, 50),
                MaximumSize = new Vector2(400, 500)
            };
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {
            //bool enableSolo = Plugin.Configuration.EnableWhileSolo;
            //if(ImGui.Checkbox("Enable While Solo", ref enableSolo)) {
            //    Plugin.ToggleEnableSolo(enableSolo);
            //}

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Map Window");


            bool hideZoneTable = Plugin.Configuration.HideZoneTableWhenEmpty;
            if(ImGui.Checkbox("Hide 'Map Links by Zone' when empty", ref hideZoneTable)) {
                Plugin.Configuration.HideZoneTableWhenEmpty = hideZoneTable;
                Plugin.Save();
            }

            bool requireDoubleTap = Plugin.Configuration.RequireDoubleClickOnClearAll;
            if(ImGui.Checkbox("Require double click on 'Clear All'", ref requireDoubleTap)) {
                Plugin.Configuration.RequireDoubleClickOnClearAll = requireDoubleTap;
                Plugin.Save();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Stats Window");
        }
    }
}
