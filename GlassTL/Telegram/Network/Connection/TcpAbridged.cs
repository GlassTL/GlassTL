namespace GlassTL.Telegram.Network.Connection
{
    using System;
    using System.Threading.Tasks;

    public class TcpAbridged : SocketConnection
    {
        
        public override Codec PacketCodec => Codec.AbridgedPacketCodec;
        
        public TcpAbridged(DataCenter dc) : base(dc.Address, dc.Port)
        {
            Logger.Log(Logger.Level.Debug, "TCP Abridged connection initialized");
        }

        protected override async Task InitConnection(TcpClient client)
        {
            await client.Send(new byte[] { 0xEF });
        }

        protected override byte[] SerializePacket(byte[] packet)
        {
            Logger.Log("Attempting to serialize packet using TcpAbridged.  This is untested!");
            
            // Length to be used as part of the envelope 
            var packetLength = packet.Length / 4;
            
            // Ensure the packet length is valid.
            if (packetLength % 4 == 0 || packetLength > 1 << 24) throw new ArgumentException($"Packet length is invalid.", nameof(packet));

            // Based on the specs, create the header/envelope.
            var envelope = packetLength < 0x7F ? new[] { (byte) packetLength } : BitConverter.GetBytes((uint)(packetLength << 8 | 0x7F));

            // Create the enveloped packet
            var encodedPacket = new byte[envelope.Length + packetLength];
            Buffer.BlockCopy(envelope, 0, encodedPacket, 0, envelope.Length);
            Buffer.BlockCopy(packet,   0, encodedPacket, envelope.Length, packet.Length);
            
            return encodedPacket;
        }

        protected override byte[] DeserializePacket(byte[] packet)
        {
            
            
            
            throw new System.NotImplementedException();
        }

    }
}