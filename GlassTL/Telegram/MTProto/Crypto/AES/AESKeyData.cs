namespace GlassTL.Telegram.MTProto.Crypto.AES
{
    using System;
    using Utils;

    public readonly struct AesKeyData
    {
        public AesKeyData(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }

        private readonly byte[] _key;
        private readonly byte[] _iv;

        public byte[] GetKey() => _key;
        public byte[] GetIv() => _iv;

        public override bool Equals(object obj)
        {
            if (obj is not AesKeyData keyData) return false;
            return keyData._key.DirectSequenceEquals(_key) && keyData._iv.DirectSequenceEquals(_iv);
        }

        public static bool operator ==(AesKeyData left, AesKeyData right) => left.Equals(right);
        public static bool operator !=(AesKeyData left, AesKeyData right) => !(left == right);

        public override int GetHashCode() => throw new NotImplementedException();
    }
}
