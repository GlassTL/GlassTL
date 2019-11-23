using System;
using GlassTL.Telegram.Network;

namespace GlassTL.Telegram
{
    public class AuthKeyUpdatedEventArgs : EventArgs
    {
        public ServerAuthentication ServerAuthentication { get; }

        public AuthKeyUpdatedEventArgs(ServerAuthentication authentication)
        {
            ServerAuthentication = authentication;
        }
    }
}
