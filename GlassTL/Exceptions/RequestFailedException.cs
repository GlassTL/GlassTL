namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;
    using Telegram.MTProto;

    /// <summary>
    /// Occurs when sending a request to Telegram fails.  The InnerException my shed some more light on the matter.
    /// </summary>
    [Serializable]
    public class RequestFailedException : Exception
    {
        public TLObject Request { get; }

        public RequestFailedException(string message) : base(message) { }
        public RequestFailedException(string message, Exception innerException) : base(message, innerException) { }

        public RequestFailedException(string message, TLObject request) : base(message)
        {
            Request = request;
        }
        public RequestFailedException(string message, Exception innerException, TLObject request) : base(message, innerException)
        {
            Request = request;
        }

        public RequestFailedException() { }

        protected RequestFailedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}