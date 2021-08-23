namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class AuthRestartException : Exception
    {
        public AuthRestartException(string message) : base(message) { }

        public AuthRestartException(string message, Exception innerException) : base(message, innerException) { }

        public AuthRestartException() { }

        protected AuthRestartException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}