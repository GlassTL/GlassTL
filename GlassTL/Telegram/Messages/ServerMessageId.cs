namespace GlassTL.Telegram.Messages
{
    public class ServerMessageID
    {
        public int ID { get; internal set; } = 0;

        public ServerMessageID(int id) => ID = id;

        public bool IsValid() => ID > 0;
    }
}
