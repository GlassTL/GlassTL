namespace GlassTL.Telegram.MTProto.Crypto.RSA
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Linq;

    public static class Rsa
    {
        private static Dictionary<long, RSACryptoServiceProvider> _cachedProviders;

        private static IEnumerable<string> RsaPublicKeys(RSAKeySelection options = RSAKeySelection.New) {
            var oldKeys = new[]
            {
                // 0xc3b42b026ce86b21
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAwVACPi9w23mF3tBkdZz+zwrzKOaaQdr01vAbU4E1pvkfj4sqDsm6
                lyDONS789sVoD/xCS9Y0hkkC3gtL1tSfTlgCMOOul9lcixlEKzwKENj1Yz/s7daS
                an9tqw3bfUV/nqgbhGX81v/+7RFAEd+RwFnK7a+XYl9sluzHRyVVaTTveB2GazTw
                Efzk2DWgkBluml8OREmvfraX3bkHZJTKX4EQSjBbbdJ2ZXIsRrYOXfaA+xayEGB+
                8hdlLmAjbCVfaigxX0CDqWeR1yFL9kwd9P0NsZRPsmoqVwMbMu7mStFai6aIhc3n
                Slv8kg9qv1m6XHVQY3PnEw+QQtqSIXklHwIDAQAB
                -----END RSA PUBLIC KEY-----",
                // 0x9a996a1db11c729b
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAxq7aeLAqJR20tkQQMfRn+ocfrtMlJsQ2Uksfs7Xcoo77jAid0bRt
                ksiVmT2HEIJUlRxfABoPBV8wY9zRTUMaMA654pUX41mhyVN+XoerGxFvrs9dF1Ru
                vCHbI02dM2ppPvyytvvMoefRoL5BTcpAihFgm5xCaakgsJ/tH5oVl74CdhQw8J5L
                xI/K++KJBUyZ26Uba1632cOiq05JBUW0Z2vWIOk4BLysk7+U9z+SxynKiZR3/xdi
                XvFKk01R3BHV+GUKM2RYazpS/P8v7eyKhAbKxOdRcFpHLlVwfjyM1VlDQrEZxsMp
                NTLYXb6Sce1Uov0YtNx5wEowlREH1WOTlwIDAQAB
                -----END RSA PUBLIC KEY-----",
                // ReSharper disable once CommentTypo
                // 0xb05b2a6f70cdea78
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAsQZnSWVZNfClk29RcDTJQ76n8zZaiTGuUsi8sUhW8AS4PSbPKDm+
                DyJgdHDWdIF3HBzl7DHeFrILuqTs0vfS7Pa2NW8nUBwiaYQmPtwEa4n7bTmBVGsB
                1700/tz8wQWOLUlL2nMv+BPlDhxq4kmJCyJfgrIrHlX8sGPcPA4Y6Rwo0MSqYn3s
                g1Pu5gOKlaT9HKmE6wn5Sut6IiBjWozrRQ6n5h2RXNtO7O2qCDqjgB2vBxhV7B+z
                hRbLbCmW0tYMDsvPpX5M8fsO05svN+lKtCAuz1leFns8piZpptpSCFn7bWxiA9/f
                x5x17D7pfah3Sy2pA+NDXyzSlGcKdaUmwQIDAQAB
                -----END RSA PUBLIC KEY-----",
                // 0x71e025b6c76033e3
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAwqjFW0pi4reKGbkc9pK83Eunwj/k0G8ZTioMMPbZmW99GivMibwa
                xDM9RDWabEMyUtGoQC2ZcDeLWRK3W8jMP6dnEKAlvLkDLfC4fXYHzFO5KHEqF06i
                qAqBdmI1iBGdQv/OQCBcbXIWCGDY2AsiqLhlGQfPOI7/vvKc188rTriocgUtoTUc
                /n/sIUzkgwTqRyvWYynWARWzQg0I9olLBBC2q5RQJJlnYXZwyTL3y9tdb7zOHkks
                WV9IMQmZmyZh/N7sMbGWQpt4NMchGpPGeJ2e5gHBjDnlIf2p1yZOYeUYrdbwcS0t
                UiggS4UeE8TzIuXFQxw7fzEIlmhIaq3FnwIDAQAB
                -----END RSA PUBLIC KEY-----"
            };

            var newKeys = new[]
            {
                // 0xbc35f3509f7b7a5
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAruw2yP/BCcsJliRoW5eBVBVle9dtjJw+OYED160Wybum9SXtBBLX
                riwt4rROd9csv0t0OHCaTmRqBcQ0J8fxhN6/cpR1GWgOZRUAiQxoMnlt0R93LCX/
                j1dnVa/gVbCjdSxpbrfY2g2L4frzjJvdl84Kd9ORYjDEAyFnEA7dD556OptgLQQ2
                e2iVNq8NZLYTzLp5YpOdO1doK+ttrltggTCy5SrKeLoCPPbOgGsdxJxyz5KKcZnS
                Lj16yE5HvJQn0CNpRdENvRUXe6tBP78O39oJ8BTHp9oIjd6XWXAsp2CvK45Ol8wF
                XGF710w9lwCGNbmNxNYhtIkdqfsEcwR5JwIDAQAB
                -----END RSA PUBLIC KEY-----",
                // 0x15ae5fa8b5529542
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAvfLHfYH2r9R70w8prHblWt/nDkh+XkgpflqQVcnAfSuTtO05lNPs
                pQmL8Y2XjVT4t8cT6xAkdgfmmvnvRPOOKPi0OfJXoRVylFzAQG/j83u5K3kRLbae
                7fLccVhKZhY46lvsueI1hQdLgNV9n1cQ3TDS2pQOCtovG4eDl9wacrXOJTG2990V
                jgnIKNA0UMoP+KF03qzryqIt3oTvZq03DyWdGK+AZjgBLaDKSnC6qD2cFY81UryR
                WOab8zKkWAnhw2kFpcqhI0jdV5QaSCExvnsjVaX0Y1N0870931/5Jb9ICe4nweZ9
                kSDF/gip3kWLG0o8XQpChDfyvsqB9OLV/wIDAQAB
                -----END RSA PUBLIC KEY-----",
                // ReSharper disable once CommentTypo
                // 0xaeae98e13cd7f94f
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAs/ditzm+mPND6xkhzwFIz6J/968CtkcSE/7Z2qAJiXbmZ3UDJPGr
                zqTDHkO30R8VeRM/Kz2f4nR05GIFiITl4bEjvpy7xqRDspJcCFIOcyXm8abVDhF+
                th6knSU0yLtNKuQVP6voMrnt9MV1X92LGZQLgdHZbPQz0Z5qIpaKhdyA8DEvWWvS
                Uwwc+yi1/gGaybwlzZwqXYoPOhwMebzKUk0xW14htcJrRrq+PXXQbRzTMynseCoP
                Ioke0dtCodbA3qQxQovE16q9zz4Otv2k4j63cz53J+mhkVWAeWxVGI0lltJmWtEY
                K6er8VqqWot3nqmWMXogrgRLggv/NbbooQIDAQAB
                -----END RSA PUBLIC KEY-----",
                // 0x5a181b2235057d98
                @"-----BEGIN RSA PUBLIC KEY-----
                MIIBCgKCAQEAvmpxVY7ld/8DAjz6F6q05shjg8/4p6047bn6/m8yPy1RBsvIyvuD
                uGnP/RzPEhzXQ9UJ5Ynmh2XJZgHoE9xbnfxL5BXHplJhMtADXKM9bWB11PU1Eioc
                3+AXBB8QiNFBn2XI5UkO5hPhbb9mJpjA9Uhw8EdfqJP8QetVsI/xrCEbwEXe0xvi
                fRLJbY08/Gp66KpQvy7g8w7VB8wlgePexW3pT13Ap6vuC+mQuJPyiHvSxjEKHgqe
                Pji9NP3tJUFQjcECqcm0yV7/2d0t/pbCm+ZH1sadZspQCEPPrtbkQBlvHb4OLiIW
                PGHKSMeRFvp3IWcmdJqXahxLCUS1Eh6MAQIDAQAB
                -----END RSA PUBLIC KEY-----"
            };

            return options switch
            {
                RSAKeySelection.New => newKeys,
                RSAKeySelection.Old => oldKeys,
                _ => newKeys.Concat(oldKeys).ToArray(),
            };
        }

        private static int ReadTagNumber(Stream s, int tag)
        {
            var tagNo = tag & 0x1f;

            //
            // with tagged object tag number is bottom 5 bits, or stored at the start of the content
            //
            if (tagNo != 0x1f) return tagNo;
            
            tagNo = 0;

            var b = s.ReadByte();

            // X.690-0207 8.1.2.4.2
            // "c) bits 7 to 1 of the first subsequent octet shall not all be zero."
            if ((b & 0x7f) == 0) // Note: -1 will pass
            {
                throw new IOException("Corrupted stream - invalid high tag number found");
            }

            while (b >= 0 && (b & 0x80) != 0)
            {
                tagNo |= b & 0x7f;
                tagNo <<= 7;
                b = s.ReadByte();
            }

            if (b < 0) throw new EndOfStreamException("EOF found inside tag value.");

            tagNo |= b & 0x7f;

            return tagNo;
        }
        private static int ReadLength(Stream s, int limit)
        {
            var length = s.ReadByte();
            
            switch (length)
            {
                case < 0:
                    throw new EndOfStreamException("EOF found when length expected");
                case 0x80:
                    return -1; // indefinite-length encoding
                case > 127:
                {
                    var size = length & 0x7f;

                    // Note: The invalid long form "0xff" (see X.690 8.1.3.5c) will be caught here
                    if (size > 4) throw new IOException("DER length more than 4 bytes: " + size);

                    length = 0;
                    for (var i = 0; i < size; i++)
                    {
                        var next = s.ReadByte();

                        if (next < 0) throw new EndOfStreamException("EOF found reading length");

                        length = (length << 8) + next;
                    }

                    if (length < 0) throw new IOException("Corrupted stream - negative length found");

                    // after all we must have read at least 1 byte
                    if (length >= limit) throw new IOException("Corrupted stream - out of bounds length found");
                    break;
                }
            }

            return length;
        }

        private static long ComputeHash(BigInteger modulus, BigInteger exponent)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            Utils.BytesUtil.Serialize(modulus.ToByteArrayUnsigned(), writer);
            Utils.BytesUtil.Serialize(exponent.ToByteArrayUnsigned(), writer);

            byte[] shaResult;

            using (var sha12 = SHA1.Create()) shaResult = sha12.ComputeHash(stream.ToArray());
            
            return BitConverter.ToInt64(shaResult, 12);
        }

        /// <summary>
        /// Loads an RSA Public Key for use in RSA Cryptography
        /// </summary>
        /// <param name="pemString"></param>
        /// <returns></returns>
        private static KeyValuePair<long, RSACryptoServiceProvider> Load(string pemString)
        {
            if (!pemString.Contains("BEGIN RSA PUBLIC KEY"))
            {
                throw new Exception("This method is only valid for RSA Public Keys and has been tailored to work with Telegram's in particular.  Consider using Bouncy Castle.");
            }

            var pemStripped = pemString;

            pemStripped = Regex.Replace(pemStripped, @"-+.*-+", "");
            pemStripped = Regex.Replace(pemStripped, @"\s*", "", RegexOptions.Singleline);

            if (pemStripped.Length % 4 != 0)
            {
                throw new Exception("The pem key appears to be corrupt and cannot be parsed.");
            }

            var pemData = Convert.FromBase64String(pemStripped);

            BigInteger modulus;
            BigInteger exponent;

            using (var stream = new MemoryStream(pemData, false))
            using (var reader = new BinaryReader(stream))
            {
                int tag, item1Length, item2Length;

                if ((tag = ReadTagNumber(stream, reader.ReadByte())) <= 0)
                    throw new Exception("The provided key doesn't appear to be valid or isn't supported.  Consider using Bouncy Castle.");

                if ((tag & 0x40) != 0)
                    throw new Exception("Application specific objects are not supported.  Consider using Bouncy Castle.");

                if ((tag & 0x80) != 0)
                    throw new Exception("Tagged objects are not supported.  Consider using Bouncy Castle.");

                if (ReadLength(stream, (int)(stream.Length - stream.Position)) <= 0)
                    throw new Exception("The provided key has an indefinite length.  Whereas this may be a valid key, it is not currently supported.  Consider using Bouncy Castle.");

                if (ReadTagNumber(stream, reader.ReadByte()) != 0x2)
                    throw new Exception("The first object in the key must be primitive.  Consider using Bouncy Castle.");

                if ((item1Length = ReadLength(stream, (int)(stream.Length - stream.Position))) <= 0)
                    throw new Exception("The provided key has an indefinite length.  Whereas this may be a valid key, it is not currently supported.  Consider using Bouncy Castle.");

                modulus = new BigInteger(1, reader.ReadBytes(item1Length));

                if (ReadTagNumber(stream, reader.ReadByte()) != 0x2)
                    throw new Exception("The second object in the key must be primitive.  Consider using Bouncy Castle.");

                if ((item2Length = ReadLength(stream, (int)(stream.Length - stream.Position))) <= 0)
                    throw new Exception("The provided key has an indefinite length.  Whereas this may be a valid key, it is not currently supported.  Consider using Bouncy Castle.");

                exponent = new BigInteger(1, reader.ReadBytes(item2Length));
            }

            var rsaKey = new RSACryptoServiceProvider();

            rsaKey.ImportParameters(new RSAParameters
            {
                Modulus  = modulus.ToByteArrayUnsigned(),
                Exponent = exponent.ToByteArrayUnsigned()
            });

            return new KeyValuePair<long, RSACryptoServiceProvider>(ComputeHash(modulus, exponent), rsaKey);
        }
        /// <summary>
        /// Loads Telegram's RSA Public Key for use in RSA Cryptography
        /// </summary>
        private static Dictionary<long, RSACryptoServiceProvider> LoadAll()
        {
            if (_cachedProviders != null) return _cachedProviders;
            
            _cachedProviders = new Dictionary<long, RSACryptoServiceProvider>();

            var keys = RsaPublicKeys(RSAKeySelection.All);

            foreach (var pemString in keys)
            {
                var (key, value) = Load(pemString);
                _cachedProviders.Add(key, value);
            }

            return _cachedProviders;
        }

        public static RsaEncrypter Encrypt(IEnumerable<long> fingerprints)
        {
            var providers = LoadAll();

            foreach (var fingerprint in fingerprints)
            {
                if (!providers.ContainsKey(fingerprint)) continue;

                return new RsaEncrypter(fingerprint, providers[fingerprint]);
            }

            throw new Exception("The server responded with an unknown fingerprint");
        }
    }
}

