using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class NotSignedInException : Exception, ISerializable
    {
        public NotSignedInException(string message) : base(message) { }

        public NotSignedInException(string message, Exception innerException) : base(message, innerException) { }

        public NotSignedInException() { }

        protected NotSignedInException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}