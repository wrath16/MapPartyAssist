using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace MapPartyAssist;

internal unsafe class GameFunctions {

    internal GameFunctions() {
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
