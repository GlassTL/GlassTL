using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class SrpIDInvalidException : Exception, ISerializable
    {
        public SrpIDInvalidException(string message) : base(message) { }

        public SrpIDInvalidException(string message, Exception innerException) : base(message, innerException) { }

        public SrpIDInvalidException() { }

        protected SrpIDInvalidException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}