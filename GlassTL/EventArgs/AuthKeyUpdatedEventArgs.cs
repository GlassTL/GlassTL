namespace GlassTL.EventArgs
{
    using System;
    using Telegram.Network.Authentication;

    public class AuthKeyUpdatedEventArgs : EventArgs
    {
        public ServerAuthentication ServerAuthentication { get; }

        public AuthKeyUpdatedEventArgs(ServerAuthentication authentication)
        {
            ServerAuthentication = authentication;
        }
    }
}
