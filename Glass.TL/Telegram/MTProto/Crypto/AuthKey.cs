using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GlassTL.Telegram.MTProto.Crypto
{
    public class AuthKey
    {
        public AuthKey(BigInteger gab) : this(gab.ToByteArrayUnsigned()) { }

        public AuthKey(byte[] data)
        {
            Key = data;

            using var hash = new SHA1Managed();
            using var hashStream = new MemoryStream(hash.ComputeHash(Key), false);
            using var hashReader = new BinaryReader(hashStream);

            AuxHash = hashReader.ReadUInt64();
            hashReader.ReadBytes(4);
            KeyID = hashReader.ReadUInt64();
        }

        public byte[] CalcNewNonceHash(byte[] newNonce, int number)
        {
            using var sha = SHA1.Create();

            return sha.ComputeHash(
                newNonce
                .Concat(new byte[] { (byte)number })
                .Concat(BitConverter.GetBytes(AuxHash))
                .ToArray()
                ).Skip(4).Take(16).ToArray();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "<Pending>")]
        public byte[] Key { get; }

        public ulong KeyID { get; }
        public ulong AuxHash { get; set; }

        public override string ToString()
        {
            return string.Format("(Key: {0}, KeyId: {1}, AuxHash: {2})", Key, KeyID, AuxHash);
        }
    }
}
