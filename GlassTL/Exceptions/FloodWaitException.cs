using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when the server has flood waited requests for a period of time.
    /// </summary>
    [Serializable]
    public class FloodWaitException : Exception, ISerializable
    {
        public int FloodWaitSeconds { get; private set; }
        public FloodWaitException(int FloodWaitSeconds) : base($"Flood waited for {FloodWaitSeconds} seconds.")
        {
            this.FloodWaitSeconds = FloodWaitSeconds;
        }

        public FloodWaitException(int FloodWaitSeconds, Exception innerException) : base($"Flood waited for {FloodWaitSeconds} seconds.", innerException)
        {
            this.FloodWaitSeconds = FloodWaitSeconds;
        }

        public FloodWaitException() { }

        protected FloodWaitException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }

        public FloodWaitException(string message) : base(message) { }
        public FloodWaitException(string message, Exception innerException) : base(message, innerException) { }
    }
}