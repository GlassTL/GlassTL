using System;
using System.Runtime.Serialization;

namespace GlassTL.Telegram.Exceptions
{
    [Serializable]
    public class MissingApiConfigurationException : Exception, ISerializable
    {
        public const string InfoUrl = "https://github.com/sochix/Telegram#quick-configuration";

        public MissingApiConfigurationException(string message) : base($"Your {message} setting is missing. Adjust the configuration first, see {InfoUrl}") { }

        public MissingApiConfigurationException() { }

        public MissingApiConfigurationException(string message, Exception innerException) : base(message, innerException) { }

        protected MissingApiConfigurationException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}
