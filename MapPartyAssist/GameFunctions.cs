using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MapPartyAssist;

internal unsafe class GameFunctions {

    private readonly Plugin _plugin;
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F2 48 8B F9 45 84 C9")]
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, void> _sendChatMessage = null;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 0F B6 F0 E8 ?? ?? ?? ?? 48 8D 4D C0")]
    private readonly delegate* unmanaged<Utf8String*, int, IntPtr, void> _sanitizeString = null;

    internal GameFunctions(Plugin plugin) {
        _plugin = plugin;
        try {
            plugin.InteropProvider.InitializeFromAttributes(this);
        } catch(Exception ex) {
            plugin.Log.Error(ex, "Failed to find signatures.");
        }
    }

    internal void SendChatMessage(string message) {
        if(_sendChatMessage == null) {
            //throw new InvalidOperationException("Could not find signature for chat sending");
            _plugin.Log.Error("Unable to send chat message, function not found");
            return;
        }

        try {
            var bytes = Encoding.UTF8.GetBytes(SanitizeText(message));
            if(bytes.Length == 0) {
                throw new ArgumentException("The message is empty", nameof(message));
            }

            if(bytes.Length > 500) {
                throw new ArgumentException("The message is longer than 500 bytes", nameof(message));
            }

            var uiModule = (IntPtr)UIModule.Instance();
            using var payload = new ChatPayload(bytes);
            var mem1 = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem1, false);
            _sendChatMessage(uiModule, mem1, IntPtr.Zero, 0);

            Marshal.FreeHGlobal(mem1);
        } catch(Exception ex) {
            _plugin.Log.Error(ex, $"Failed to send message.");
        }
    }

    private unsafe string SanitizeText(string text) {
        if(_sanitizeString == null) {
            throw new InvalidOperationException("Could not find signature for chat sanitisation");
        }

        var uText = Utf8String.FromString(text);

        _sanitizeString(uText, 0x27F, IntPtr.Zero);
        var sanitised = uText->ToString();

        uText->Dtor();
        IMemorySpace.Free(uText);

        return sanitised;
    }

    internal void OpenMap(uint mapId) {
        //AgentMap* agent = AgentMap.Instance();
        //AgentMap.MemberFunctionPointers.OpenMapByMapId(agent, mapId);
        AgentMap.Instance()->OpenMapByMapId(mapId);
    }

    internal void SetFlagMarker(uint territoryId, uint mapId, float mapX, float mapY) {
        //AgentMap.MemberFunctionPointers.SetFlagMapMarker(AgentMap.Instance(), territoryId, mapId, mapX, mapY, 60561u);
        AgentMap.Instance()->IsFlagMarkerSet = 0;
        AgentMap.Instance()->SetFlagMapMarker(territoryId, mapId, mapX, mapY);
    }

    internal int GetCurrentDutyId() {
        if(GameMain.Instance() == null) {
            return 0;
        }
        return GameMain.Instance()->CurrentContentFinderConditionId;
    }

    internal InstanceContentType? GetInstanceContentType() {
        var instanceDirector = EventFramework.Instance()->GetInstanceContentDirector();
        return instanceDirector != null ? instanceDirector->InstanceContentType : null;
    }

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct ChatPayload : IDisposable {
        [FieldOffset(0)] private readonly IntPtr textPtr;
        [FieldOffset(16)] private readonly ulong textLen;
        [FieldOffset(8)] private readonly ulong unk1;
        [FieldOffset(24)] private readonly ulong unk2;

        internal ChatPayload(byte[] stringBytes) {
            textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
            Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
            Marshal.WriteByte(textPtr + stringBytes.Length, 0);

            textLen = (ulong)(stringBytes.Length + 1);

            unk1 = 64;
            unk2 = 0;
        }

        public void Dispose() {
            Marshal.FreeHGlobal(textPtr);
        }
    }
}
