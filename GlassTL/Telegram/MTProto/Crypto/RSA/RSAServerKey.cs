namespace GlassTL.Telegram.MTProto.Crypto.RSA
{
    using System;
    using System.Security.Cryptography;
    using Utils;

    public class RsaEncrypter
    {
        private RsaEncrypter(long fingerprint, BigInteger modulus, BigInteger exponent)
        {
            Fingerprint = fingerprint;
            Modulus = modulus;
            Exponent = exponent;
        }

        private RsaEncrypter(long fingerprint, RSAParameters parameters) : this(fingerprint, new BigInteger(1, parameters.Modulus), new BigInteger(1, parameters.Exponent))
        { }
        public RsaEncrypter(long fingerprint, RSACryptoServiceProvider provider) : this(fingerprint, provider.ExportParameters(false))
        { }

        public long Fingerprint { get; }
        private BigInteger Modulus { get; }
        private BigInteger Exponent { get; }

        public byte[] Encrypt(byte[] data) => Encrypt(data, 0, data.Length);

        private byte[] Encrypt(byte[] data, int offset, int length)
        {
            var plaintextPaddingSize = length < 235 ? 235 - length : 0;
            var plaintextBytes = new byte[20 + data.Length + plaintextPaddingSize];

            using (var sha1 = new SHA1Managed()) Array.Copy(sha1.ComputeHash(data, offset, length), 0, plaintextBytes, 0, 20);
            Array.Copy(data, 0, plaintextBytes, 20, data.Length);
            Array.Copy(Helpers.GenerateRandomBytes(plaintextPaddingSize), 0, plaintextBytes, 20 + data.Length, plaintextPaddingSize);

            var ciphertextBytes = new BigInteger(1, plaintextBytes).ModPow(Exponent, Modulus).ToByteArrayUnsigned();

            if (ciphertextBytes.Length == 256) return ciphertextBytes;
            
            var paddedCiphertext = new byte[256];
            Array.Copy(ciphertextBytes, 0, paddedCiphertext, 256 - ciphertextBytes.Length, ciphertextBytes.Length);
            return paddedCiphertext;
        }
    }
}
