namespace GlassTL.Telegram.MTProto.Crypto.AES
{
    using System;
    using System.Security.Cryptography;
    using Utils;

    public static class Aes
    {
        public static byte[] DecryptWithNonces(byte[] data, byte[] serverNonce, byte[] newNonce)
        {
            return DecryptAes(GenerateKeyDataFromNonces(serverNonce, newNonce), data);
        }

        public static AesKeyData GenerateKeyDataFromNonces(byte[] serverNonce, byte[] newNonce)
        {
            using var sha1 = new SHA1Managed();
            
            // new_nonce + server_nonce
            var byte1 = new byte[newNonce.Length + serverNonce.Length];
            Buffer.BlockCopy(newNonce, 0, byte1, 0, newNonce.Length);
            Buffer.BlockCopy(serverNonce, 0, byte1, newNonce.Length, serverNonce.Length);
            // SHA1(byte1)
            var hash1 = sha1.ComputeHash(byte1);

            // server_nonce + new_nonce
            var byte2 = new byte[serverNonce.Length + newNonce.Length];
            Buffer.BlockCopy(serverNonce, 0, byte2, 0, serverNonce.Length);
            Buffer.BlockCopy(newNonce, 0, byte2, serverNonce.Length, newNonce.Length);
            // SHA1(byte2)
            var hash2 = sha1.ComputeHash(byte2);

            // tmp_aes_key := hash1 + substr (hash2, 0, 12);
            var key = new byte[hash1.Length + 12];
            Buffer.BlockCopy(hash1, 0, key, 0, hash1.Length);
            Buffer.BlockCopy(hash2, 0, key, hash1.Length, 12);

            //---------------------------------------------------------------------

            // new_nonce + new_nonce
            var byte3 = new byte[newNonce.Length * 2];
            Buffer.BlockCopy(newNonce, 0, byte3, 0, newNonce.Length);
            Buffer.BlockCopy(newNonce, 0, byte3, newNonce.Length, newNonce.Length);
            // SHA1(byte3)
            var hash3 = sha1.ComputeHash(byte3);

            // tmp_aes_iv := substr (hash2, 12, 8) + hash3 + substr (new_nonce, 0, 4);
            var iv = new byte[8 + hash3.Length + 4];
            Buffer.BlockCopy(hash2, 12, iv, 0, 8);
            Buffer.BlockCopy(hash3, 0, iv, 8, hash3.Length);
            Buffer.BlockCopy(newNonce, 0, iv, 8 + hash3.Length, 4);

            return new AesKeyData(key, iv);
        }

        public static byte[] DecryptAes(AesKeyData key, byte[] ciphertext) => DecryptIge(ciphertext, key.GetKey(), key.GetIv());
        public static byte[] EncryptAes(AesKeyData key, byte[] plaintext) => EncryptIge(plaintext, key.GetKey(), key.GetIv());

        public static byte[] DecryptIge(byte[] ciphertext, byte[] key, byte[] iv)
        {   
            // Check arguments.
            if (ciphertext is not {Length: > 0}) throw new ArgumentNullException(nameof(ciphertext));
            if (key is not {Length: > 0}) throw new ArgumentNullException(nameof(key));
            if (iv is not {Length: > 0}) throw new ArgumentNullException(nameof(iv));

            var iv1 = new byte[iv.Length / 2];
            var iv2 = new byte[iv.Length / 2];

            Array.Copy(iv, 0, iv1, 0, iv1.Length);
            Array.Copy(iv, iv1.Length, iv2, 0, iv2.Length);

            var aes = new AesIgeEngine();
            aes.Init(false, key);

            var ciphertextBlock = new byte[16];
            var plaintextBlock = new byte[16];
            var plaintext = new byte[ciphertext.Length];
            
            for (var blockIndex = 0; blockIndex < ciphertext.Length / 16; blockIndex++)
            {
                for (var i = 0; i < 16; i++) ciphertextBlock[i] = (byte)(ciphertext[blockIndex * 16 + i] ^ iv2[i]);

                aes.ProcessBlock(ciphertextBlock, 0, plaintextBlock, 0);

                for (var i = 0; i < 16; i++) plaintextBlock[i] ^= iv1[i];

                Array.Copy(ciphertext, blockIndex * 16, iv1, 0, 16);
                Array.Copy(plaintextBlock, 0, iv2, 0, 16);

                Array.Copy(plaintextBlock, 0, plaintext, blockIndex * 16, 16);
            }

            return plaintext;
        }
        public static byte[] EncryptIge(byte[] originPlaintext, byte[] key, byte[] iv)
        {
            // Check arguments.
            if (originPlaintext is not {Length: > 0}) throw new ArgumentNullException(nameof(originPlaintext));
            if (key is not {Length: > 0}) throw new ArgumentNullException(nameof(key));
            if (iv is not {Length: > 0}) throw new ArgumentNullException(nameof(iv));
            
            var padding = Helpers.GenerateRandomBytes(Helpers.PositiveMod(-originPlaintext.Length, 16));
            var plaintext = new byte[originPlaintext.Length + padding.Length];
            Array.Copy(originPlaintext, 0, plaintext, 0, originPlaintext.Length);
            Array.Copy(padding, 0, plaintext, originPlaintext.Length, padding.Length);

            var iv1 = new byte[iv.Length / 2];
            var iv2 = new byte[iv.Length / 2];

            Array.Copy(iv, 0, iv1, 0, iv1.Length);
            Array.Copy(iv, iv1.Length, iv2, 0, iv2.Length);

            var aes = new AesIgeEngine();
            aes.Init(true, key);
            
            var ciphertextBlock = new byte[16];
            var plaintextBlock = new byte[16];
            var ciphertext = new byte[plaintext.Length];

            for (var blockIndex = 0; blockIndex < plaintext.Length / 16; blockIndex++)
            {
                Array.Copy(plaintext, 16 * blockIndex, plaintextBlock, 0, 16);

                for (var i = 0; i < 16; i++) plaintextBlock[i] ^= iv1[i];

                aes.ProcessBlock(plaintextBlock, 0, ciphertextBlock, 0);

                for (var i = 0; i < 16; i++) ciphertextBlock[i] ^= iv2[i];
                
                Array.Copy(ciphertextBlock, 0, iv1, 0, 16);
                Array.Copy(plaintext, 16 * blockIndex, iv2, 0, 16);
                Array.Copy(ciphertextBlock, 0, ciphertext, blockIndex * 16, 16);
            }

            return ciphertext;
        }

        public static byte[] Xor(byte[] buffer1, byte[] buffer2)
        {
            var result = new byte[buffer1.Length];
            for (var i = 0; i < buffer1.Length; i++) result[i] = (byte)(buffer1[i] ^ buffer2[i]);
            return result;
        }
    }
}