namespace GlassTL.Telegram.Network.Authentication
{
    using System;
    using MTProto;
    using MTProto.Crypto;
    using Utils;

    public partial class Authenticator
    {
        private void HandleClientDhRequest(TLObject setClientDhParamsAnswer)
        {
            // Make sure we have all the needed info
            if (_gab == null)
            {
                HandleException(new Exception("Unable to find the GAB object from previous steps.  Please restart the connection process"));
                return;
            }
            
            if (_timeOffset == null)
            {
                HandleException(new Exception("Unable to find the TimeOffset from previous steps.  Please restart the connection process"));
                return;
            }

            var authKey = new AuthKey(_gab);
            var newNonceHashCalculated = authKey.CalcNewNonceHash(_pqInnerData.GetAs<byte[]>("new_nonce"), 1);

            Logger.Log(Logger.Level.Debug, $"Received TLObject {setClientDhParamsAnswer["_"]}.");

            if (!setClientDhParamsAnswer.GetAs<byte[]>("new_nonce_hash1").DirectSequenceEquals(newNonceHashCalculated))
            {
                HandleException(new Exception("The server returned an invalid new nonce hash 1.  Please restart the connection process"));
                return;
            }

            Logger.Log(Logger.Level.Info, $"Successfully negotiated authorization with the server");

            _mtSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;

            _response.TrySetResult(new ServerAuthentication
            {
                AuthKey = authKey,
                TimeOffset = (int)_timeOffset
            });
        }
    }
}
