using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GlassTL.Telegram.MTProto.Crypto
{
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
        public const uint DefaultPolynomial = 0xedb88320u;
        public const uint DefaultSeed = 0xffffffffu;

        static uint[] defaultTable;

        readonly uint seed;
        readonly uint[] table;
        uint hash;

        public Crc32() : this(DefaultPolynomial, DefaultSeed)
        {
        }

        public Crc32(uint polynomial, uint seed)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new PlatformNotSupportedException("Not supported on Big Endian processors");
            }

            table = InitializeTable(polynomial);
            this.seed = hash = seed;
        }

        public static int Compute(byte[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            else if (buffer.Length < index)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            else if (buffer.Length - index < count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return (int)Compute(buffer.Skip(index).Take(count).ToArray());
        }

        public override void Initialize()
        {
            hash = seed;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            hash = CalculateHash(table, hash, array, ibStart, cbSize);
        }

        protected override byte[] HashFinal()
        {
            var hashBuffer = UintToBigEndianBytes(~hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize { get; } = 32;

        public static uint Compute(byte[] buffer)
        {
            return Compute(DefaultSeed, buffer);
        }

        public static uint Compute(uint seed, byte[] buffer)
        {
            return Compute(DefaultPolynomial, seed, buffer);
        }

        public static uint Compute(uint polynomial, uint seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }

        static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null) return defaultTable;

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

            if (polynomial == DefaultPolynomial) defaultTable = createTable;

            return createTable;
        }

        static uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
        {
            var hash = seed;

            for (var i = start; i < start + size; i++)
            {
                hash = (hash >> 8) ^ table[buffer[i] ^ hash & 0xff];
            }

            return hash;
        }

        static byte[] UintToBigEndianBytes(uint value)
        {
            var result = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian) Array.Reverse(result);

            return result;
        }
    }
}