namespace GlassTL.EventArgs
{
    using System;
    using Telegram.MTProto;

    public class TLObjectEventArgs : EventArgs
    {
        public TLObject TLObject { get; }

        public TLObjectEventArgs(TLObject tlObject)
        {
            TLObject = tlObject;
        }
    }
}
