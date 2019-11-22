using System;
using GlassTL.Telegram.MTProto;

namespace GlassTL.Telegram
{
    public class TLObjectEventArgs : EventArgs
    {
        public TLObject TLObject { get; } = null;

        public TLObjectEventArgs(TLObject TLObject)
        {
            this.TLObject = TLObject;
        }
    }
}
