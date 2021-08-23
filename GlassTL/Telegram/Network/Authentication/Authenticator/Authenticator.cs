namespace GlassTL.Telegram.Network.Authentication
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using EventArgs;
    using MTProto;
    using MTProto.Crypto;
    using Senders;
    using Utils;

    public partial class Authenticator
    {
        private readonly dynamic _schema = new TLSchema();
        private AuthenticationState _currentState = AuthenticationState.NotStarted;
        private readonly MTProtoPlainSender _mtSender;

        private TLObject _pqInnerData;
        private TLObject _serverDhInnerData;
        private BigInteger _gab;
        private int? _timeOffset;

        private TaskCompletionSource<ServerAuthentication> _response;

        public Authenticator([NotNull] MTProtoPlainSender sender)
        {
            // Make sure a connection is provided
            if (sender.Connection is not {IsConnected: true})
            {
                throw new ArgumentException("The connection to secure is invalid", nameof(sender));
            }

            // Save the connection
            _mtSender = sender;

            // Subscribe to the event... maybe?
            _mtSender.TLObjectReceivedEvent += Sender_TLObjectReceivedEvent;
            Logger.Log(Logger.Level.Debug, $"Subscribed to {nameof(_mtSender.TLObjectReceivedEvent)} event");

            Logger.Log(Logger.Level.Info, $"Authenticator created");
        }

        ~Authenticator()
        {
            _mtSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
        }

        public Task<ServerAuthentication> DoAuthentication()
        {
            // Make sure we aren't securing a connection twice
            if (_response != null) throw new Exception("An authentication is either already created or is being created.");

            Logger.Log(Logger.Level.Info, $"Starting server authentication");

            // Prepare a response for when we are authenticated
            _response = new TaskCompletionSource<ServerAuthentication>();
            // We are currently requesting a PQ from the server
            _currentState = AuthenticationState.PqRequest;

            // Start the authorization process by requesting a PQ from the server
            Logger.Log(Logger.Level.Debug, $"Sending request for PQ");
            _mtSender.Send(_schema.req_pq_multi(new
            {
                nonce = Helpers.GenerateRandomBytes(16)
            }));

            // Return the Task.  The task can be awaited and when the connection
            // is secure, it will be resolved with the correct information
            return _response.Task;
        }

        private void Sender_TLObjectReceivedEvent(object sender, TLObjectEventArgs e)
        {
            switch (_currentState)
            {
                case AuthenticationState.PqRequest:
                    HandlePqRequest(e.TLObject);
                    break;
                case AuthenticationState.ServerDhRequest:
                    HandleServerDhRequest(e.TLObject);
                    break;
                case AuthenticationState.ClientDhRequest:
                    HandleClientDhRequest(e.TLObject);
                    break;
                case AuthenticationState.NotStarted:
                    // Ignore any data that we aren't expecting
                    Logger.Log(Logger.Level.Debug, $"Received authentication data unexpectedly.  Skipping.  Try restarting the connection.");
                    break;
                default:
                    throw new Exception("Unknown MTProto Authentication State.");
            }
        }

        private void HandleException(Exception exception)
        {
            Logger.Log(exception);
            _response.TrySetException(exception);
            _mtSender.TLObjectReceivedEvent -= Sender_TLObjectReceivedEvent;
            _currentState = AuthenticationState.NotStarted;
            Logger.Log(Logger.Level.Debug, $"Unsubscribed from {nameof(_mtSender.TLObjectReceivedEvent)} event");
            Logger.Log(Logger.Level.Info, $"Auth aborted.");
        }
    }
}
