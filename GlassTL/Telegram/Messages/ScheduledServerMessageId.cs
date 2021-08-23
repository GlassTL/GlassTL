namespace GlassTL.Telegram.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class ScheduledServerMessageID
    {
        public int ID { get; internal set; } = 0;

        public ScheduledServerMessageID(int id) => ID = id;

        public bool IsValid() => ID > 0 && ID < (1 << 18);
    }
}
