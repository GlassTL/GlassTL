using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class CloudPasswordNeededException : Exception, ISerializable
    {
        public CloudPasswordNeededException(string message) : base(message) { }

        public CloudPasswordNeededException(string message, Exception innerException) : base(message, innerException) { }

        public CloudPasswordNeededException() { }

        protected CloudPasswordNeededException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}