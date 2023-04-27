using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

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
}
