using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;

namespace MapPartyAssist;

internal unsafe class GameFunctions {

    private readonly Plugin _plugin;
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F2 48 8B F9 45 84 C9")]
    private readonly delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, void> _sendChatMessage = null;

    internal GameFunctions(Plugin plugin) {
        _plugin = plugin;
        try {
            plugin.InteropProvider.InitializeFromAttributes(this);
        } catch(Exception ex) {
            plugin.Log.Error(ex, "Failed to find signatures.");
        }
    }

    internal void SendChatMessage(string message) {
        try {
            const AllowedEntities combinedEntities =
                AllowedEntities.UppercaseLetters |
                AllowedEntities.LowercaseLetters |
                AllowedEntities.Numbers |
                AllowedEntities.SpecialCharacters |
                AllowedEntities.CharacterList |
                AllowedEntities.OtherCharacters |
                AllowedEntities.Payloads |
                AllowedEntities.Unknown8 |
                AllowedEntities.Unknown9;
            var x = new Utf8String(message);
            x.SanitizeString(combinedEntities);
            UIModule.Instance()->ProcessChatBoxEntry(&x);
        } catch(Exception ex) {
            _plugin.Log.Error(ex, $"Failed to send message.");
        }
    }

    internal void OpenMap(uint mapId) {
        //AgentMap* agent = AgentMap.Instance();
        //AgentMap.MemberFunctionPointers.OpenMapByMapId(agent, mapId);
        AgentMap.Instance()->OpenMapByMapId(mapId);
    }

    internal void SetFlagMarker(uint territoryId, uint mapId, float mapX, float mapY) {
        //AgentMap.MemberFunctionPointers.SetFlagMapMarker(AgentMap.Instance(), territoryId, mapId, mapX, mapY, 60561u);
        //AgentMap.Instance()->FlagMapMarkers.Clear();
        AgentMap.Instance()->FlagMarkerCount = 0;
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
}
