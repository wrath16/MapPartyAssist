using System;

namespace MapPartyAssist.Types {
    public record class Message(DateTime Time, int Channel, string Text, uint? ItemId, bool? IsHq, string? PlayerKey);
}
