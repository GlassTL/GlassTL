namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when attempting to sign in to an account that doesn't exist and you need to sign up with the number instead.
    /// </summary>
    [Serializable]
    public class PhoneNumberUnoccupiedException : Exception
    {
        public PhoneNumberUnoccupiedException(string message) : base(message) { }

        public PhoneNumberUnoccupiedException(string message, Exception innerException) : base(message, innerException) { }

        public PhoneNumberUnoccupiedException() { }

        protected PhoneNumberUnoccupiedException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}