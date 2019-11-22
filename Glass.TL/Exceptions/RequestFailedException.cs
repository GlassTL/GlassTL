using System;
using System.Runtime.Serialization;
using GlassTL.Telegram.MTProto;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when an sending a request to Telegram fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    class RequestFailedException : Exception, ISerializable
    {
        public TLObject Request { get; }

        public RequestFailedException(string message) : base(message) { }
        public RequestFailedException(string message, Exception innerException) : base(message, innerException) { }

        public RequestFailedException(string message, TLObject Request) : base(message)
        {
            this.Request = Request;
        }
        public RequestFailedException(string message, Exception innerException, TLObject Request) : base(message, innerException)
        {
            this.Request = Request;
        }

        public RequestFailedException() { }

        protected RequestFailedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}