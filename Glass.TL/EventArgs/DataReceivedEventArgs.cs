using System;
using System.Diagnostics.CodeAnalysis;

namespace GlassTL.Telegram
{
    public class DataReceivedEventArgs : EventArgs
    {
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "It's fine...")]
        public byte[] Data { get; } = null;

        public DataReceivedEventArgs(byte[] data)
        {
            Data = data;
        }
    }
}
