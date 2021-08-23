namespace GlassTL.Telegram.Network.Authentication
{
    public enum AuthenticationState
    {
        NotStarted,
        PqRequest,
        ServerDhRequest,
        ClientDhRequest
    }
}
