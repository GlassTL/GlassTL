﻿namespace GlassTL.Telegram.MTProto.Crypto
{
    using System;
    using System.Security;
    using System.Security.Cryptography;
    using Utils;

    public static class CloudPasswordHelper
    {
        /// <summary>
        /// All operations use 256-bit hashes
        /// </summary>
        private const int KSizeForHash = 256;

        private static readonly dynamic Schema = new TLSchema();

        /// <summary>
        /// Hashes bytes using the SHA-256 hashing method
        /// </summary>
        /// <param name="buffer">Accepts byte arrays and concats them</param>
        private static byte[] Sha256(params byte[][] buffer)
        {
            // Create a new instance rather than reuse an existing one
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(buffer.Join());
        }
        /// <summary>
        /// Xor's one byte array with another
        /// </summary>
        /// <param name="a">A byte array</param>
        /// <param name="b">A byte array</param>
        private static byte[] Xor(byte[] a, byte[] b)
        {
            // We only support blocks of the same length.
            if (a.Length != b.Length) throw new ArgumentException($"Cannot Xor two blocks of unequal length.");

            // Loop through and xor the bytes
            for (var i = 0; i < b.Length; i++) a[i] ^= b[i];

            return a;
        }
        /// <summary>
        /// Generates a 512-bit PBKDF2/RFC2898 derivative with HMAC SHA-512
        /// </summary>
        /// <param name="password">The master password from which a derived key is generated</param>
        /// <param name="salt">A cryptographic salt for the operation</param>
        /// <param name="iterations">The number of iterations</param>
        private static byte[] Pbkdf2Sha512(byte[] password, byte[] salt, int iterations)
        {
            // Use built-in implementation.
            // Note:  There's no point to outputting more bits than the base hash function size.
            // SHA-512 => 512 bits (64 bytes)
            // ToDo: Explore other ways of doing this because it is horrendously slow...
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA512);
            return deriveBytes.GetBytes(64);
        }

        /// <summary>
        /// Computes the PH2 hash based on the following:
        ///
        /// PH2 = SH(pbkd2(sha512, PH1(password, salt1, salt2), salt1, 100000), salt2)
        /// </summary>
        /// <param name="algo">The TLObject from account.getPassword["current_algo"]</param>
        /// <param name="password">The cloud password that the user provided</param>
        private static byte[] PasswordHash(TLObject algo, SecureString password)
        {
            // Given the following:
            //  H(data) = SHA256(data)
            //  SH(data, salt) = H(salt | data | salt)
            //  PH1(password, salt1, salt2) = SH(SH(password, salt1), salt2)
            //  PH2(password, salt1, salt2) = SH(pbkd2(sha512, PH1(password, salt1, salt2), salt1, 100000), salt2)

            var ph1 = Sha256(
                algo["salt2"],
                password.Process(unsafePassword => Sha256(algo["salt1"], unsafePassword, algo["salt1"])),
                algo["salt2"]
            );

            var ph2 = Sha256(
                algo["salt2"],
                Pbkdf2Sha512(ph1, algo["salt1"], 100000),
               algo["salt2"]
            );

            return ph2;
        }
        /// <summary>
        /// Computes v based on the following:
        ///
        /// v = pow(g, PH2(password, salt1, salt2)) mod p
        /// </summary>
        /// <param name="algo">The TLObject from account.getPassword["current_algo"]</param>
        /// <param name="password">The cloud password that the user provided</param>
        public static byte[] DigestPasswordHash(TLObject algo, SecureString password)
        {
            var g = new BigInteger(algo["g"].ToString());
            var ph2 = new BigInteger(1, PasswordHash(algo, password));
            var p = new BigInteger(1, algo["p"]);

            return g.ModPow(ph2, p).ToByteArrayUnsigned().PadByteArray(KSizeForHash);
        }

        /// <summary>
        /// Generates a 2048-bit random number, calculating and returning:
        ///
        /// a = 2048-bit cryptographically safe number
        /// g_a = pow(g, a) mod p
        /// u = H(g_a | g_b)
        /// </summary>
        /// <param name="g">The <see cref="BigInteger"/> from account.getPassword["current_algo"]["g"]</param>
        /// <param name="p">The <see cref="BigInteger"/> from account.getPassword["current_algo"]["p"]></param>
        /// <param name="srpB">The byte data from account.getPassword["srp_B"]</param>
        private static (BigInteger, byte[], BigInteger) GenerateAndCheckRandom(BigInteger g, BigInteger p, byte[] srpB)
        {
            // Loop until we have valid info
            while (true)
            {
                // A 2048-bit BigInteger with random data
                var a = new BigInteger(1, Helpers.GenerateRandomBytes(2048 / 8));

                // g_a = pow(g, a) mod p
                var gA = g.ModPow(a, p);

                // Validate the info
                if (!IsGoodModExpFirst(gA, p)) continue;

                // We need the byte data anyway
                var gaForHash = gA.ToByteArrayUnsigned().PadByteArray(KSizeForHash);
                // u = H(g_a | g_b)
                var u = new BigInteger(1, Sha256(gaForHash, srpB));

                // u will always be positive

                return (a, gaForHash, u);
            }
        }

        // Validate Primes.  This needs to be reviewed...
        private static bool IsPrimeAndGood(byte[] primeBytes, int g)
        {
            var goodPrime = new byte[] {
                0xC7, 0x1C, 0xAE, 0xB9, 0xC6, 0xB1, 0xC9, 0x04, 0x8E, 0x6C, 0x52, 0x2F, 0x70, 0xF1, 0x3F, 0x73,
                0x98, 0x0D, 0x40, 0x23, 0x8E, 0x3E, 0x21, 0xC1, 0x49, 0x34, 0xD0, 0x37, 0x56, 0x3D, 0x93, 0x0F,
                0x48, 0x19, 0x8A, 0x0A, 0xA7, 0xC1, 0x40, 0x58, 0x22, 0x94, 0x93, 0xD2, 0x25, 0x30, 0xF4, 0xDB,
                0xFA, 0x33, 0x6F, 0x6E, 0x0A, 0xC9, 0x25, 0x13, 0x95, 0x43, 0xAE, 0xD4, 0x4C, 0xCE, 0x7C, 0x37,
                0x20, 0xFD, 0x51, 0xF6, 0x94, 0x58, 0x70, 0x5A, 0xC6, 0x8C, 0xD4, 0xFE, 0x6B, 0x6B, 0x13, 0xAB,
                0xDC, 0x97, 0x46, 0x51, 0x29, 0x69, 0x32, 0x84, 0x54, 0xF1, 0x8F, 0xAF, 0x8C, 0x59, 0x5F, 0x64,
                0x24, 0x77, 0xFE, 0x96, 0xBB, 0x2A, 0x94, 0x1D, 0x5B, 0xCD, 0x1D, 0x4A, 0xC8, 0xCC, 0x49, 0x88,
                0x07, 0x08, 0xFA, 0x9B, 0x37, 0x8E, 0x3C, 0x4F, 0x3A, 0x90, 0x60, 0xBE, 0xE6, 0x7C, 0xF9, 0xA4,
                0xA4, 0xA6, 0x95, 0x81, 0x10, 0x51, 0x90, 0x7E, 0x16, 0x27, 0x53, 0xB5, 0x6B, 0x0F, 0x6B, 0x41,
                0x0D, 0xBA, 0x74, 0xD8, 0xA8, 0x4B, 0x2A, 0x14, 0xB3, 0x14, 0x4E, 0x0E, 0xF1, 0x28, 0x47, 0x54,
                0xFD, 0x17, 0xED, 0x95, 0x0D, 0x59, 0x65, 0xB4, 0xB9, 0xDD, 0x46, 0x58, 0x2D, 0xB1, 0x17, 0x8D,
                0x16, 0x9C, 0x6B, 0xC4, 0x65, 0xB0, 0xD6, 0xFF, 0x9C, 0xA3, 0x92, 0x8F, 0xEF, 0x5B, 0x9A, 0xE4,
                0xE4, 0x18, 0xFC, 0x15, 0xE8, 0x3E, 0xBE, 0xA0, 0xF8, 0x7F, 0xA9, 0xFF, 0x5E, 0xED, 0x70, 0x05,
                0x0D, 0xED, 0x28, 0x49, 0xF4, 0x7B, 0xF9, 0x59, 0xD9, 0x56, 0x85, 0x0C, 0xE9, 0x29, 0x85, 0x1F,
                0x0D, 0x81, 0x15, 0xF6, 0x35, 0xB1, 0x05, 0xEE, 0x2E, 0x4E, 0x15, 0xD0, 0x4B, 0x24, 0x54, 0xBF,
                0x6F, 0x4F, 0xAD, 0xF0, 0x34, 0xB1, 0x04, 0x03, 0x11, 0x9C, 0xD8, 0xE3, 0xB9, 0x2F, 0xCC, 0x5B
            };

            if (goodPrime.DirectSequenceEquals(primeBytes)) return IsPrimeAndGoodCheck(new BigInteger(1, primeBytes), g);
            return g is 3 or 4 or 5 or 7 || IsPrimeAndGoodCheck(new BigInteger(1, primeBytes), g);
        }
        // Validate Primes.  This needs to be reviewed...
        private static bool IsPrimeAndGoodCheck(BigInteger prime, int g)
        {
            const int kGoodPrimeBitsCount = 2048;

            if (prime < 0 || prime.BitLength != kGoodPrimeBitsCount)
            {
                // LOG(("MTP Error: Bad prime bits count %1, expected %2." ).arg(prime.bitsSize()).arg(kGoodPrimeBitsCount));
                return false;
            }

            if (!prime.IsProbablePrime(10))
            {
                //LOG(("MTP Error: Bad prime."));
                return false;
            }

            switch (g)
            {
                case 2:
                    if (prime % 8 != 7)
                    {
                        //LOG(("BigNum PT Error: bad g value: %1, mod8: %2").arg(g).arg(mod8));
                        return false;
                    }
                    break;
                case 3:
                    if (prime % 3 != 2)
                    {
                        //LOG(("BigNum PT Error: bad g value: %1, mod3: %2").arg(g).arg(mod3));
                        return false;
                    }
                    break;
                case 4:
                    break;
                case 5:
                    var mod5 = prime % 5;
                    if (mod5 != 1 && mod5 != 4)
                    {
                        //LOG(("BigNum PT Error: bad g value: %1, mod5: %2").arg(g).arg(mod5));
                        return false;
                    }
                    break;
                case 6:
                    var mod24 = prime % 24;
                    if (mod24 != 19 && mod24 != 23)
                    {
                        //LOG(("BigNum PT Error: bad g value: %1, mod24: %2").arg(g).arg(mod24));
                        return false;
                    }
                    break;
                case 7:
                    var mod7 = prime % 7;
                    if (mod7 != 3 && mod7 != 5 && mod7 != 6)
                    {
                        //LOG(("BigNum PT Error: bad g value: %1, mod7: %2").arg(g).arg(mod7));
                        return false;
                    }
                    break;
                default:
                    //LOG(("BigNum PT Error: bad g value: %1").arg(g));
                    return false;
            }

            if (!((prime - 1) / 2).IsProbablePrime(10))
            {
                //LOG(("MTP Error: Bad (prime - 1) / 2."));
                return false;
            }

            return true;
        }
        // Validate Primes.  This needs to be reviewed...
        private static bool IsGoodModExpFirst(BigInteger modexp, BigInteger prime)
        {
            var diff = prime - modexp;
            const int kMinDiffBitsCount = 2048 - 64;

            return diff >= 0 && diff.BitLength >= kMinDiffBitsCount && modexp.BitLength >= kMinDiffBitsCount && modexp.BitLength / 8 <= KSizeForHash;
        }

        /// <summary>
        /// Determines whether or not a <see cref="BigInteger" /> is positive
        /// </summary>
        /// <param name="number">Number to analyze</param>
        private static bool IsPositive(BigInteger number)
        {
            return number >= 0;
        }

        /// <summary>
        /// Determines whether or not a <see cref="BigInteger" /> is positive and smaller than a cap
        /// </summary>
        /// <param name="number">Number to analyze</param>
        /// <param name="p">The cap for our number</param>
        private static bool IsGoodLarge(BigInteger number, BigInteger p)
        {
            return IsPositive(number) && IsPositive(p - number);
        }

        /// <summary>
        /// Builds an `inputCheckPasswordSRP` object when provided with the results from
        /// `account.getPassword` and the user's cloud password
        /// </summary>
        /// <param name="request">The results from `account.getPassword`</param>
        /// <param name="password">The 2FA password from the user</param>
        public static TLObject ComputePasswordCheck(TLObject request, SecureString password)
        {
            var algo = new TLObject(request["current_algo"]);
            var pwHash = PasswordHash(algo, password);

            if (algo["_"] != "passwordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow")
            {
                throw new Exception($"Password algorithm not supported: {(string)algo["_"]}");
            }

            var p = new BigInteger(1, algo["p"]);
            var g = new BigInteger(algo["g"].ToString());
            var b = new BigInteger(1, request["srp_B"]);

            if (!IsPrimeAndGood(algo["p"], (int)algo["g"])) throw new Exception($"Bad or unsupported p_g");
            if (!IsGoodLarge(b, p))  throw new Exception($"Bad or unsupported B");

            var x = new BigInteger(1, pwHash);
            var pForHash = ((byte[])algo["p"]).PadByteArray(KSizeForHash);
            var gForHash = g.ToByteArrayUnsigned().PadByteArray(KSizeForHash);
            var bForHash = ((byte[])request["srp_B"]).PadByteArray(KSizeForHash);
            var kgX = new BigInteger(1, Sha256(pForHash, gForHash)) * g.ModPow(x, p) % p;

            var (a, gaForHash, u) = GenerateAndCheckRandom(g, p, bForHash);

            var gB = b - kgX % p;

            if (!IsGoodModExpFirst(gB, p)) throw new Exception($"Bad or unsupported g_b");

            var k = Sha256(gB.ModPow(a + u * x, p).ToByteArrayUnsigned());
            var m1 = Sha256(
                Xor(Sha256(pForHash), Sha256(gForHash)),
                Sha256(algo["salt1"]),
                Sha256(algo["salt2"]),
                gaForHash,
                bForHash,
                k
            );

            return Schema.inputCheckPasswordSRP(new
            {
                srp_id = request["srp_id"],
                A      = gaForHash,
                M1     = m1
            });
        }
    }
}
