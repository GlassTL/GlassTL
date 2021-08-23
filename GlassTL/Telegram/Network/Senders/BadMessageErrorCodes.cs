namespace GlassTL.Telegram.Network.Senders
{
    public enum BadMessageErrorCodes
    {
        MessageIdTooLow        = 0x10,
        MessageIdTooHigh       = 0x11,
        MessageIdInvalid       = 0x12,
        DuplicateMessageId     = 0x13,
        MessageIdTooOld        = 0x14,
        MessageSequenceTooLow  = 0x20,
        MessageSequenceTooHigh = 0x21,
        MessageIdNotEven       = 0x22,
        MessageIdNotOdd        = 0x23,
        BadServerSalt          = 0x30,
        InvalidContainer       = 0x40,
    }
}