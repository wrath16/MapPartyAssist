using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {
    public class ZoneCountWindow : Window, IDisposable {

        private MainWindow MainWindow { get; set; }
        private Plugin Plugin;

        public ZoneCountWindow(Plugin plugin, MainWindow mainWindow) : base("Map Links by Zone") {
            this.ShowCloseButton = false;
            this.Flags = Flags | ImGuiWindowFlags.AlwaysAutoResize;
            this.PositionCondition = ImGuiCond.Always;
            this.SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            this.MainWindow = mainWindow;
            this.Plugin = plugin;
        }

        public void Dispose() {
        }

        public override void Draw() {
            //this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y + MainWindow.CurrentSize.Y);

            //PluginLog.Debug($"{ImGui.GetWindowPos()}");
            ZoneCountTable(Plugin.CurrentPartyList);
            //ZoneCountTable(Plugin.FakePartyList);
            //this.Position.
        }

        public override void PreDraw() {
            base.PreDraw();
            this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y + MainWindow.CurrentSize.Y);
        }


        private void ZoneCountTable(Dictionary<string, MPAMember> list) {
            Dictionary<string, int> zones = new();
            foreach(MPAMember player in list.Values.Where(p => p.MapLink != null)) {
                if(zones.ContainsKey(player.MapLink!.PlaceName)) {
                    zones[player.MapLink.PlaceName] += 1;
                } else {
                    zones.Add(player.MapLink.PlaceName, 1);
                }
            }

            if(ImGui.BeginTable($"##{list.GetHashCode()}_Zone_Table", 2, ImGuiTableFlags.NoHostExtendX)) {
                ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 158f);
                ImGui.TableSetupColumn("count", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 15);
                foreach(var zone in zones.OrderBy(kvp => kvp.Key)) {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{zone.Key}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{zone.Value}");
                }
                ImGui.EndTable();
            }
        }
    }
}
