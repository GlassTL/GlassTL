namespace GlassTL.Telegram.Network.Authentication
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using MTProto;
    using MTProto.Crypto;
    using Utils;
    using Aes = MTProto.Crypto.AES.Aes;

    public partial class Authenticator
    {
        private void HandleServerDhRequest(TLObject serverDhParams)
        {
            // Make sure we have all the needed info
            if (_pqInnerData == null)
            {
                HandleException(new Exception("Unable to find the PQInnerData object from previous steps.  Please restart the connection process"));
                return;
            }

            Logger.Log(Logger.Level.Debug, $"Received TLObject {serverDhParams["_"]}.");

            // Determine the result
            if (serverDhParams.GetAs<string>("_") != "server_DH_params_ok")
            {
                // Aaaand we failed to handle things correctly
                HandleException(new Exception($"The server responded with a {serverDhParams["_"]} response.  We are unable to continue."));
                return;
            }

            Logger.Log(Logger.Level.Debug, "Decrypting the DHInnerData");

            var key = Aes.GenerateKeyDataFromNonces(serverDhParams.GetAs<byte[]>("server_nonce"), _pqInnerData.GetAs<byte[]>("new_nonce"));
            var plaintextAnswer = Aes.DecryptAes(key, serverDhParams.GetAs<byte[]>("encrypted_answer"));

            using (var memory = new MemoryStream(plaintextAnswer))
            using (var reader = new BinaryReader(memory))
            {
                // ToDo: Implement HashCode check
                var _ = reader.ReadBytes(20);
                _serverDhInnerData = TLObject.Deserialize(reader);
            }

            if (_serverDhInnerData == null)
            {
                HandleException(new Exception("The server did not respond with valid DH Inner Data.  We are unable to continue."));
                return;
            }

            if (_serverDhInnerData.GetAs<string>("_") != "server_DH_inner_data")
            {
                HandleException(new Exception($"The server responded with a {_serverDhInnerData["_"]} response.  We are unable to continue."));
                return;
            }

            Logger.Log(Logger.Level.Debug, $"Decrypted TLObject {_serverDhInnerData["_"]}.");

            _timeOffset = _serverDhInnerData.GetAs<int>("server_time") - (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            Logger.Log(Logger.Level.Debug, $"Updated Time Offset {_timeOffset}.");
            Logger.Log(Logger.Level.Debug, $"Calculating GAB");

            var b = new BigInteger(1, Helpers.GenerateRandomBytes(256));
            var gb = BigInteger.ValueOf(_serverDhInnerData.GetAs<int>("g")).ModPow(b, new BigInteger(1, _serverDhInnerData.GetAs<byte[]>("dh_prime")));

            _gab = new BigInteger(1, _serverDhInnerData.GetAs<byte[]>("g_a")).ModPow(b, new BigInteger(1, _serverDhInnerData.GetAs<byte[]>("dh_prime")));
            
            var clientDhInnerDataRaw = _schema.client_DH_inner_data(new
            {
                nonce        = _serverDhInnerData["nonce"],
                server_nonce = _serverDhInnerData["server_nonce"],
                retry_id     = 0L,
                g_b          = gb.ToByteArrayUnsigned()
            }).Serialize();
            byte[] clientDhInnerDataHash;

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            using (var sha    = SHA1.Create())
            {
                writer.Write(sha.ComputeHash(clientDhInnerDataRaw));
                writer.Write(clientDhInnerDataRaw);

                clientDhInnerDataHash = memory.ToArray();
            }

            Logger.Log(Logger.Level.Debug, $"Sending ClientDHRequest");

            // Get ready to the next response
            _currentState = AuthenticationState.ClientDhRequest;

            _mtSender.Send(_schema.set_client_DH_params(new
            {
                nonce = _serverDhInnerData["nonce"],
                server_nonce = _serverDhInnerData["server_nonce"],
                encrypted_data = Aes.EncryptAes(key, clientDhInnerDataHash)
            }));
        }
    }
}
