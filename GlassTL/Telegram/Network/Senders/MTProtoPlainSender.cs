namespace GlassTL.Telegram.Network.Senders
{
    using System;
    using System.IO;
    using MTProto;
    using Utils;
    using System.Threading.Tasks;
    using EventArgs;
    using Connection;

    /// <summary>
    /// MTProto Mobile Protocol plain sender
    /// (https://core.telegram.org/mtproto/description#unencrypted-messages)
    /// </summary>
    public sealed class MTProtoPlainSender : IDisposable
    {
        #region Public-Members
        /// <summary>
        /// Raised when the connection is established.
        /// </summary>
        public event EventHandler ConnectedEvent;
        /// <summary>
        /// Raised when the connection is destroyed.
        /// </summary>
        public event EventHandler DisconnectedEvent;
        /// <summary>
        /// Raised when a TLObject has become available from the server.
        /// </summary>
        public event EventHandler<TLObjectEventArgs> TLObjectReceivedEvent;

        /// <summary>
        /// Gets the underlying connection used by this sender
        /// </summary>
        public SocketConnection Connection { get; private set; } = null;
        #endregion

        #region Private-Members
        /// <summary>
        /// Provides functionality for Message ID generation and more.
        /// </summary>
        private MTProtoHelper State { get; } = new MTProtoHelper(null);
        #endregion

        #region Events
        /// <summary>
        /// Raised by the underlying <see cref="Connection"/> when byte data is received
        /// </summary>
        private void Connection_DataReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            try
            {
                // Attempt to handle the data correctly
                using var memoryStream = new MemoryStream(e.GetData());
                using var binaryReader = new BinaryReader(memoryStream);

                if (memoryStream.Capacity < 8) throw new Exception("The data received from the server is not valid.  Skipping...");

                var authKeyId = binaryReader.ReadInt64();
                if (authKeyId != 0) throw new Exception($"The value \"{authKeyId}\" is not a valid {nameof(authKeyId)}. Expected \"0\".  Skipping...");

                var messageId = binaryReader.ReadInt64();
                if (messageId <= 0) throw new Exception($"The value \"{messageId}\" is not a valid {nameof(messageId)}. Expected positive, non-zero value.  Skipping...");

                var messageLength = binaryReader.ReadInt32();
                if (messageLength <= 0) throw new Exception($"The value \"{messageLength}\" is not a valid {nameof(messageLength)}. Expected positive, non-zero value.  Skipping...");

                var tlObject = new TLObject(binaryReader)
                    ?? throw new Exception("Unable to parse the data as a valid TLObject.  Skipping...");

                // Raise the event
                TLObjectReceivedEvent.RaiseEventSafe(sender, new TLObjectEventArgs(tlObject));
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"The packed received from the server doesn't appear to be valid.\n\n{ex.Message}");
            }
        }
        /// <summary>
        /// Raised by the underlying <see cref="Connection"/> when a connection is destroyed
        /// </summary>
        private void Connection_DisconnectedEvent(object sender, EventArgs e)
        {
            // Just pass on the event along
            DisconnectedEvent.RaiseEventSafe(sender, e);
        }
        /// <summary>
        /// Raised by the underlying <see cref="Connection"/> when a connection is established
        /// </summary>
        private void Connection_ConnectedEvent(object sender, EventArgs e)
        {
            // Just pass on the event along
            ConnectedEvent.RaiseEventSafe(sender, e);
        }
        #endregion

        #region Constructors-and-Factories
        /// <summary>
        /// Initializes the MTProto plain sender.
        /// </summary>
        /// <param name="connection">The Connection to be used.</param>
        public MTProtoPlainSender(SocketConnection connection)
        {
            // Cannot have a null value as a connection
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));

            // Subscribe to all the events
            Connection.ConnectedEvent += Connection_ConnectedEvent;
            Connection.DisconnectedEvent += Connection_DisconnectedEvent;
            Connection.DataReceivedEvent += Connection_DataReceivedEvent;
        }
        #endregion

        #region Public-Methods
        /// <summary>
        /// Sends a TLObject through the connection.  Subscribe to <see cref="TLObjectReceivedEvent"/> so that you will get the response.
        /// </summary>
        /// <param name="TLObject">The TLObject to send</param>
        public async Task Send(TLObject TLObject)
        {
            // Can't send a non-object
            if (TLObject == null) throw new ArgumentNullException(nameof(TLObject));

            // Get the byte representation of the TLObject
            var request = TLObject.Serialize();

            using var memoryStream = new MemoryStream(8 + 8 + 4 + request.Length);
            using var binaryWriter = new BinaryWriter(memoryStream);

            // auth_key_id = 0 (64 bits) which means that there is no auth_key
            binaryWriter.Write(0L);
            // message_id
            binaryWriter.Write(State.GetNewMessageID());
            // message_data_length
            binaryWriter.Write(request.Length);
            // message_data
            binaryWriter.Write(request);

            await Connection.Send(memoryStream.ToArray());
        }

        /// <summary>
        /// Unsubscribes from events.
        /// 
        /// Note: This does not dispose of the connection since
        /// it is expected that the connection will be reused.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from the events
            Connection.ConnectedEvent -= Connection_ConnectedEvent;
            Connection.DisconnectedEvent -= Connection_DisconnectedEvent;
            Connection.DataReceivedEvent -= Connection_DataReceivedEvent;
        }
        #endregion
    }
}
