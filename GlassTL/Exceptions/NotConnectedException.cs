namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when the connection to Telegram servers fails.  The InnerException my shed some more light on the matter.
    /// </summary>
    [Serializable]
    public class NotConnectedException : Exception
    {
        public NotConnectedException(string message) : base(message) { }

        public NotConnectedException(string message, Exception innerException) : base(message, innerException) { }

        public NotConnectedException() { }

        protected NotConnectedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}