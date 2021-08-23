namespace GlassTL.Telegram.Network.Connection
{
    public enum Codec
    {
        Unknown = -1,
        /// <summary>
        /// Default Telegram mode. Sends 12 additional bytes and
        /// needs to calculate the CRC value of the packet itself.
        /// </summary>
        FullPacketCodec,
        /// <summary>
        /// The lightest protocol available.
        /// - Overhead: Very small
        /// - Minimum envelope length: 1 byte
        /// - Maximum envelope length: 4 bytes
        ///
        /// Payload structure:
        /// 
        ///    +-+----...----+
        ///    |l|  payload  |
        ///    +-+----...----+
        ///    OR
        ///    +-+---+----...----+
        ///    |h|len|  payload  |
        ///    +-+---+----...----+
        ///
        /// Before sending anything into the underlying socket, the
        /// client must first send 0xef as the first byte (the
        /// server will not send 0xef as the first byte in the first
        /// reply).
        /// 
        /// Then, payloads are wrapped in the following envelope:
        /// 
        /// - Length: payload length, divided by four, and encoded
        ///   as a single byte, only if the resulting packet length
        ///   is a value between 0x01..0x7e.
        /// - Payload: the MTProto payload
        /// 
        /// If the packet length divided by four is bigger than or
        /// equal to 127 (>= 0x7f), the following envelope must be
        /// used, instead:
        /// 
        /// - Header: A single byte of value 0x7f
        /// - Length: payload length, divided by four, and encoded
        ///   as 3 length bytes (little endian)
        /// - Payload: the MTProto payload
        /// </summary>
        AbridgedPacketCodec
    }
}
