namespace GlassTL.Telegram.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    // message ID layout
    // |-------31--------|---17---|1|--2-|
    // |server_message_id|local_id|0|type|

    // scheduled message ID layout
    // |-------30-------|----18---|1|--2-|
    // |send_date-2**30 |server_id|1|type|

    public class MessageID
    {
        private const int SERVER_ID_SHIFT = 20;
        private const int SHORT_TYPE_MASK = (1 << 2) - 1;
        private const int TYPE_MASK = (1 << 3) - 1;
        private const int FULL_TYPE_MASK = (1 << SERVER_ID_SHIFT) - 1;
        private const int SCHEDULED_MASK = 4;
        private const int TYPE_UNSENT = 1;
        private const int TYPE_LOCAL = 2;

        public long ID { get; internal set; }

        public static MessageID MinValue() => new MessageID(TYPE_UNSENT);
        public static MessageID MaxValue() => new MessageID((long)int.MaxValue << SERVER_ID_SHIFT);

        public MessageID(long id) => ID = id;
        public MessageID(ServerMessageID server_message_id) => ID = server_message_id.ID << SERVER_ID_SHIFT;
        public MessageID(ScheduledServerMessageID server_message_id, int send_date, bool force = false)
        {
            if (send_date <= (1 << 30))
            {
                Logger.Log(Logger.Level.Error, $"Scheduled message send date {send_date} is in the past");
                return;
            }

            if (!server_message_id.IsValid() && !force)
            {
                Logger.Log(Logger.Level.Error, $"Scheduled message ID {server_message_id.ID} is invalid");
                return;
            }

            ID = ((long)(send_date - (1 << 30)) << 21) | (server_message_id.ID << 3) | SCHEDULED_MASK;
        }

        public bool IsScheduled() => (ID & SCHEDULED_MASK) != 0;
        public bool IsUnsent() => (ID & SHORT_TYPE_MASK) == TYPE_UNSENT;
        public bool IsLocal() => (ID & SHORT_TYPE_MASK) == TYPE_LOCAL;

        public bool IsValid()
        {
            if (ID <= 0 || ID > MaxValue().ID) return false;
            if ((ID & FULL_TYPE_MASK) == 0) return true;

            var type = (int)(ID & TYPE_MASK);
            return type == TYPE_UNSENT || type == TYPE_LOCAL;

        }



    }
}
