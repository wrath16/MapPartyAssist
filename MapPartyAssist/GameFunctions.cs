using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace MapPartyAssist;

internal unsafe class GameFunctions {
    //[Signature("BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 41 8B 4D 08", Offset = 1)]
    //private uint _agentId;

    //[Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 33 ED 48 8D 15")]
    //private readonly delegate* unmanaged<uint, uint, float, float, uint> _setFlagMapMarker;
    ////(uint territoryId, uint mapId, float mapX, float mapY, uint iconId = 0xEC91)

    //[Signature("E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B0 ?? 48 8B B4 24")]
    //private readonly delegate* unmanaged<uint> _openMapByMapId;
    ////(uint mapId)

    //[Signature("E8 ?? ?? ?? ?? 84 C0 0F 94 C0 EB 19")]
    //private readonly delegate* unmanaged<nint, nint> _setWaymark;
    //(uint mapId)

    //private static AtkUnitBase* AddonToDoList => GetUnitBase<AtkUnitBase>("_ToDoList");

    internal GameFunctions() {
        Dalamud.Utility.Signatures.SignatureHelper.Initialise(this);
    }

    internal void OpenMap(uint mapId) {
        //AgentMap* agent = AgentMap.Instance();
        //AgentMap.MemberFunctionPointers.OpenMapByMapId(agent, mapId);
        AgentMap.Instance()->OpenMapByMapId(mapId);
    }

    internal void SetFlagMarkers(uint territoryId, uint mapId, float mapX, float mapY) {
        //AgentMap.MemberFunctionPointers.SetFlagMapMarker(AgentMap.Instance(), territoryId, mapId, mapX, mapY, 60561u);
        AgentMap.Instance()->SetFlagMapMarker(territoryId, mapId, mapX, mapY);
    }

    internal int GetCurrentDutyId() {
        return GameMain.Instance()->CurrentContentFinderConditionId;
    }
}
