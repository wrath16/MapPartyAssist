using Dalamud.Game.Text;
using System;

namespace MapPartyAssist.Types {
    public class MPAMessage {

        public DateTime Time { get; set; }
        public XivChatType Channel { get; set; }
        public string Text { get; set; }
        public uint? ItemId { get; set; }
        public bool? IsHq { get; set; }
        public MPAMapLink? MapLink { get; set; }
        public string? PlayerKey { get; set; }

        public MPAMessage(DateTime time, XivChatType channel, string text, uint? itemId = null, bool? isHq = null, MPAMapLink? mapLink = null, string? playerKey = null) {
            Time = time;
            Channel = channel;
            Text = text;
            ItemId = itemId;
            IsHq = isHq;
            MapLink = mapLink;
            PlayerKey = playerKey;
        }
    }
}
