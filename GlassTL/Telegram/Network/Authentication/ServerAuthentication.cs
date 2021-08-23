namespace GlassTL.Telegram.Network.Authentication
{
    using MTProto.Crypto;

    public class ServerAuthentication
    {
        public AuthKey AuthKey { get; set; }
        public int TimeOffset { get; set; }
    }
}
