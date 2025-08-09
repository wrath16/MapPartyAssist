using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MapPartyAssist.Helper {
    internal static class ImGuiHelper {

        internal static void RightAlignCursor(string text) {
            var size = ImGui.CalcTextSize(text);
            var posX = ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X;
            if(posX > ImGui.GetCursorPosX()) {
                ImGui.SetCursorPosX(posX);
            }
        }

        internal static void RightAlignCursor2(string text, float extra) {
            var posX = ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X;
            if(posX > ImGui.GetCursorPosX()) {
                ImGui.SetCursorPosX(posX + extra);
            }
        }

        internal static void CenterAlignCursor(string text) {
            var size = ImGui.CalcTextSize(text);
            var posX = ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X) / 2f;
            ImGui.SetCursorPosX(posX);
        }

        internal static void HelpMarker(string text, bool sameLine = true, bool alignToFrame = false) {
            if(sameLine) ImGui.SameLine();
            if(alignToFrame) ImGui.AlignTextToFramePadding();

            ImGui.TextDisabled("(?)");
            WrappedTooltip(text, 500f);
        }

        internal static void HelpMarker(System.Action action, bool sameLine = true) {
            if(sameLine) ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            CustomTooltip(action);
        }

        internal static void CustomTooltip(System.Action action) {
            if(ImGui.IsItemHovered()) {
                using var tooltip = ImRaii.Tooltip();
                action.Invoke();
            }
        }

        internal static void WrappedTooltip(string text, float width = 400f) {
            if(ImGui.IsItemHovered()) {
                using var tooltip = ImRaii.Tooltip();
                ImGui.Text(WrappedString(text, width));
            }
        }

        internal static string WrappedString(string text, float width) {
            width *= ImGuiHelpers.GlobalScale;
            string[] splitStrings = text.Split(" ");
            string wrappedString = "";
            string currentLine = "";

            foreach(var word in splitStrings) {
                if(ImGui.CalcTextSize($"{currentLine} {word}").X > width) {
                    if(wrappedString == "") {
                        wrappedString = word;
                    } else {
                        wrappedString += $"\n{word}";
                    }
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
            return wrappedString;
        }

        internal static string WrappedString(string text, uint lines) {
            var size = ImGui.CalcTextSize(text).X;
            var sizePerLine = size / lines * 1.4; //add a margin of error
            string[] splitStrings = text.Split(" ");
            string wrappedString = "";
            string currentLine = "";
            int lineIndex = 0;

            foreach(var word in splitStrings) {
                if(ImGui.CalcTextSize($"{currentLine} {word}").X > sizePerLine && currentLine != "" && lineIndex + 1 < lines) {
                    if(wrappedString == "") {
                        wrappedString = word;
                    } else {
                        wrappedString += $"\n{word}";
                        lineIndex++;
                    }
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
            return wrappedString;
        }

        internal static void DonateButton() {
            using(_ = ImRaii.PushFont(UiBuilder.IconFont)) {
                var text = $"{FontAwesomeIcon.Star.ToIconString()}{FontAwesomeIcon.Copy.ToIconString()}";
                using(_ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed)) {
                    if(ImGui.Button($"{FontAwesomeIcon.Heart.ToIconString()}##--Donate")) {
                        Task.Run(() => {
                            Process.Start(new ProcessStartInfo() {
                                UseShellExecute = true,
                                FileName = "https://ko-fi.com/samoxiv"
                            });
                        });
                    }
                }
            }
            WrappedTooltip("Support the dev");
        }
    }
}
