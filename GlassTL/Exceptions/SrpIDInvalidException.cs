namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Typically occurs when dealing with a cloud password and the SRP ID expired.  This SHOULD be handled by GlassTL transparently.
    /// </summary>
    [Serializable]
    public class SrpIdInvalidException : Exception
    {
        public SrpIdInvalidException(string message) : base(message) { }

        public SrpIdInvalidException(string message, Exception innerException) : base(message, innerException) { }

        public SrpIdInvalidException() { }

        protected SrpIdInvalidException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}