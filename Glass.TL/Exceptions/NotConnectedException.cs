
using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class NotConnectedException : Exception, ISerializable
    {
        public NotConnectedException(string message) : base(message) { }

        public NotConnectedException(string message, Exception innerException) : base(message, innerException) { }

        public NotConnectedException() { }

        protected NotConnectedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}