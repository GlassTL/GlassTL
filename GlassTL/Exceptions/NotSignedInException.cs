namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when your connection to Telegram servers is not authenticated.  The InnerException my shed some more light on the matter.
    /// </summary>
    [Serializable]
    public class NotSignedInException : Exception
    {
        public NotSignedInException(string message) : base(message) { }

        public NotSignedInException(string message, Exception innerException) : base(message, innerException) { }

        public NotSignedInException() { }

        protected NotSignedInException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}