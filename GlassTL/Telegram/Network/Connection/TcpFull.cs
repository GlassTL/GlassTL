namespace GlassTL.Telegram.Network.Connection
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using MTProto.Crypto;

    public class TcpFull : SocketConnection
    {
        public override Codec PacketCodec => Codec.FullPacketCodec;

        private int SequenceNumber { get; set; }

        public TcpFull(DataCenter dc) : base(dc.Address, dc.Port)
        {
            Logger.Log(Logger.Level.Debug, "TCP Full connection initialized");
        }

        protected override async Task InitConnection(TcpClient client)
        {
            // TcpFull connection does not require initialization.
            await Task.CompletedTask;
        }

        protected override byte[] DeserializePacket(byte[] packet)
        {
            try
            {
                if (packet == null)
                {
                    Logger.Log(Logger.Level.Error, "Null packet received.  Skipping.");
                    return null;
                }

                if (packet.Length < 12)
                {
                    Logger.Log(Logger.Level.Error, $"TCPFull packets should at least be 12 bytes, but this was {packet.Length}.  Skipping.");
                    return null;
                }

                Logger.Log(Logger.Level.Info, "Attempting to deserialize packet");
                Logger.Log(Logger.Level.Debug, $"Packet Length: {packet.Length}");

                using var memoryStream = new MemoryStream(packet);
                using var binaryReader = new BinaryReader(memoryStream);

                var packetLength = binaryReader.ReadInt32();
                var seq          = binaryReader.ReadInt32();
                var body         = binaryReader.ReadBytes(packetLength - 12);
                var checksum     = binaryReader.ReadInt32();

                Logger.Log(Logger.Level.Debug, $"\tSequence: {seq}");
                Logger.Log(Logger.Level.Debug, $"\tBody Length: {body.Length}");

                if (checksum == Crc32.Compute(packet, 0, packetLength - 4)) return body;
                
                Logger.Log(Logger.Level.Error, "Packet checksum could not be validated.  Skipping.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while deserializing the packet.  Skipping.\n\n{ex.Message}");
                return null;
            }
        }

        protected override byte[] SerializePacket(byte[] packet)
        {
            /*
             * https://core.telegram.org/mtproto#tcp-transport
             * 4 length bytes are added at the front 
             * (to include the length, the sequence number, and CRC32; always divisible by 4)
             * and 4 bytes with the packet sequence number within this TCP connection 
             * (the first packet sent is numbered 0, the next one 1, etc.),
             * and 4 CRC32 bytes at the end (length, sequence number, and payload together).
             */

            // https://core.telegram.org/mtproto#tcp-transport
            //total length, sequence number, packet and checksum (CRC32)

            try
            {
                using var memoryStream = new MemoryStream();
                using var binaryWriter = new BinaryWriter(memoryStream);

                binaryWriter.Write(packet.Length + 12);
                binaryWriter.Write(SequenceNumber++);
                binaryWriter.Write(packet);
                binaryWriter.Write(Crc32.Compute(memoryStream.GetBuffer(), 0, packet.Length + 8));

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to serialize TCPFull packet.", ex);
            }
        }
    }
}
