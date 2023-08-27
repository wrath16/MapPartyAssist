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
        public Dictionary<string, int> Zones { get; set; } = new();

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

        public void Refresh() {
            UpdateZoneCountTable();
        }

        private void UpdateZoneCountTable() {
            Zones = new();
            foreach(MPAMember player in Plugin.CurrentPartyList.Values.Where(p => p.MapLink != null)) {
                if(Zones.ContainsKey(player.MapLink!.GetMapLinkPayload().PlaceName)) {
                    Zones[player.MapLink.GetMapLinkPayload().PlaceName] += 1;
                } else {
                    Zones.Add(player.MapLink.GetMapLinkPayload().PlaceName, 1);
                }
            }
        }

        public override void PreDraw() {
            base.PreDraw();
            this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y + MainWindow.CurrentSize.Y);
        }

        public override void Draw() {
            //this.Position = new Vector2(MainWindow.CurrentPosition.X, MainWindow.CurrentPosition.Y + MainWindow.CurrentSize.Y);
            //PluginLog.Debug($"{ImGui.GetWindowPos()}");

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


            //ZoneCountTable(Plugin.CurrentPartyList);
            //ZoneCountTable(Plugin.FakePartyList);
            //this.Position.
        }

        private void ZoneCountTable(Dictionary<string, MPAMember> list) {
            Dictionary<string, int> zones = new();
            foreach(MPAMember player in list.Values.Where(p => p.MapLink != null)) {
                if(zones.ContainsKey(player.MapLink!.GetMapLinkPayload().PlaceName)) {
                    zones[player.MapLink.GetMapLinkPayload().PlaceName] += 1;
                } else {
                    zones.Add(player.MapLink.GetMapLinkPayload().PlaceName, 1);
                }
            }

            ////hide self on no links
            //if(zones.Count <= 0) {
            //    this.IsOpen = false;
            //}

            if(ImGui.BeginTable($"##{list.GetHashCode()}_Zone_Table", 2, ImGuiTableFlags.NoHostExtendX)) {
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
