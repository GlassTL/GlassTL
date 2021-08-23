namespace GlassTL.Telegram.Network.Authentication
{
    using System;
    using MTProto;
    using MTProto.Crypto.RSA;
    using Utils;

    public partial class Authenticator
    {
        private void HandlePqRequest(TLObject resPq)
        {
            Logger.Log(Logger.Level.Debug, $"Received TLObject {resPq["_"]}.");

            // Factorize the PQ into two prime factors and fail if we can't
            if (!Helpers.FindPq(resPq.GetAs<byte[]>("pq"), out var factorizedPair))
            {
                HandleException(new Exception($"Unable to find any valid factors of PQ: {resPq["pq"]}"));
                return;
            }

            // Compile the PQ information
            _pqInnerData = _schema.p_q_inner_data(new
            {
                pq = factorizedPair["pq"],
                p = factorizedPair["min"],
                q = factorizedPair["max"],
                nonce = resPq["nonce"],
                server_nonce = resPq["server_nonce"],
                new_nonce = Helpers.GenerateRandomBytes(32)
            });

            // Encrypt the PQ information.
            var encrypter = Rsa.Encrypt(resPq.GetAs<long[]>("server_public_key_fingerprints"));
            var encryptedInnerData = encrypter.Encrypt(_pqInnerData.Serialize());

            Logger.Log(Logger.Level.Debug, $"Using Public RSA Key: {encrypter.Fingerprint}");

            // Get ready for the next response
            _currentState = AuthenticationState.ServerDhRequest;

            Logger.Log(Logger.Level.Debug, $"Submitting PQ factor to server");

            // Compile the rest of the information and send to the server for grading
            _mtSender.Send(_schema.req_DH_params(new
            {
                nonce = resPq["nonce"],
                server_nonce = resPq["server_nonce"],
                p = factorizedPair["min"],
                q = factorizedPair["max"],
                public_key_fingerprint = encrypter.Fingerprint,
                encrypted_data = encryptedInnerData
            }));
        }
    }
}
