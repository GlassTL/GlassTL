namespace GlassTL.EventArgs
{
    using System;

    public class DataReceivedEventArgs : EventArgs
    {
        private readonly byte[] _data;

        public byte[] GetData() => (byte[]) _data.Clone();

        public DataReceivedEventArgs(byte[] data)
        {
            _data = data;
        }
    }
}
