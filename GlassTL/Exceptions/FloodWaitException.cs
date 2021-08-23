namespace GlassTL.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Occurs when the server has flood waited requests for a period of time.
    /// </summary>
    [Serializable]
    public class FloodWaitException : Exception
    {
        /// <summary>
        /// The number of seconds to wait before resending the request
        /// </summary>
        public int FloodWaitSeconds { get; private set; }

        public FloodWaitException(int floodWaitSeconds) : base($"Flood waited for {floodWaitSeconds} seconds.")
        {
            FloodWaitSeconds = floodWaitSeconds;
        }

        public FloodWaitException(int floodWaitSeconds, Exception innerException) : base($"Flood waited for {floodWaitSeconds} seconds.", innerException)
        {
            FloodWaitSeconds = floodWaitSeconds;
        }

        public FloodWaitException() { }

        protected FloodWaitException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }

        public FloodWaitException(string message) : base(message) { }
        public FloodWaitException(string message, Exception innerException) : base(message, innerException) { }
    }
}