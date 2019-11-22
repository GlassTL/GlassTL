﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.Extensions;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.Utils;
using GlassTL.Telegram.Exceptions;

namespace GlassTL.Telegram.Network
{
    public class MTProtoSender
    {
        #region Private-Members
        /// <summary>
        /// Since acks don't need to be sent right away, we have a handler to send any required
        /// acks on a regular schedule
        /// </summary>
        private Task AckHandler = null;
        private CancellationTokenSource AckCancellation = null;
        /// <summary>
        /// Since properties can't be passed as refs, this is used as the actual object
        /// </summary>
        private readonly MTProtoHelper _State = null;
        /// <summary>
        /// A dymamic reference to the TLSchema class
        /// </summary>
        private dynamic Schema { get; } = new TLSchema();
        /// <summary>
        /// A Thread-Safe Collection (FIFO) that attempts to package multiple requests together for easier transport to the server
        /// </summary>
        private MessagePacker SendQueue { get; } = null;
        /// <summary>
        /// A Thread-Safe Dictionary containing Message IDs and their respective requests until a response is received from the server
        /// 
        /// Note: this will not contain batches.  Only the individual requests
        /// </summary>
        private ConcurrentDictionary<long, RequestState> PendingQueue { get; } = new ConcurrentDictionary<long, RequestState>();
        /// <summary>
        /// A Thread-Safe List of Message IDs that we received and must be acknowledged.
        /// </summary>
        private ConcurrentBag<long> PendingAcks { get; } = new ConcurrentBag<long>();
        /// <summary>
        /// A Thread-Safe Buffer containing the last X number of acknowledged Message IDs.
        /// </summary>
        private ConcurrentCircularBuffer<RequestState> SentAcks { get; } = new ConcurrentCircularBuffer<RequestState>(10);
        #endregion

        #region Public-Members
        /// <summary>
        /// Provides Encryption, message ID handling, and more.
        /// </summary>
        public MTProtoHelper State => _State;
        /// <summary>
        /// The current connection to Telegram servers.
        /// </summary>
        public Connection Connection { get; private set; } = null;
        /// <summary>
        /// The number of times to retry processes should they fail.
        /// </summary>
        public int RetryCount { get; set; } = 5;
        /// <summary>
        /// The number of milliseconds a process should be delayed before reattempting after an error
        /// </summary>
        public int RetryDelay { get; set; } = 1;
        /// <summary>
        /// Indicates whether the reconnection process should be automatic should connection be lost or not
        /// </summary>
        public bool AutoReconnect { get; set; } = true;
        /// <summary>
        /// Indicates the number of seconds to wait before reattempting a connection.
        /// 
        /// Note: This includes negotiating auth information which may not be timely.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 10000;
        /// <summary>
        /// Indicates whether the communication with the server is established and authorized
        /// </summary>
        public bool CommunicationEstablished { get; private set; } = false;
        /// <summary>
        /// Indicates the connection status.
        /// 
        /// ToDo: We should probably add an enum with specific statuses besides just this
        /// </summary>
        public bool Reconnecting { get; private set; } = false;
        #endregion

        #region Public-Events
        /// <summary>
        /// Occurs when a new update is received from the server
        /// Note: This includes results from manual requests and
        /// is raised before the result is returned to the requestor.
        /// </summary>
        public event EventHandler<TLObjectEventArgs> UpdateReceivedEvent;
        #endregion

        #region Private-Events
        private async void Connection_DataReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            try
            {
                Logger.Log(Logger.Level.Info, "Attempting to deserialize data to a TLObject");
                if (!(State.DecryptMessageData(e.Data) is TLObject Message))
                {
                    Logger.Log(Logger.Level.Info, "The data could not be deserialized.  Skipping.");
                    return;
                }

                Logger.Log(Logger.Level.Info, $"Processing TLObject: {Message["_"]}");
                await ProcessUpdate(Message);

                var unawaited = Task.Run(async () => await ProcessSendQueue());
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while handling the received update.  Skipping\n\n{ex.Message}");
            }
        }
        #endregion

        #region Constructors-and-Factories
        /// <summary>
        /// Creates a new instance which handles the Mobile Transport Protocol.
        /// </summary>
        /// <param name="AuthInfo">A reference to the Authorization information.  Pass a null reference to have this generated for you</param>
        public MTProtoSender(ref MTProtoHelper Helper)
        {
            // If the Helper was a reference to a null value, create
            // a new Helper.  This will update the original as well
            if (Helper == null) Helper = new MTProtoHelper(null);
            // Save the reference to the Helper
            _State = Helper;
            // Create a new Thread Safe packer for all the requests
            SendQueue = new MessagePacker(ref _State);
        }
        #endregion

        #region Public-Methods
        /// <summary>
        /// Creates a new connection to the server using a given <see cref="Connection"/>
        /// </summary>
        public async Task Connect(Connection connection)
        {
            // If a connection is already made, there's no need to make a new one.
            if (CommunicationEstablished) return;

            // Save the connection
            Connection = connection;

            // Initialize the connection
            // This is done separately since we may need to reconnection without doing the above
            await InitConnect();
        }
        /// <summary>
        /// Cleanly disconnects the server.  Pending requests are placed back in a sending queue to be resent upon reconnection
        /// </summary>
        public void Disconnect()
        {
            // Close the connection superficially
            CommunicationEstablished = false;

            try
            {
                // Stop the ack processing loop.
                Logger.Log<MTProtoSender>(Logger.Level.Debug, $"Trying to stop Ack Handler");
                if (AckCancellation != null) AckCancellation.Cancel();
                if (AckHandler != null) AckHandler.Wait();

                // Close the connection to the server
                // ToDo: Should we also dispose?
                Connection.Disconnect();

                // Remove any event handlers
                Logger.Log<MTProtoSender>(Logger.Level.Debug, $"Removing event handlers");
                Connection.DataReceivedEvent -= Connection_DataReceivedEvent;

                // Place all the pending messages back in the queue to resend
                // Note: Leave pending acks alone.  We will pick up where we left off later
                PendingQueue.Values.ToList().ForEach(x => SendQueue.Add(x));
                PendingQueue.Clear();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while disconnecting.\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// Resets the server authentication which forces a new auth key to be generated.
        /// Disconnect before calling this method.
        /// </summary>
        public void ResetServerAuthentication()
        {
            // In order to prevent issues, we should be disconnected first before doing this.
            if (CommunicationEstablished) throw new Exception("You must be disconnected in order to reset the auth information");

            // There's no going back...
            State.AuthInfo.AuthKey = null;
        }

        /// <summary>
        /// Sends a request to the server asynchronously.  This method will either return the response (if applicable) or throw an exception (if applicable)
        /// </summary>
        /// <param name="request">The request to send to the server.</param>
        /// <returns>A Task that must be awaited to get a response.</returns>
        public Task<TLObject> Send(TLObject request)
        {
            // If we aren't even connected, there's no point in trying
            if (!CommunicationEstablished)
            {
                var sad = new RequestFailedException("Failed to send a request to the server because we are not connected", request);
                Logger.Log(sad);
                throw sad;
            }

            // Wrap the individual request in preparation to send
            var state = new RequestState(request);

            // Add to the send queue.
            SendQueue.Add(state);

            // Run un-awaited so as to let it process in the background.
            // This will take any and all messages out of the queue above
            // package them together in a container (if applicable) and
            // send to the server
            Task.Run(async () =>
            {
                Logger.Log(Logger.Level.Debug, "Starting send process");
                await ProcessSendQueue();
            });

            // Return the task associated with the request state.
            // When a response is received, the Task will
            // be resolved and the response returned
            return state.Response.Task;
        }
        /// <summary>
        /// Takes messages out of a queue and sends them to the server.
        /// </summary>
        public async Task ProcessSendQueue()
        {
            Logger.Log(Logger.Level.Debug, "Send process started");

            // Create a variable to store messages to send
            PackedMessage SendData = null;

            try
            {
                //// Don't run this time if there aren't any messages to acknowledge
                //if (PendingAcks.Any())
                //{
                //    // Take all the required acks out of the bag
                //    var acks = new List<long>();
                //    while (PendingAcks.TryTake(out var ack)) acks.Add(ack);

                //    Logger.Log(Logger.Level.Debug, $"Found {acks.Count} messages that need acknowledgement.  Adding them to the payload.");

                //    // Create a request that contains the list of acks
                //    var Acks_Request = new RequestState(Schema.msgs_ack(new { msg_ids = acks }));

                //    // Add the request to both the send queue (to be sent to the server) and
                //    // the sent acks queue (in case we need to resend.  We don't want to place
                //    // in pending since there shouldn't be a response.
                //    SendQueue.Add(Acks_Request);
                //    SentAcks.Put(Acks_Request);
                //}

                // Get messages if possible
                if ((SendData = SendQueue.Get()) == null)
                {
                    // If not, stop processing
                    Logger.Log(Logger.Level.Debug, $"There are no requests to send.");
                    return;
                }

                // Add all non-ack messages to a queue
                // Note: The queue contains messages that require a response
                // from the server.  Acks do not and, if added, would sit forever
                // ToDo: add support for AddRange
                SendData.Batch
                    .Where(x => (string)x.Request["_"] != "msgs_ack")
                    .ToList().ForEach(x => PendingQueue[x.MessageID] = x);

                var encrypted = State.EncryptMessageData(SendData.Data);
                var decrypted = State.DecryptMessageData(encrypted, true);

                // Send the messages to the server
                await Connection.Send(encrypted);

                Logger.Log(Logger.Level.Debug, $"There are now {PendingQueue.Count} pending requests");
            }
            catch (Exception ex)
            {
                var sad = new RequestFailedException("Sending the requests to Telegram failed", ex);
                Logger.Log(sad);

                // Determine if there were messages in context
                if (SendData != null)
                {
                    // If so, fail them with the same exception
                    Logger.Log(Logger.Level.Error, $"Failing the current requests\n\n{ex.Message}");
                    SendData.Batch.ToList().ForEach(x => x.Response.TrySetException(sad));
                }
            }
        }
        #endregion

        #region Private-Methods
        private async Task AckHandlerMethod(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Send every three minutes
                    await Task.Delay(3 * 60 * 1000, cancellationToken);

                    // Don't run this time if there aren't any messages to acknowledge
                    if (!PendingAcks.Any()) continue;

                    // Take all the required acks out of the bag
                    var acks = new List<long>();
                    while (PendingAcks.TryTake(out var ack)) acks.Add(ack);

                    Logger.Log(Logger.Level.Debug, $"Found {acks.Count} messages that need acknowledgement.  Adding them to the payload.");

                    // Create a request that contains the list of acks
                    var Acks_Request = new RequestState(Schema.msgs_ack(new { msg_ids = acks }));
                    
                    // Add the request to both the send queue (to be sent to the server) and
                    // the sent acks queue (in case we need to resend.  We don't want to place
                    // in pending since there shouldn't be a response.
                    SendQueue.Add(Acks_Request);
                    SentAcks.Put(Acks_Request);

                    // Send the acks to the server
                    // ToDo: If the user will be polling for updates, can we skip this line
                    // and let the acks be sent for us?
                    Task unawaited = Task.Run(() => ProcessSendQueue());
                }
                catch (TaskCanceledException)
                {
                    // We don't really care if the task was cancelled.
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // We don't really care if the task was cancelled.
                    break;
                }
                catch (Exception ex)
                {
                    // ToDo: Do we really want to skip all acks when this happens?  Likely we
                    // will encounter the same error again if we reprocess...
                    Logger.Log(Logger.Level.Error, $"An error occurred process acks.  Skipping.\n\n{ex.Message}");
                }

            }
        }
        /// <summary>
        /// Initializes an authorized connection to Telegram
        /// </summary>
        private async Task InitConnect()
        {
            Logger.Log(Logger.Level.Info, "Attempting connection");

            // Attempt to connect X number of times
            for (int i = 0; i < RetryCount; i++)
            {
                try
                {
                    if (i > 0) Logger.Log(Logger.Level.Info, "Reattempting connection");
                    Connection.Connect(ConnectionTimeout);
                    Logger.Log(Logger.Level.Info, $"Connection created after {i + 1} attempt(s)");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log(new ConnectionFailedException($"Connection attempt {i + 1} has failed.\n\n\t{ex.Message}", ex));
                    Logger.Log(Logger.Level.Info, $"Sleeping for {RetryDelay}ms");
                    await Task.Delay(RetryDelay);
                }
            }

            if (!Connection.IsConnected)
            {
                var sad = new ConnectionFailedException($"Failed to connect to Telegram {RetryCount} times in a row.");
                Logger.Log(sad);
                throw sad;
            }

            // Determine if we need to secure the connection
            if (State.AuthInfo.AuthKey == null)
            {
                Logger.Log(Logger.Level.Info, "Attempting to secure the connection");

                // Initialize a new instance of the plan sender based on the same connection
                using (var plain = new MTProtoPlainSender(Connection))
                {
                    // Attempt to authenticate X number of times
                    for (int i = 0; i < RetryCount; i++)
                    {
                        try
                        {
                            if (i > 0) Logger.Log(Logger.Level.Info, "Reattempting to secure the connection");
                            State.AuthInfo = await new Authenticator(plain).DoAuthentication().TimeoutAfter(ConnectionTimeout);
                            Logger.Log(Logger.Level.Info, $"Obtained a secure connection after {i + 1} attempt(s)");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(Logger.Level.Error, $"Securing attempt {i + 1} has failed.\n\n\t{ex.Message}");
                            Logger.Log(Logger.Level.Info, $"Sleeping for {RetryDelay}ms");
                            await Task.Delay(RetryDelay);
                        }
                    }
                }

                // Determine if the authorization was successful
                if (State.AuthInfo.AuthKey == null)
                {
                    var sad = new ConnectionFailedException($"Failed to secure the connection {RetryCount} times in a row.");
                    Logger.Log(sad);
                    Disconnect();
                    throw sad;
                }
            }

            // Get ourselves ready to handle requests
            CommunicationEstablished = true;

            Logger.Log<MTProtoSender>(Logger.Level.Debug, $"Attaching event handlers");
            Connection.DataReceivedEvent += Connection_DataReceivedEvent;

            Logger.Log<MTProtoSender>(Logger.Level.Debug, $"Starting Ack Handler");
            AckCancellation = new CancellationTokenSource();
            AckHandler = Task.Run(() => AckHandlerMethod(AckCancellation.Token));
        }

        /// <summary>
        /// Cleanly disconnects and then reconnects.
        /// </summary>
        private async Task Reconnect()
        {
            Connection.Disconnect();

            Reconnecting = false;

            State.Reset();

            await InitConnect();
        }

        /// <summary>
        /// Returns all messages sent given a matching Message ID.
        /// </summary>
        /// <param name="MsgID"></param>
        private RequestState[] GetStatesByID(long MsgID)
        {
            // In case the id referenced the message id
            if (PendingQueue.TryRemove(MsgID, out var state)) return new RequestState[] { state };

            // In case the id is a container id
            var FromContainer = PendingQueue.Values
                .Where(x => x.ContainerID == MsgID)
                .Select(x => GetStatesByID(x.MessageID))
                .Join();
            
            if (FromContainer.Count() > 0) return FromContainer;

            // In case the id refers to an ack
            var AlreadySend = SentAcks.Read()
                .Where(x => x.MessageID == MsgID)
                .ToArray();

            if (AlreadySend.Count() > 0) return AlreadySend;

            // When all else fails
            return null;
        }
        #endregion

        #region Response Handlers
        /// <summary>
        /// Attempts to handle the message on our side in the case it's a backend update
        /// </summary>
        /// <param name="Message">The update in question</param>
        /// <returns>True if the message was handled.  Otherwise, false.</returns>
        private async Task<bool> ProcessUpdate(TLObject Message)
        {
            Logger.Log(Logger.Level.Info, $"Beginning internal update processing of \"{(string)Message["body"]["_"]}\"");

            try
            {
                if (((int)Message["seqno"] & 0x01) != 0)
                {
                    Logger.Log(Logger.Level.Debug, $"Adding \"{(long)Message["msg_id"]}\" to be acked");
                    PendingAcks.Add((long)Message["msg_id"]);
                }

                var args = new object[] { this, new TLObjectEventArgs(new TLObject(Message["body"])) };
                UpdateReceivedEvent.RaiseEventSafe(ref args);

                return (Message["body"].Value<string>("_")) switch
                {
                    "bad_server_salt" => ProcessBadSalt(Message),
                    "bad_msg_notification" => ProcessBadMsgNotification(Message),
                    "future_salts" => ProcessFutureSalts(Message),
                    "gzip_packed" => await ProcessGzipPacked(Message),
                    "msg_container" => ProcessMessageContainer(Message),
                    "msg_detailed_info" => ProcessMessageDetailedInfo(Message),
                    "msg_new_detailed_info" => ProcessNewMessageDetailedInfo(Message),
                    "msg_resend_req" => ProcessMessageResendRequest(Message),
                    "msgs_ack" => ProcessMessageAck(Message),
                    "msgs_all_info" => ProcessMessageInfoAll(Message),
                    "msgs_state_req" => ProcessMessageStateReqest(Message),
                    "new_session_created" => ProcessNewSessionCreated(Message),
                    "pong" => ProcessPong(Message),
                    "rpc_result" => await ProcessRPCResult(Message),
                    _ => true,
                };
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while processing an update.  It has been skipped.\n\n{ex.Message}");
                return false;
            }
        }

        private bool ProcessBadSalt(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Info, $"Updating salt");

                // Update the Salt
                State.Salt = (long)Message["body"]["new_server_salt"];

                // Requeue the message(s) that triggered the error
                var StatesToResend = GetStatesByID((long)Message["body"]["bad_msg_id"])
                    ?? throw new Exception($"A message or container with id {(long)Message["body"]["bad_msg_id"]} could not found.  Skipping.");

                Logger.Log(Logger.Level.Debug, $"Identified {StatesToResend.Count()} message(s) to resend");

                StatesToResend.ToList().ForEach(x => {
                    var ID = x.ContainerID == -1L ? $"{x.MessageID}" : $"{x.ContainerID}:{x.MessageID}";
                    Logger.Log(Logger.Level.Debug, $"Requeuing {ID} - {x.Request["_"]}");
                    SendQueue.Add(x);
                });

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurrect while updating the salt.\n\n{ex.Message}");
            }

            // Yes, we handled it
            return true;
        }
        private bool ProcessBadMsgNotification(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Debug, $"Error code {(long)Message["body"]["bad_msg_id"]}");

                var states = GetStatesByID((long)Message["body"]["bad_msg_id"])
                    ?? throw new Exception($"A message or container with id { (long)Message["body"]["bad_msg_id"] } could not found.  Skipping.");

                Logger.Log(Logger.Level.Debug, $"Identified {states.Count()} messages as containing an error");

                switch ((int)Message["body"]["error_code"])
                {
                    case 16:
                    case 17:
                        // Sent msg_id too low or too high (respectively).
                        // Use the current msg_id to determine the right time offset.
                        Logger.Log(Logger.Level.Debug, $"Updating time offset");
                        State.UpdateTimeOffset((long)Message["msg_id"]);
                        break;
                    case 18:
                        // Invalid msg_id (msg_id should be divisible by 4)
                        // This shouldn't happen as we have checks in place to make sure it doesn't.
                        // If these checks have failed, we should probably revaluate our code
                        var sad18 = new Exception("The message was rejected by the server for having a bad Message ID.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad18);
                            SendQueue.Add(x);
                        });
                        throw sad18;
                    case 19:
                        // Container msg_id is the same as msg_id of a previously received
                        var sad19 = new Exception("The message was rejected by the server for having a bad Message ID.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad19);
                            SendQueue.Add(x);
                        });
                        throw sad19;
                    case 20:
                        // Message too old, and it cannot be verified whether the server
                        // has received a message with this msg_id or not
                        var sad20 = new Exception("The message was rejected by the server for having an old Message ID.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad20);
                            SendQueue.Add(x);
                        });
                        throw sad20;
                    case 32:
                        // msg_seqno too low, so just pump it up by some "large" amount
                        // TODO A better fix would be to start with a new fresh session ID
                        Logger.Log(Logger.Level.Debug, $"Updating sequence number");
                        State.Sequence += 64;
                        break;
                    case 33:
                        Logger.Log(Logger.Level.Debug, $"Updating sequence number");
                        // msg_seqno too high
                        State.Sequence -= 16;
                        break;
                    case 34:
                        // An even msg_seqno expected (irrelevant message), but odd received
                        var sad34 = new Exception("The message was rejected by the server for having an invalid squence number.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad34);
                            SendQueue.Add(x);
                        });
                        throw sad34;
                    case 35:
                        // Odd msg_seqno expected (relevant message), but even received.
                        var sad35 = new Exception("The message was rejected by the server for having an invalid squence number.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad35);
                            SendQueue.Add(x);
                        });
                        throw sad35;
                    case 48:
                        // bad_server_salt
                        // handled elsewhere
                        Logger.Log(Logger.Level.Debug, $"Bad salt, what now?  Does salt lose its flavor?");
                        break;
                    case 64:
                        // Invalid container.
                        // So, we could just disable batch sending of messages, but likely the code should just be updated
                        var sad64 = new Exception("The message was rejected by the server for having an invalid container.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad64);
                            SendQueue.Add(x);
                        });
                        throw sad64;
                    default:
                        var sad = new Exception($"The message was rejected by the server with error code {(int)Message["body"]["error_code"]}.  This is not your fault.  Please contact a dev to have this issue resolved.");
                        states.ToList().ForEach(x => {
                            Logger.Log(Logger.Level.Debug, $"Throwing exception on the message {x.MessageID} - {x.Request["_"]}");
                            x.Response.TrySetException(sad);
                            SendQueue.Add(x);
                        });
                        throw sad;
                }

                // Messages are to be re-sent once we've corrected the issue
                states.ToList().ForEach(x => {
                    var ID = x.ContainerID == -1L ? $"{x.MessageID}" : $"{x.ContainerID}:{x.MessageID}";
                    Logger.Log(Logger.Level.Debug, $"Requeuing {ID} - {x.Request["_"]}");
                    SendQueue.Add(x);
                });
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while processing an error.  Funny thing, that is.\n\n{ex.Message}");
            }

            return true;
        }
        private bool ProcessFutureSalts(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Debug, $"Handling future salts update");

                var states = GetStatesByID((long)Message["body"]["req_msg_id"])
                    ?? throw new Exception($"A message or container with id { (long)Message["body"]["req_msg_id"] } could not found.  Skipping.");

                states.ToList().ForEach(x => {
                    var ID = x.ContainerID == -1L ? $"{x.MessageID}" : $"{x.ContainerID}:{x.MessageID}";
                    Logger.Log(Logger.Level.Debug, $"Resolving {ID} - {x.Request["_"]}");
                    x.Response.SetResult(new TLObject(Message["body"]["result"]));
                });

                // ToDo: Add to state on a timer so that we can automatically switch.
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while handing future salts.\n\n{ex.Message}");
            }

            return true;
        }
        private async Task<bool> ProcessGzipPacked(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Debug, $"Handling gzip packed update");

                var PackedData = Message["body"]["packed_data"];

                if (PackedData.Type != JTokenType.Bytes)
                {
                    throw new Exception($"Expected raw bytes, but got {PackedData.Type.ToString()}");
                }

                using (var memory = new MemoryStream())
                using (var packedStream = new MemoryStream((byte[])PackedData, false))
                using (var zipStream = new GZipStream(packedStream, CompressionMode.Decompress))
                using (var compressedReader = new BinaryReader(memory))
                {
                    zipStream.CopyTo(memory);
                    Message["body"] = TLObject.Deserialize(memory.ToArray());
                }

                return await ProcessUpdate(Message);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while handing gzip packed updates.\n\n{ex.Message}");
            }

            return true;
        }
        private bool ProcessMessageContainer(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Debug, $"Handling message container update");

                return Message["body"]["messages"]
                    .Select(async (x) => await ProcessUpdate(new TLObject(x)))
                    .All(x => true);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while handing message container updates.\n\n{ex.Message}");
            }

            return true;
        }
        private bool ProcessMessageDetailedInfo(TLObject Message)
        {
            //    msg_id = message.obj.answer_msg_id
            //    self._log.debug('Handling detailed info for message %d', msg_id)
            //    self._pending_ack.add(msg_id)

            //if (isset($this->outgoing_messages[$this->incoming_messages[$current_msg_id]['content']['msg_id']]))
            //{
            //    if (isset($this->incoming_messages[$this->incoming_messages[$current_msg_id]['content']['answer_msg_id']]))
            //    {
            //                $this->handle_response($this->incoming_messages[$current_msg_id]['content']['msg_id'], $this->incoming_messages[$current_msg_id]['content']['answer_msg_id']);
            //    }
            //    else
            //    {
            //                $this->callFork($this->object_call_async('msg_resend_req', ['msg_ids' => [$this->incoming_messages[$current_msg_id]['content']['answer_msg_id']]], ['postpone' => true]));
            //    }
            //}

            return true;
        }
        private bool ProcessNewMessageDetailedInfo(TLObject Message)
        {
            //    msg_id = message.obj.answer_msg_id
            //    self._log.debug('Handling new detailed info for message %d', msg_id)
            //    self._pending_ack.add(msg_id)

            //if (isset($this->incoming_messages[$this->incoming_messages[$current_msg_id]['content']['answer_msg_id']]))
            //{
            //            $this->ack_incoming_message_id($this->incoming_messages[$current_msg_id]['content']['answer_msg_id']);
            //}
            //else
            //{
            //            $this->callFork($this->object_call_async('msg_resend_req', ['msg_ids' => [$this->incoming_messages[$current_msg_id]['content']['answer_msg_id']]], ['postpone' => true]));
            //}
            return true;
        }
        private bool ProcessMessageResendRequest(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Info, $"Processing message resend request");

                // Requeue the message(s) that triggered the error
                var StatesToResend = Message["body"]["msg_ids"].Select(x =>
                {
                    var results = GetStatesByID((long)x);

                    if (results == null)
                    {
                        Logger.Log(Logger.Level.Error, $"A message or container with id {(long)x} could not found.  Skipping.");
                    }

                    return results;
                }).Join();

                Logger.Log(Logger.Level.Debug, $"Identified {StatesToResend.Count()} message(s) to resend");

                StatesToResend.ToList().ForEach(x => {
                    var ID = x.ContainerID == -1L ? $"{x.MessageID}" : $"{x.ContainerID}:{x.MessageID}";
                    Logger.Log(Logger.Level.Debug, $"Requeuing {ID} - {x.Request["_"]}");
                    SendQueue.Add(x);
                });
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurrect while resending the requested messages.\n\n{ex.Message}");
            }

            // Yes, we handled it
            return true;
        }
        private bool ProcessMessageAck(TLObject Message)
        {
            try
            {
                //    ack = message.obj
                //    self._log.debug('Handling acknowledge for %s', str(ack.msg_ids))
                //    for msg_id in ack.msg_ids:
                //        state = self._pending_state.get(msg_id)
                //        if state and isinstance(state.request, LogOutRequest):
                //            del self._pending_state[msg_id]
                //            state.future.set_result(True)

                Logger.Log(Logger.Level.Info, $"Processing message acknowledgement");

                //var globalResults = new List<RequestState>();

                //// Requeue the message(s) that triggered the error
                //foreach (var id in (JArray)Message["body"]["msg_ids"])
                //{
                //    var results = GetStatesByID((long)id);

                //    if (results.Count() < 1)
                //    {
                //        Logger.Log(Logger.Level.Error, $"A message or container with id {(long)id} could not found.  Skipping.");
                //    }

                //    globalResults.AddRange(results);
                //}

                //Logger.Log(Logger.Level.Debug, $"Identified {globalResults.Count()} message(s) acknowledged");

                // No need to reprocess...
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurrect while processing message acknowledgements.\n\n{ex.Message}");
            }

            // Yes, we handled it
            return true;
        }
        private bool ProcessMessageInfoAll(TLObject Message)
        {
            //    self._send_queue.append(RequestState(MsgsStateInfo(
            //        req_msg_id=message.msg_id, info=chr(1) * len(message.obj.msg_ids)),
            //        loop=self._loop))

            //foreach ($this->incoming_messages[$current_msg_id]['content']['msg_ids'] as $key => $msg_id) {
            //            $info = \ord($this->incoming_messages[$current_msg_id]['content']['info'][$key]);
            //            $msg_id = new \phpseclib\Math\BigInteger(\strrev($msg_id), 256);
            //            $status = 'Status for message id '.$msg_id.': ';
            //    /*if ($info & 4) {
            //     *$this->got_response_for_outgoing_message_id($msg_id);
            //     *}
            //     */
            //    foreach (MTProto::MSGS_INFO_FLAGS as $flag => $description) {
            //        if (($info & $flag) !== 0) {
            //                    $status.= $description;
            //        }
            //    }
            //            $this->logger->logger($status, \danog\MadelineProto\Logger::NOTICE);
            //}
            return true;
        }
        private bool ProcessMessageStateReqest(TLObject Message)
        {
            //    self._send_queue.append(RequestState(MsgsStateInfo(
            //        req_msg_id=message.msg_id, info=chr(1) * len(message.obj.msg_ids)),
            //        loop=self._loop))

            //$this->callFork($this->send_msgs_state_info_async($current_msg_id, $this->incoming_messages[$current_msg_id]['content']['msg_ids']));
            //unset($this->incoming_messages[$current_msg_id]['content']);
            return true;
        }
        private bool ProcessNewSessionCreated(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Info, $"New session created!");

                // Update the Salt
                State.Salt = (long)Message["body"]["server_salt"];
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Error updating the salt when the new session was created.\n\n{ex.Message}");
            }

            // Yes, we handled it
            return true;
        }
        private bool ProcessPong(TLObject Message)
        {
            //    pong = message.obj
            //    self._log.debug('Handling pong for message %d', pong.msg_id)
            //    state = self._pending_state.pop(pong.msg_id, None)
            //    if state:
            //        state.future.set_result(pong)
            return true;
        }
        private async Task<bool> ProcessRPCResult(TLObject Message)
        {
            try
            {
                Logger.Log(Logger.Level.Info, $"Handling RPC Result");

                var StatesToResend = GetStatesByID((long)Message["body"]["req_msg_id"])
                    ?? throw new Exception($"A message or container with id {(long)Message["body"]["req_msg_id"]} could not found.  Skipping.");

                return StatesToResend
                    .Select(x => x.Response.TrySetResult(new TLObject(Message["body"]["result"])))
                    .All(x => true);

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurrect while updating the salt.\n\n{ex.Message}");
            }

            // Yes, we handled it
            return true;

            //    if rpc_result.error:
            //        error = rpc_message_to_error(rpc_result.error, state.request)
            //        self._send_queue.append(
            //            RequestState(MsgsAck([state.msg_id]), loop=self._loop))

            //        if not state.future.cancelled():
            //            state.future.set_exception(error)
            //    else:
            //        with BinaryReader(rpc_result.body) as reader:
            //            result = state.request.read_result(reader)

            //        if not state.future.cancelled():
            //            state.future.set_result(result)
        }
        #endregion

    }
}
