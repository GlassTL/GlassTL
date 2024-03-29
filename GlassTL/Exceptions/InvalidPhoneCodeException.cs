﻿namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when connection to Telegram Servers fails.  See InnerException for more details
    /// </summary>
    [Serializable]
    public class InvalidPhoneCodeException : Exception
    {
        public InvalidPhoneCodeException(string message) : base(message) { }

        public InvalidPhoneCodeException(string message, Exception innerException) : base(message, innerException) { }

        public InvalidPhoneCodeException() { }

        protected InvalidPhoneCodeException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}