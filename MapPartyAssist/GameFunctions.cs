using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;

namespace MapPartyAssist;

internal unsafe class GameFunctions {
    [Signature("B8 ?? ?? ?? ?? 44 0F B7 C2 4C 8B C9")]
    private readonly delegate* unmanaged<IntPtr, ushort, byte> _isTitleUnlocked;

    [Signature("E8 ?? ?? ?? ?? 83 7B 44 02")]
    private readonly delegate* unmanaged<AgentInterface*, uint*, byte> _setTitle;

    [Signature("E8 ?? ?? ?? ?? 89 6E 58")]
    private readonly delegate* unmanaged<IntPtr, void> _requestTitles;

    [Signature("48 8D 0D ?? ?? ?? ?? BD ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75", ScanType = ScanType.StaticAddress)]
    private readonly IntPtr _titleList;

    [Signature("BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 41 8B 4D 08", Offset = 1)]
    private uint _agentId;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 33 ED 48 8D 15")]
    private readonly delegate* unmanaged<uint, uint, float, float, uint> _setFlagMapMarker;
    //(uint territoryId, uint mapId, float mapX, float mapY, uint iconId = 0xEC91)

    [Signature("E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B0 ?? 48 8B B4 24")]
    private readonly delegate* unmanaged<uint> _openMapByMapId;
    //(uint mapId)

    //private static AtkUnitBase* AddonToDoList => GetUnitBase<AtkUnitBase>("_ToDoList");

    internal GameFunctions() {
        Dalamud.Utility.Signatures.SignatureHelper.Initialise(this);
    }

    internal void RequestTitles() {
        if(*(byte*)(this._titleList + 0x61) == 1) {
            return;
        }

        this._requestTitles(this._titleList);
    }

    internal bool IsTitleUnlocked(uint titleId) {
        if(titleId > ushort.MaxValue) {
            return false;
        }

        return this._isTitleUnlocked(this._titleList, (ushort)titleId) != 0;
    }

    internal bool SetTitle(uint titleId) {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId((AgentId)this._agentId);
        if(agent == null) {
            return false;
        }

        return this._setTitle(agent, &titleId) != 0;
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

    //internal int GetCurrentNonInstanceDutyId() {
    //    //return GameMain.Instance()->C
    //}

    internal string testfunc(nint ptr) {
        var toDoListBasePtr = (AtkUnitBase*)ptr;
        AtkComponentNode* x = (AtkComponentNode*)toDoListBasePtr->RootNode;
        AtkTextNode* y = (AtkTextNode*)(x->Component)->UldManager.RootNode;
        string z = Marshal.PtrToStringAnsi((new IntPtr(y->NodeText.StringPtr)));
        return z;
    }
}
