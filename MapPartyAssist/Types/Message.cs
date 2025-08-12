using System;

namespace MapPartyAssist.Types {
    public class Message {

        public DateTime Time { get; set; }
        public int Channel { get; set; }
        public string Text { get; set; }
        public uint? ItemId { get; set; }
        public bool? IsHq { get; set; }
        public string? PlayerKey { get; set; }

        public Message(DateTime time, int channel, string text, uint? itemId = null, bool? isHq = null, string? playerKey = null) {
            Time = time;
            Channel = channel;
            Text = text;
            ItemId = itemId;
            IsHq = isHq;
            PlayerKey = playerKey;
        }
    }
}
