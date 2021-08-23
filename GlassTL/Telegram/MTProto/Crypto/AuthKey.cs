namespace GlassTL.Telegram.MTProto.Crypto
{
    using System;
    using System.Security.Cryptography;

    public class AuthKey
    {
        private readonly byte[] _key;

        public AuthKey(BigInteger gab) : this(gab.ToByteArrayUnsigned()) { }

        public AuthKey(byte[] data)
        {
            _key = data;

            using var sha1 = new SHA1Managed();
            var hash = sha1.ComputeHash(data);

            AuxHash = BitConverter.ToUInt64(hash, 0);
            KeyId   = BitConverter.ToUInt64(hash, 12);
        }

        public byte[] CalcNewNonceHash(byte[] newNonce, int number)
        {
            using var sha = SHA1.Create();

            var nonce = new byte[newNonce.Length + 1 + 8];
            Buffer.BlockCopy(newNonce, 0, nonce, 0, newNonce.Length);
            nonce[newNonce.Length] = (byte)number;
            Buffer.BlockCopy(BitConverter.GetBytes(AuxHash), 0, nonce, newNonce.Length + 1, 8);

            var hash = new byte[16];
            Buffer.BlockCopy(sha.ComputeHash(nonce), 4, hash, 0, 16);

            return hash;
        }

        public byte[] GetKey() => (byte[]) _key.Clone();

        public ulong KeyId { get; }
        private ulong AuxHash { get; }

        public override string ToString() => $"(Key: {_key}, KeyId: {KeyId}, AuxHash: {AuxHash})";
    }
}
