using Dalamud.Interface.Utility;
using ImGuiNET;

namespace MapPartyAssist.Helper {
    internal static class ImGuiHelper {

        internal static void RightAlignCursor(string text) {
            var size = ImGui.CalcTextSize(text);
            var posX = ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X;
            if(posX > ImGui.GetCursorPosX()) {
                ImGui.SetCursorPosX(posX);
            }
        }

        internal static void CenterAlignCursor(string text) {
            var size = ImGui.CalcTextSize(text);
            var posX = ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X) / 2f;
            ImGui.SetCursorPosX(posX);
        }

        internal static void HelpMarker(string text) {
            ImGui.TextDisabled("(?)");
            if(ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text(text);
                ImGui.EndTooltip();
            }
        }

        internal static void WrappedTooltip(string text, float width = 400f) {
            width *= ImGuiHelpers.GlobalScale;
            string[] splitStrings = text.Split(" ");
            string wrappedString = "";
            string currentLine = "";

            foreach(var word in splitStrings) {
                if(ImGui.CalcTextSize($"{currentLine} {word}").X > width) {
                    wrappedString += $"\n{word}";
                    currentLine = word;
                } else {
                    if(currentLine == "") {
                        wrappedString += $"{word}";
                        currentLine += $"{word}";
                    } else {
                        wrappedString += $" {word}";
                        currentLine += $" {word}";
                    }
                }
            }

            if(ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text(wrappedString);
                ImGui.EndTooltip();
            }
        }
    }
}
