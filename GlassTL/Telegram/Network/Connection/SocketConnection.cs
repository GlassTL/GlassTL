namespace GlassTL.Telegram.Network.Connection
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using EventArgs;
    using Utils;

    /// <summary>
    /// An abstract class handling generic connections to Telegram
    /// </summary>
    public abstract class SocketConnection : IDisposable
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
        /// Raised call when byte data has become available from the server.
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceivedEvent;

        /// <summary>
        /// This property should be handled by <see cref="SocketConnection"/> subclasses
        /// </summary>
        public abstract Codec PacketCodec { get; }

        /// <summary>
        /// Gets the remote IP
        /// </summary>
        public string RemoteIp { get; }
        /// <summary>
        /// Gets the remote port
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets address information about the connection
        /// </summary>
        public AddressFamily Mode { get; internal set; } = AddressFamily.Unknown;

        /// <summary>
        /// Indicates whether or not the client is connected to the server.
        /// </summary>
        public bool IsConnected => ClientInstance?.Connected ?? false;
        #endregion

        #region Private-Members
        /// <summary>
        /// Gets or sets the socket wrapper for this connection
        /// </summary>
        private TcpClient ClientInstance { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the connection is already
        /// in the process of being disposed
        /// </summary>
        private bool Disposing { get; set; } // To detect redundant calls
        #endregion

        #region Constructors-and-Factories
        /// <summary>
        /// Initializes a new connection to a given address and port.
        /// 
        /// This will not establish the connection, however.
        /// </summary>
        /// <param name="ip">Indicates the remote IP we are connecting to</param>
        /// <param name="port">Indicates the remote Port we are connecting to</param>
        protected SocketConnection(string ip, int port)
        {
            Logger.Log(Logger.Level.Debug, "Attempting to create an underlying connection instance");

            try
            {
                // The IP and port must be valid
                if (string.IsNullOrEmpty(ip)) throw new ArgumentException("Null values are not supported", nameof(ip));
                if (port < 0) throw new ArgumentException("Negative values are not supported", nameof(port));

                // Save the information
                RemoteIp = ip;
                Port = port;

                Logger.Log(Logger.Level.Debug, "Underlying connection instance initialized");
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Severe, $"Failed to create the underlying connection instance.\n\n{ex.Message}");
                throw;
            }
        }
        #endregion

        #region Public-Methods
        /// <summary>
        /// Establishes a connection with the server.
        /// </summary>
        public void Connect(int connectionTimeout)
        {
            Logger.Log(Logger.Level.Info, "Attempting to connect using the underlying connection instance.");
            Logger.Log(Logger.Level.Debug, $"Timeout: {connectionTimeout}");

            try
            {
                // Attempt to parse the IP passed
                if (!IPAddress.TryParse(RemoteIp, out var address)) throw new Exception($"Invalid IP Address: {RemoteIp}");

                // Create a new instance and subscribe to the events
                ClientInstance = new TcpClient();

                ClientInstance.ConnectedEvent    += ClientInstance_ConnectedEvent;
                ClientInstance.DataReceivedEvent += ClientInstance_DataReceivedEvent;
                ClientInstance.DisconnectedEvent += ClientInstance_DisconnectedEvent;

                Logger.Log(Logger.Level.Debug, $"Subscribed to events from the socket wrapper");

                // Perform the action connection
                ClientInstance.Connect(address, Port, connectionTimeout);

                // Allow implementations to init the connection
                InitConnection(ClientInstance);
                
                // Save information about the address
                Mode = address.AddressFamily;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Severe, $"Failed to connect using the underlying connection.\n\n{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends a packet of data through this connection mode.
        /// </summary>
        /// <param name="data"></param>
        public async Task Send(byte[] data)
        {
            try
            {
                Logger.Log(Logger.Level.Info, "Attempting to send packet using the underlying connection instance");

                // Make sure the packet is valid
                if (data == null || data.Length == 0) throw new ArgumentNullException(nameof(data));

                Logger.Log(Logger.Level.Debug, $"\tUnserialized Length: {data.Length}");

                // Allow implementations to serialize the data according to the transport specs 
                var outgoing = SerializePacket(data)
                    ?? throw new Exception("Serializing the packet resulted in a null value.");

                Logger.Log(Logger.Level.Debug, $"\tSerialized Length: {outgoing.Length}");

                // Send the data using the socket wrapper
                await ClientInstance.Send(outgoing);

                Logger.Log(Logger.Level.Info, $"Sent {outgoing.Length} bytes successfully");
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Sending failed.\n\n{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// In the case that the connection needs to be initialized by sending data, this
        /// method allows implementations to be able to do so.
        /// </summary>
        /// <param name="client">The underlying <see cref="TcpClient"/> which represents the connection</param>
        protected abstract Task InitConnection(TcpClient client);
        /// <summary>
        /// The method should be handled by <see cref="SocketConnection"/> subclasses
        /// </summary>
        /// <param name="packet">The raw packed to serialize</param>
        /// <returns>The serialized data</returns>
        protected abstract byte[] SerializePacket(byte[] packet);
        /// <summary>
        /// The method should be handled by <see cref="SocketConnection"/> subclasses
        /// </summary>
        /// <param name="packet">The raw packed to deserialize</param>
        /// <returns>The raw data</returns>
        protected abstract byte[] DeserializePacket(byte[] packet);

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public void Disconnect()
        {
            Logger.Log(Logger.Level.Info, "Disconnecting and disposing of the socket wrapper.");

            // Disconnect the socket wrapper
            ClientInstance.Disconnect();

            // Unsubscribe from the events
            ClientInstance.ConnectedEvent    -= ClientInstance_ConnectedEvent;
            ClientInstance.DataReceivedEvent -= ClientInstance_DataReceivedEvent;
            ClientInstance.DisconnectedEvent -= ClientInstance_DisconnectedEvent;
        }

        /// <summary>
        /// Disconnects and disposes of the connection
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing)
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private-Methods
        /// <summary>
        /// Raised by the socket wrapper when a disconnection is detected
        /// </summary>
        private void ClientInstance_DisconnectedEvent(object sender, EventArgs e)
        {
            Logger.Log(Logger.Level.Info, "Disconnection detected.  Passing on...");

            // Pass the event on
            DisconnectedEvent.RaiseEventSafe(sender, EventArgs.Empty);
        }
        /// <summary>
        /// Raised by the socket wrapper when byte data is received from the server
        /// </summary>
        private void ClientInstance_DataReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            try
            {
                Logger.Log(Logger.Level.Info, "A packet was received from the server");

                // Make sure the packet is valid
                if (e.GetData() == null || e.GetData().Length == 0) throw new Exception("A null packet was received.  Skipping");

                Logger.Log(Logger.Level.Debug, $"\tSerialized Packet Length: {e.GetData().Length}");

                // Using the overridden method (hopefully), deserialize
                var deserialized = DeserializePacket(e.GetData())??
                    throw new Exception("Deserializing the packet resulted in a null value.  Skipping");

                Logger.Log(Logger.Level.Debug, $"\tDeserialized Packet Length: {deserialized.Length}");
                Logger.Log(Logger.Level.Debug, $"Raising the {nameof(DataReceivedEvent)} event");

                // Pass the data on
                DataReceivedEvent.RaiseEventSafe(sender, new DataReceivedEventArgs(deserialized));
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while handling the data received.\n\n{ex.Message}");
            }

        }
        /// <summary>
        /// Raised by the socket wrapper when a connection is successfully made
        /// </summary>
        private void ClientInstance_ConnectedEvent(object sender, EventArgs e)
        {
            Logger.Log(Logger.Level.Info, "Connection to the server made successfully.  Passing on....");

            // Pass the event on
            ConnectedEvent.RaiseEventSafe(sender, e);
        }

        /// <summary>
        /// Performs the actual disposing.
        /// </summary>
        /// <param name="managed">True if disposing managed resources.  Otherwise, false.</param>
        private void Dispose(bool managed)
        {
            // Only dispose once
            if (Disposing) return;

            Disposing = true;

            if (!managed) return;
            
            // Disconnect and unsubscribe from the events
            Disconnect();

            // Dispose of the socket wrapper
            ClientInstance.Dispose();
        }
        #endregion
    }
}

