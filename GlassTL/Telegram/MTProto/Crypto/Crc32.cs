namespace GlassTL.Telegram.MTProto.Crypto
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;

    /// <summary>
    /// Implements a 32-bit CRC hash algorithm
    /// </summary>
    /// <remarks>
    /// Used as a checksum for messages and in calculating the IDs of TLObjects
    /// 
    /// msgs_ack#62d6b459 msg_ids:Vector<long> = MsgsAck;
    /// Crc32("msgs_ack msg_ids:Vector long = MsgsAck") = 62d6b459
    /// </remarks>
    public sealed class Crc32 : HashAlgorithm
    {
        private const uint DefaultPolynomial = 0xedb88320u;
        private const uint DefaultSeed = 0xffffffffu;

        private static uint[] _defaultTable;

        private readonly uint _seed;
        private readonly uint[] _table;
        private uint _hash;

        public Crc32() : this(DefaultPolynomial, DefaultSeed) { }

        private Crc32(uint polynomial, uint seed)
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Not supported on Big Endian processors");

            _table = InitializeTable(polynomial);
            _seed = _hash = seed;
        }

        public static int Compute(byte[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < index) throw new ArgumentOutOfRangeException(nameof(index));
            if (buffer.Length - index < count) throw new ArgumentOutOfRangeException(nameof(count));

            var raw = new byte[count];
            Buffer.BlockCopy(buffer, index, raw, 0, count);

            return (int)Compute(raw);
        }

        public override void Initialize() => _hash = _seed;

        protected override void HashCore(byte[] array, int ibStart, int cbSize) => _hash = CalculateHash(_table, _hash, array, ibStart, cbSize);

        protected override byte[] HashFinal()
        {
            var hashBuffer = UintToBigEndianBytes(~_hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize => 32;

        private static uint Compute(byte[] buffer) => Compute(DefaultSeed, buffer);
        private static uint Compute(uint seed, byte[] buffer) => Compute(DefaultPolynomial, seed, buffer);
        private static uint Compute(uint polynomial, uint seed, byte[] buffer) => ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && _defaultTable != null) return _defaultTable;

            var createTable = new uint[256];

            for (var i = 0; i < 256; i++)
            {
                var entry = (uint)i;

                for (var j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                    {
                        entry = (entry >> 1) ^ polynomial;
                    }
                    else
                    {
                        entry >>= 1;
                    }
                }

                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial) _defaultTable = createTable;

            return createTable;
        }

        private static uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
        {
            var hash = seed;

            for (var i = start; i < start + size; i++)
            {
                hash = (hash >> 8) ^ table[buffer[i] ^ hash & 0xff];
            }

            return hash;
        }

        private static byte[] UintToBigEndianBytes(uint value)
        {
            var result = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian) Array.Reverse(result);

            return result;
        }
    }
}