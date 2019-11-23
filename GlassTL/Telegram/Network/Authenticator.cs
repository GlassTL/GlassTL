using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.MTProto.Crypto;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.Network
{
    public class ServerAuthentication
    {
        public AuthKey AuthKey { get; set; }
        public int TimeOffset { get; set; }
    }

    public class Authenticator
    {
        private enum AuthenticationState
        {
            NotStarted,
            PQRequest,
            ServerDHRequest,
            ClientDHRequest
        }

        private readonly dynamic schema = new TLSchema();
        private AuthenticationState CurrentState = AuthenticationState.NotStarted;
        private readonly MTProtoPlainSender MTSender = null;

        private TLObject PQInnerData = null;
        private TLObject ServerDHInnerData = null;
        private BigInteger gab = null;
        private int? TimeOffset = null;

        private TaskCompletionSource<ServerAuthentication> Response = null;

        public Authenticator(MTProtoPlainSender sender)
        {
            // Make sure a connection is provided
            if (sender == null || sender.Connection == null || !sender.Connection.IsConnected)
            {
                throw new ArgumentException("The connection to secure is invalid", nameof(sender));
            }

            // Save the connection
            MTSender = sender;

            // Subscribe to the event... maybe?
            MTSender.TLObjectReceivedEvent += Sender_TLObjectReceivedEvent;
            Logger.Log(Logger.Level.Debug, $"Subscribed to {nameof(MTSender.TLObjectReceivedEvent)} event");

            Logger.Log(Logger.Level.Info, $"Authenticator created");
        }

        public Task<ServerAuthentication> DoAuthentication()
        {
            // Make sure we aren't securing a connection twice
            if (Response != null) throw new Exception("An authentication is either already created or is being created.");

            Logger.Log(Logger.Level.Info, $"Starting server authentication");

            // Prepare a response for when we are authenticated
            Response = new TaskCompletionSource<ServerAuthentication>();
            // We are currently requesting a PQ from the server
            CurrentState = AuthenticationState.PQRequest;

            // Start the authorization process by requesting a PQ from the server
            Logger.Log(Logger.Level.Debug, $"Sending request for PQ");
            MTSender.Send(schema.req_pq_multi(new
            {
                nonce = Helpers.GenerateRandomBytes(16)
            }));

            // Return the Task.  The task can be awaited and when the connection
            // is secure, it will be resolved with the correct information
            return Response.Task;
        }

        private void Sender_TLObjectReceivedEvent(object sender, TLObjectEventArgs e)
        {
            // Ignore any data that we aren't expecting
            if (CurrentState == AuthenticationState.NotStarted)
            {
                Logger.Log(Logger.Level.Debug, $"Received authentication data unexpectedly.  Skipping.  Try restarting the connection.");
                return;
            }
            else if (MTSender == null)
            {
                // How did we get here if there's no sender?  Hmm?
                var sad = new Exception("Unable to find a connection to Telegram.  Skipping.  Try restarting the connection.");
                Logger.Log(sad);
                Response.TrySetException(sad);
                CurrentState = AuthenticationState.NotStarted;
                return;
            }

            switch (CurrentState)
            {
                case AuthenticationState.PQRequest:
                    // Deserialize the data
                    var ResPQ = e.TLObject;

                    Logger.Log(Logger.Level.Debug, $"Received TLObject {ResPQ["_"]}.");

                    // Factorize the PQ into two prime factors and fail if we can't
                    if (!Helpers.FindPQ((byte[])ResPQ["pq"], out JToken factorizedPair))
                    {
                        var sad = new Exception($"Unable to find any valid factors of PQ: {ResPQ["pq"]}");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    // Compile the PQ information
                    PQInnerData = schema.p_q_inner_data(new
                    {
                        pq = factorizedPair["pq"],
                        p = factorizedPair["min"],
                        q = factorizedPair["max"],
                        nonce = ResPQ["nonce"],
                        server_nonce = ResPQ["server_nonce"],
                        new_nonce = Helpers.GenerateRandomBytes(32)
                    });

                    // Encrypt the PQ information.
                    // NOTE: Because the server responded with potentially more than one fingerprint,
                    // this also returns the fingerprint that we are using.
                    (var fingerpint, var EncryptedInnerData) = MTProto.Crypto.RSA.Encrypt(ResPQ["server_public_key_fingerprints"].ToObject<long[]>(), PQInnerData.Serialize());

                    Logger.Log(Logger.Level.Debug, $"Using Public RSA Key: {fingerpint}");

                    // Get ready for the next response
                    CurrentState = AuthenticationState.ServerDHRequest;

                    Logger.Log(Logger.Level.Debug, $"Submitting PQ factor to server");

                    // Compile the rest of the information and send to the server for grading
                    MTSender.Send(schema.req_DH_params(new
                    {
                        nonce = ResPQ["nonce"],
                        server_nonce = ResPQ["server_nonce"],
                        p = factorizedPair["min"],
                        q = factorizedPair["max"],
                        public_key_fingerprint = fingerpint,
                        encrypted_data = EncryptedInnerData
                    }));

                    break;
                case AuthenticationState.ServerDHRequest:
                    // Make sure we have all the needed info
                    if (PQInnerData == null)
                    {
                        var sad = new Exception("Unable to find the PQInnerData object from previous steps.  Please restart the connection process");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    // Deserialize the data
                    var ServerDHParams = e.TLObject;

                    Logger.Log(Logger.Level.Debug, $"Received TLObject {ServerDHParams["_"]}.");

                    // Determine the result
                    if ((string)ServerDHParams["_"] != "server_DH_params_ok")
                    {
                        // Aaaand we failed to handle things correctly
                        var sad = new Exception($"The server responded with a {ServerDHParams["_"]} response.  We are unable to continue.");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    Logger.Log(Logger.Level.Debug, $"Decrypting the DHInnerData");

                    var key = AES.GenerateKeyDataFromNonces((byte[])ServerDHParams["server_nonce"], (byte[])PQInnerData["new_nonce"]);
                    var plaintextAnswer = AES.DecryptAES(key, (byte[])ServerDHParams["encrypted_answer"]);

                    using (var memory = new MemoryStream(plaintextAnswer))
                    using (var reader = new BinaryReader(memory))
                    {
                        var hashsum = reader.ReadBytes(20);

                        ServerDHInnerData = TLObject.Deserialize(reader);
                    }

                    if (ServerDHInnerData == null)
                    {
                        var sad = new Exception("The server did not respond with valid DH Inner Data.  We are unable to continue.");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    if ((string)ServerDHInnerData["_"] != "server_DH_inner_data")
                    {
                        var sad = new Exception($"The server responded with a {ServerDHInnerData["_"]} response.  We are unable to continue.");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    Logger.Log(Logger.Level.Debug, $"Decrypted TLObject {ServerDHInnerData["_"]}.");

                    TimeOffset = (int)ServerDHInnerData["server_time"] - (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                    Logger.Log(Logger.Level.Debug, $"Updated Time Offset {TimeOffset}.");
                    Logger.Log(Logger.Level.Debug, $"Calculating GAB");

                    var b = new BigInteger(1, Helpers.GenerateRandomBytes(256));
                    var gb = BigInteger.ValueOf((int)ServerDHInnerData["g"]).ModPow(b, new BigInteger(1, (byte[])ServerDHInnerData["dh_prime"]));

                    gab = new BigInteger(1, (byte[])ServerDHInnerData["g_a"]).ModPow(b, new BigInteger(1, (byte[])ServerDHInnerData["dh_prime"]));

                    TLObject ClientDHInnerData = schema.client_DH_inner_data(new
                    {
                        nonce = ServerDHInnerData["nonce"],
                        server_nonce = ServerDHInnerData["server_nonce"],
                        retry_id = 0L,
                        g_b = gb.ToByteArrayUnsigned()
                    });

                    var ClientDHInnerData_raw = ClientDHInnerData.Serialize();
                    byte[] ClientDHInnerDataHash = null;

                    using (var memory = new MemoryStream())
                    using (var writer = new BinaryWriter(memory))
                    using (var sha = SHA1.Create())
                    {
                        writer.Write(sha.ComputeHash(ClientDHInnerData_raw));
                        writer.Write(ClientDHInnerData_raw);

                        ClientDHInnerDataHash = memory.ToArray();
                    }

                    Logger.Log(Logger.Level.Debug, $"Sending ClientDHRequest");

                    // Get ready to the next response
                    CurrentState = AuthenticationState.ClientDHRequest;

                    MTSender.Send(schema.set_client_DH_params(new
                    {
                        nonce = ServerDHInnerData["nonce"],
                        server_nonce = ServerDHInnerData["server_nonce"],
                        encrypted_data = AES.EncryptAES(key, ClientDHInnerDataHash)
                    }));

                    break;
                case AuthenticationState.ClientDHRequest:
                    // Make sure we have all the needed info
                    if (gab == null)
                    {
                        var sad = new Exception("Unable to find the GAB object from previous steps.  Please restart the connection process");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }
                    else if (TimeOffset == null)
                    {
                        var sad = new Exception("Unable to find the TimeOffset from previous steps.  Please restart the connection process");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    var SetClientDHParamsAnswer = e.TLObject;
                    var AuthKey = new AuthKey(gab);
                    var newNonceHashCalculated = AuthKey.CalcNewNonceHash((byte[])PQInnerData["new_nonce"], 1);

                    Logger.Log(Logger.Level.Debug, $"Received TLObject {SetClientDHParamsAnswer["_"]}.");

                    if (!((byte[])SetClientDHParamsAnswer["new_nonce_hash1"]).SequenceEqual(newNonceHashCalculated))
                    {
                        var sad = new Exception("The server returned an invalid new nonce hash 1.  Please restart the connection process");
                        Logger.Log(sad);
                        Response.TrySetException(sad);
                        MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
                        CurrentState = AuthenticationState.NotStarted;
                        Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(MTSender.TLObjectReceivedEvent)} event");
                        Logger.Log(Logger.Level.Info, $"Auth aborted.");
                        return;
                    }

                    Logger.Log(Logger.Level.Info, $"Successfully negotiated authorization with the server");

                    MTSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;

                    Response.TrySetResult(new ServerAuthentication
                    {
                        AuthKey = AuthKey,
                        TimeOffset = (int)TimeOffset
                    });

                    break;
            }
        }
    }
}
