namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class ConnectionFailedException : Exception
    {
        public ConnectionFailedException(string message) : base(message) { }

        public ConnectionFailedException(string message, Exception innerException) : base(message, innerException) { }

        public ConnectionFailedException() { }

        protected ConnectionFailedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}
