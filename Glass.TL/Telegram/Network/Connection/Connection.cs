using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.Network
{
    public enum Codec
    {
        Unkown = -1,
        /// <summary>
        /// Default Telegram mode. Sends 12 additional bytes and
        /// needs to calculate the CRC value of the packet itself.
        /// </summary>
        FullPacketCodec
    }

    /// <summary>
    /// The <see cref="Connection"/> class is a wrapper around <see cref="TcpClient"/>.
    /// Subclasses will implement different transport modes as atomic operations,
    /// which this class eases doing since the exposed interface simply puts and
    /// gets complete data payloads to and from queues.
    /// </summary>
    public abstract class Connection : IDisposable
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
        /// This property should be handled by <see cref="Connection"/> subclasses
        /// </summary>
        public abstract Codec PacketCodec { get; }

        /// <summary>
        /// Gets the remote IP
        /// </summary>
        public string RemoteIP { get; internal set; } = "";
        /// <summary>
        /// Gets the remote port
        /// </summary>
        public int Port { get; internal set; } = -1;

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
        internal TcpClient ClientInstance { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the connection is already
        /// in the process of being disposed
        /// </summary>
        private bool Disposing { get; set; } = false; // To detect redundant calls
        #endregion

        #region Constructors-and-Factories
        /// <summary>
        /// Initializes a new connection to a given address and port.
        /// 
        /// This will not establish the connection, however.
        /// </summary>
        /// <param name="IP">Indicates the remote IP we are connecting to</param>
        /// <param name="Port">Indicates the remote Port we are connecting to</param>
        protected Connection(string IP, int Port)
        {
            Logger.Log(Logger.Level.Debug, "Attempting to create an underlying connection instance");

            try
            {
                // The IP and port must be valid
                if (string.IsNullOrEmpty(IP))
                {
                    throw new ArgumentException("Null values are not supported", nameof(IP));
                }
                else if (Port < 0)
                {
                    throw new ArgumentException("Negatative values are not supported", nameof(Port));
                }

                // Save the information
                RemoteIP = IP;
                this.Port = Port;

                Logger.Log(Logger.Level.Debug, "Underlying connection instance initualized");
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
        public void Connect(int ConnectionTimeout)
        {
            Logger.Log(Logger.Level.Info, "Attempting to connect using the underlying connection instance.");
            Logger.Log(Logger.Level.Debug, $"Timeout: {ConnectionTimeout}");

            try
            {
                // Attempt to parse the IP passed
                if (!IPAddress.TryParse(RemoteIP, out IPAddress Address)) throw new Exception($"Invalid IP Address: {RemoteIP}");

                // Create a new instance and subscribe to the events
                ClientInstance = new TcpClient();

                ClientInstance.ConnectedEvent += ClientInstance_ConnectedEvent;
                ClientInstance.DataReceivedEvent += ClientInstance_DataReceivedEvent;
                ClientInstance.DisconnectedEvent += ClientInstance_DisconnectedEvent;

                Logger.Log(Logger.Level.Debug, $"Subscribed to events from the socket wrapper");

                // Perform the action connection
                ClientInstance.Connect(Address, Port, ConnectionTimeout);

                // Save information about the address
                Mode = Address.AddressFamily;
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

                // Using the overridden method (hopefully), serialize
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
        /// The method should be handled by <see cref="Connection"/> subclasses
        /// </summary>
        /// <param name="Packet">The raw packed to serialize</param>
        /// <returns>The serialized data</returns>
        public abstract byte[] SerializePacket(byte[] Packet);
        /// <summary>
        /// The method should be handled by <see cref="Connection"/> subclasses
        /// </summary>
        /// <param name="Packet">The raw packed to deserialize</param>
        /// <returns>The raw data</returns>
        public abstract byte[] DeserializePacket(byte[] Packet);

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public void Disconnect()
        {
            Logger.Log(Logger.Level.Info, "Disconnecting and disposing of the socket wrapper.");

            // Disconnect the socket wrapper
            ClientInstance.Disconnect();

            // Unsubscribe from the events
            ClientInstance.ConnectedEvent -= ClientInstance_ConnectedEvent;
            ClientInstance.DataReceivedEvent -= ClientInstance_DataReceivedEvent;
            ClientInstance.DisconnectedEvent -= ClientInstance_DisconnectedEvent;
        }

        /// <summary>
        /// Disconnects and disposes of the connection
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
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
            var args = new object[] { sender, EventArgs.Empty };
            DisconnectedEvent.RaiseEventSafe(ref args);
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
                if (e.Data == null || e.Data.Length == 0) throw new Exception("A null packet was received.  Skipping");

                Logger.Log(Logger.Level.Debug, $"\tSerialized Packet Length: {e.Data.Length}");

                // Using the overridden method (hopefully), deserialize
                var deserialized = DeserializePacket(e.Data)
                    ?? throw new Exception("Deserializing the packet resulted in a null value.  Skipping");

                Logger.Log(Logger.Level.Debug, $"\tDeserialized Packet Length: {deserialized.Length}");
                Logger.Log(Logger.Level.Debug, $"Raising the {nameof(DataReceivedEvent)} event");

                // Pass the data on
                var args = new object[] { sender, new DataReceivedEventArgs(deserialized) };
                DataReceivedEvent.RaiseEventSafe(ref args);
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
            var args = new object[] { sender, e };
            ConnectedEvent.RaiseEventSafe(ref args);
        }

        /// <summary>
        /// Performs the actual disposing.
        /// </summary>
        /// <param name="Managed">True if disposing managed resources.  Otherise, false.</param>
        protected virtual void Dispose(bool Managed)
        {
            // Only dispose once
            if (Disposing) return;

            if (!Managed)
            {

            }

            // Disconnect and unsubscribe from the events
            Disconnect();

            // Dispose of the socket wrapper
            ClientInstance.Dispose();

            Disposing = true;
        }
        #endregion
    }
}
