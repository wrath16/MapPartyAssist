using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MapPartyAssist.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MapPartyAssist.Windows {
    internal class ZoneCountWindow : Window {

        private Plugin _plugin;
        private MainWindow _mainWindow;

        internal Dictionary<string, int> Zones { get; set; } = new();

        internal ZoneCountWindow(Plugin plugin, MainWindow mainWindow) : base("Map Links by Zone") {
            ShowCloseButton = false;
            Flags = Flags | ImGuiWindowFlags.AlwaysAutoResize;
            PositionCondition = ImGuiCond.Always;
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = new Vector2(150, 50),
                MaximumSize = new Vector2(500, 500)
            };
            _mainWindow = mainWindow;
            _plugin = plugin;
        }

        internal void Refresh() {
            UpdateZoneCountTable();
        }

        private void UpdateZoneCountTable() {
            Zones = new();
            foreach(MPAMember player in _plugin.CurrentPartyList.Values.Where(p => p.MapLink != null)) {
                if(Zones.ContainsKey(player.MapLink!.GetMapLinkPayload().PlaceName)) {
                    Zones[player.MapLink.GetMapLinkPayload().PlaceName] += 1;
                } else {
                    Zones.Add(player.MapLink.GetMapLinkPayload().PlaceName, 1);
                }
            }
        }

        public override void PreDraw() {
            base.PreDraw();
            Position = new Vector2(_mainWindow.CurrentPosition.X, _mainWindow.CurrentPosition.Y + _mainWindow.CurrentSize.Y);
        }

        public override void Draw() {
            if(ImGui.BeginTable($"##ZoneTable", 2, ImGuiTableFlags.NoHostExtendX)) {
                ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch, ImGuiHelpers.GlobalScale * 158f);
                ImGui.TableSetupColumn("count", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 15);
                foreach(var zone in Zones.OrderBy(kvp => kvp.Key)) {
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
