using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    /// <summary>
    /// Occurs when attempting to sign in to an account that doesn't exist.
    /// </summary>
    [Serializable]
    public class PhoneNumberUnoccupiedException : Exception, ISerializable
    {
        public PhoneNumberUnoccupiedException(string message) : base(message) { }

        public PhoneNumberUnoccupiedException(string message, Exception innerException) : base(message, innerException) { }

        public PhoneNumberUnoccupiedException() { }

        protected PhoneNumberUnoccupiedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}