using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.Network
{
    /// <summary>
    /// TCP client with events.  
    /// Set the Connected, Disconnected, and DataReceived callbacks.  
    /// Once set, use Connect() to connect to the server.
    /// </summary>
    public sealed class TcpClient
    {
        #region Public-Members
        /// <summary>
        /// Raised when a connection to the remote socket is established.
        /// </summary>
        public event EventHandler ConnectedEvent;
        /// <summary>
        /// Raised when the connection to the remote socket is destroyed.
        /// </summary>
        public event EventHandler DisconnectedEvent;
        /// <summary>
        /// Raied when byte data has become available from the server.
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceivedEvent;

        /// <summary>
        /// Indicates whether or not the client is connected to the server.
        /// </summary>
        public bool Connected { get; private set; } = false;

        /// <summary>
        /// Gets the remote IP
        /// </summary>
        public IPAddress RemoteIP { get; private set; }

        /// <summary>
        /// Gets the remote port
        /// </summary>
        public int Port { get; private set; }
        #endregion

        #region Private-Members
        private System.Net.Sockets.TcpClient Client { get; set; } = null;
        private NetworkStream NetworkStream { get; set; }

        private CancellationTokenSource TokenSource { get; set; } = null;
        private CancellationToken Token => TokenSource?.Token ?? CancellationToken.None;
        private SemaphoreSlim SemaphoreSlim { get; } = new SemaphoreSlim(1, 1);

        private Task DataReceiverLoop = null;
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        public TcpClient() { }

        #endregion

        #region Public-Methods
        public void Disconnect()
        {
            if (!Connected)
            {
                Logger.Log(Logger.Level.Info, "A disconnection request was made while already disconnected.  Skipping.");
                return;
            }

            Logger.Log(Logger.Level.Info, "Attempting to disconnect from the remote socket.");

            Connected = false;

            if (TokenSource != null)
            {
                Logger.Log(Logger.Level.Debug, "Cancelling and disposing of TokenSource");
                if (!TokenSource.IsCancellationRequested) TokenSource.Cancel();
                TokenSource.Dispose();
                TokenSource = null;
            }
            else
            {
                Logger.Log(Logger.Level.Debug, "TokenSource already cancelled.  Skipping.");
            }

            if (NetworkStream != null)
            {
                // Network stream must be disconnected and disposed separately
                Logger.Log(Logger.Level.Debug, "Closing and disposing of network stream");
                NetworkStream.Close();
                NetworkStream.Dispose();
                NetworkStream = null;
            }
            else
            {
                Logger.Log(Logger.Level.Debug, "Network stream already closed.  Skipping.");
            }

            if (Client != null)
            {
                Logger.Log(Logger.Level.Debug, "Closing and disposing of socket instance");
                Client.Close();
                Client.Dispose();
                Client = null;
            }
            else
            {
                Logger.Log(Logger.Level.Debug, "Socket instance already closed.  Skipping");
            }

            Logger.Log(Logger.Level.Info, "Disconnected from the remote socket");
        }
        /// <summary>
        /// Dispose of the TCP client.
        /// </summary>
        public void Dispose()
        {
            Logger.Log(Logger.Level.Info, "Disposing of socket instance.");

            // Disconnect handles task cancellation and disposing of each object
            Disconnect();

            Logger.Log(Logger.Level.Info, "Disposed");
        }

        /// <summary>
        /// Establish the connection to the server.
        /// </summary>
        /// <param name="RemoteIP">The server IP address.</param>
        /// <param name="Port">The TCP port on which to connect.</param>
        /// <param name="ConnectionTimeout">The timeout for this operation in milliseonds</param>
        public void Connect(IPAddress RemoteIP, int Port, int ConnectionTimeout = 5000)
        {
            Logger.Log(Logger.Level.Info, "Attempting connection to the remote socket");

            try
            {
                if (Port < 0) throw new ArgumentException("Negative values not supported.", nameof(Port));
                if (ConnectionTimeout < 0) throw new ArgumentException("ConnectionTimeout must be zero or greater.", nameof(ConnectionTimeout));

                this.RemoteIP = RemoteIP ?? throw new ArgumentNullException("Null values are not supported", nameof(RemoteIP));
                this.Port = Port;

                TokenSource = new CancellationTokenSource();
                Client = new System.Net.Sockets.TcpClient();

                Logger.Log(Logger.Level.Debug, $"Timeout: {ConnectionTimeout}");
                Logger.Log(Logger.Level.Debug, "Beginning socket connection");

                var ar = Client.BeginConnect(RemoteIP, Port, null, null);

                try
                {
                    if (!ar.AsyncWaitHandle.WaitOne(ConnectionTimeout, false))
                    {
                        Client.Close();
                        throw new TimeoutException($"Timeout connecting to {this.RemoteIP}:{this.Port}");
                    }

                    Client.EndConnect(ar);

                    Logger.Log(Logger.Level.Info, "Connection to remote socket succeeded");

                    NetworkStream = Client.GetStream();
                    Connected = true;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    ar.AsyncWaitHandle.Close();
                }

                Logger.Log(Logger.Level.Info, $"Raising the {nameof(ConnectedEvent)} event");

                var args = new object[] { this, EventArgs.Empty };
                ConnectedEvent.RaiseEventSafe(ref args);

                Logger.Log(Logger.Level.Info, $"Starting socket monitoring");

                DataReceiverLoop = Task.Run(() => DataReceiver(Token), Token);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Failed to connect to the remote socket.\n\n{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Send data to the server.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        public async Task Send(byte[] data)
        {
            Logger.Log(Logger.Level.Info, $"Attempting to send data on the socket level");

            if (data == null || data.Length < 1) throw new ArgumentNullException("Cannot send null info.  Skipping.", nameof(data));
            if (!Connected) throw new IOException("Not connected to the server");

            Logger.Log(Logger.Level.Debug, $"Waiting for the socket to be available");

            // Asynchronously wait to enter the Semaphore.
            // If no one has been granted access to the Semaphore,
            // code execution will proceed, otherwise this thread
            // waits here until the semaphore is released 
            await SemaphoreSlim.WaitAsync();

            Logger.Log(Logger.Level.Info, $"Obtained lock on socket.");
            Logger.Log(Logger.Level.Debug, $"Sending {data.Length} bytes");

            try
            {
                await NetworkStream.WriteAsync(data, 0, data.Length);
                NetworkStream.Flush();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Failed to send data through the socket.\n\n{ex.Message}");
                throw;
            }
            finally
            {
                Logger.Log(Logger.Level.Debug, $"Releasing lock on socket");

                // When the task is ready, release the semaphore.
                // It is vital to ALWAYS release the semaphore
                // when we are ready, or else we will end up with
                // a Semaphore that is forever locked. This is
                // why it is important to do the Release within a
                // try...finally clause; program execution may
                // crash or take a different path, this way we
                // are guaranteed execution
                SemaphoreSlim.Release();
            }
        }
        #endregion

        #region Private-Methods

        /// <summary>
        /// Loop that listens for data and raises events when data is received.
        /// </summary>
        private async Task DataReceiver(CancellationToken token)
        {
            Logger.Log(Logger.Level.Debug, $"Currently monitoring socket for incoming data");

            try
            {
                // Loop forever.  That's a long time
                while (true)
                {
                    // Determine if we can loop
                    if (token.IsCancellationRequested || Client == null || !Client.Connected)
                    {
                        Logger.Log(Logger.Level.Debug, $"Halting socket monitoring...");
                        break;
                    }

                    // Read data.  This should not return until data is received
                    byte[] data = await DataReadAsync(token);

                    // Obviously, if there's no data, there's an issue
                    if (data == null)
                    {
                        Logger.Log(Logger.Level.Warning, $"Read null bytes from the socket.  Skipping...");
                        // Wait for a bit and try again
                        await Task.Delay(30);
                        continue;
                    }

                    Logger.Log(Logger.Level.Debug, $"Read {data.Length} bytes from the socket.  Raising events.");

                    // Raise the event unawaited so that we can keep looping in case more data comes in
                    _ = Task.Run(() =>
                    {
                        var args = new object[] { this, new DataReceivedEventArgs(data) };
                        DataReceivedEvent.RaiseEventSafe(ref args);
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // We don't really care if the task was cancelled.
            }
            catch (OperationCanceledException)
            {
                // We don't really care if the task was cancelled.
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred monitoring the socket for data.\n\n{ex.Message}");
            }

            Logger.Log(Logger.Level.Debug, $"Raising the {nameof(DisconnectedEvent)} event");
            _ = Task.Run(() =>
            {
                var args = new object[] { this, EventArgs.Empty };
                DisconnectedEvent.RaiseEventSafe(ref args);
            });
        }
        /// <summary>
        /// Reads and returns a byte array when available from the socket
        /// </summary>
        private async Task<byte[]> DataReadAsync(CancellationToken token)
        {
            try
            {
                // Ensure that we the connection wasn't closed or cancelled
                if (Client == null || !Client.Connected || token.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                else if (!NetworkStream.CanRead)
                {
                    throw new IOException();
                }

                // Define a buffer to use
                var buffer = new byte[Client.ReceiveBufferSize];

                // We are using a memory stream to take in data.
                // ToDo: This is not to be used for files.  We still need to write something for that.
                using (var ms = new MemoryStream())
                {
                    // Loop while there is data available
                    do
                    {
                        // Read from the socket asynchronously.
                        // This returns when there's data available to read
                        int read = await NetworkStream.ReadAsync(buffer, 0, buffer.Length, token);

                        // If there's data, write it to the memory stream.
                        // If there's no data, it shouldn't return.  Throw an exception.
                        if (read > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        else
                        {
                            throw new SocketException();
                        }
                    } while (NetworkStream.DataAvailable);

                    // Return the data we received
                    return ms.ToArray();
                }
            }
            catch (TaskCanceledException)
            {
                // We don't really care if the task was cancelled.
                return default;
            }
            catch (ObjectDisposedException)
            {
                // We don't really care if the task was cancelled.
                return default;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                throw;
            }
        }

        #endregion
    }
}