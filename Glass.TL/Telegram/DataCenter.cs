
using System.IO;

namespace GlassTL.Telegram
{
    public class DataCenter
    {
        public DataCenter(string address, int port, bool testDC, int dcId)
        {
            Address = address;
            Port = port;
            TestDC = testDC;
            DataCenterId = dcId;
        }

        public string Address { get; private set; }
        public int Port { get; private set; }
        public bool TestDC { get; private set; }
        public int DataCenterId { get; private set; }

        public byte[] Serialize()
        {
            using var memory = new MemoryStream(4 + 4 + Address.Length + 4);
            using var writer = new BinaryWriter(memory);

            Utils.StringUtil.Serialize(Address, writer);
            Utils.IntegerUtil.Serialize(Port, writer);
            Utils.BoolUtil.Serialize(TestDC, writer);
            Utils.IntegerUtil.Serialize(DataCenterId, writer);

            return memory.ToArray();
        }

        public static DataCenter Deserialize(byte[] raw)
        {
            using var memory = new MemoryStream(raw);
            using var reader = new BinaryReader(memory);

            return Deserialize(reader);
        }
        public static DataCenter Deserialize(BinaryReader reader)
        {
            return new DataCenter(
                Utils.StringUtil.Deserialize(reader),
                Utils.IntegerUtil.Deserialize(reader),
                Utils.BoolUtil.Deserialize(reader),
                Utils.IntegerUtil.Deserialize(reader)
            );
        }
    }
}
