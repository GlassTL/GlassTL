using System;
using System.IO;
using GlassTL.Telegram.MTProto.Crypto;

namespace GlassTL.Telegram.Network
{
    public class TCPFull : Connection
    {
        public override Codec PacketCodec => Codec.FullPacketCodec;

        public int SequenceNumber { get; private set; } = 0;

        public TCPFull(DataCenter DC) : base(DC.Address, DC.Port)
        {
            Logger.Log(Logger.Level.Debug, "TCP Full connection initialized");
        }
        
        public override byte[] DeserializePacket(byte[] Packet)
        {
            try
            {
                if (Packet == null)
                {
                    Logger.Log(Logger.Level.Error, "Null packet received.  Skipping.");
                    return null;
                }

                if (Packet.Length < 12)
                {
                    Logger.Log(Logger.Level.Error, $"TCPFull packets should at least be 12 bytes, but this was {Packet.Length}.  Skipping.");
                    return null;
                }

                Logger.Log(Logger.Level.Info, "Attempting to deserialize packet");
                Logger.Log(Logger.Level.Debug, $"Packet Length: {Packet.Length}");

                using (var memoryStream = new MemoryStream(Packet))
                using (var binaryReader = new BinaryReader(memoryStream))
                {
                    var packetLength = binaryReader.ReadInt32();
                    var seq = binaryReader.ReadInt32();
                    var body = binaryReader.ReadBytes(packetLength - 12);
                    var checksum = binaryReader.ReadInt32();

                    Logger.Log(Logger.Level.Debug, $"\tSequence: {seq}");
                    Logger.Log(Logger.Level.Debug, $"\tBody Length: {body.Length}");

                    if (checksum != Crc32.Compute(Packet, 0, packetLength - 4))
                    {
                        Logger.Log(Logger.Level.Error, "Packet checksum could not be validated.  Skipping.");
                        return null;
                    }

                    return body;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while deserializing the packet.  Skipping.\n\n{ex.Message}");
                return null;
            }
        }

        public override byte[] SerializePacket(byte[] Packet)
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
                using (var memoryStream = new MemoryStream())
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(Packet.Length + 12);
                    binaryWriter.Write(SequenceNumber++);
                    binaryWriter.Write(Packet);
                    binaryWriter.Write(Crc32.Compute(memoryStream.GetBuffer(), 0, Packet.Length + 8));

                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to serialize TCPFull packet.", ex);
            }
        }
    }
}
