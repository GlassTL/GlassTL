using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.Network;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.Extensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "<Pending>")]
    public class PackedMessage
    {
        public RequestState[] Batch { get; set; }

        public byte[] Data { get; set; }

    }

    /// <summary>
    /// Thread-Safe collection that returns a <see cref="PackedMessage"/> if one or more items could be retrieved.
    /// </summary>
    public class MessagePacker : BlockingCollection<RequestState>
    {
        private readonly MTProtoHelper _State;

        /// <summary>
        /// Gets or sets a value indicating whether multiple requests should be packaged together or sent individually
        /// 
        /// This is highly recommended
        /// </summary>
        public bool PackageRequests { get; set; } = true;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="State">A Reference (important) to the state to support serialization and packaging</param>
        public MessagePacker(ref MTProtoHelper State)
        {
            _State = State;
        }

        /// <summary>
        /// Attempts to get items from the collection, package them, and returns the data
        /// </summary>
        /// <returns></returns>
        public PackedMessage Get()
        {
            Logger.Log(Logger.Level.Info, "Attempting to package requests");

            // That was easy
            if (Count < 1)
            {
                Logger.Log(Logger.Level.Info, "No requests to send.");
                return null;
            }

            // Create some local variables.

            /*
             * Batch contains everything we are sending.
             * Size is the total payload
             * RawPackedData is the compilation of all the serialized data
             */
            var Batch = new List<RequestState>();
            var Size = 0;
            var RawPackedData = new List<byte[]>();

            // Loop while there're requests to send AND we are sending less than 100
            while (Count > 0 && Batch.Count <= 100)
            {
                // Remove an item and calculate the payload
                var State = Take();
                Size += State.Data.Length + 12;

                // Make sure we aren't sending more than we can handle.
                // The server will not be happy and you may have to give
                // it chocolate to make it feel better.  Don't oversend...
                if (Size <= 1044448)
                {
                    // Serializes the request and returns assigned message ID
                    State.MessageID = _State.WriteDataAsMessage(State.Data, out byte[] serialized, true);
                    // Save this data for later use
                    RawPackedData.Add(serialized);
                    // Save the original request
                    Batch.Add(State);

                    Logger.Log(Logger.Level.Debug, $"Found {State.Request["_"]}.  Assigned Message ID: {State.MessageID}.");

                    // *IF* you need to send messages one at a time, fine....
                    if (!PackageRequests)
                    {
                        if (Count > 0)
                        {
                            Logger.Log(Logger.Level.Debug, $"Limited to one message per package.  Skipping the rest.");
                        }

                        break;
                    }

                    // Attempt to add another message to the container
                    continue;
                }
                    
                // Getting here means that we can't add any more to the
                // container even though we have some left.  Spam much?
                if (Batch.Count > 0)
                {
                    Logger.Log(Logger.Level.Debug, $"Skipping {State.Request["_"]} because it won't fit in this package.");
                    // Assuming that there are others that we already
                    // processed, place this one back so we can send
                    // it next time and stop looping
                    Add(State);
                    break;
                }

                Logger.Log(Logger.Level.Debug, $"Skipping {State.Request["_"]} which exceeds the payload limit.  Cancelling request.");

                // Getting HERE means that this one message is too large
                // in and of itself.  We cannot send it.  Rather than
                // throwing an exception here, we will throw an exception
                // on the Task that is being awaited.
                State.Response.SetException(new Exception("The request exceeds the size limit of 1,044,448 bytes.  Please split the request into multiple smaller ones."));
                // And reset the size since we are back down to 0
                Size = 0;
            }

            // If we finished looping and didn't get anything to send, it
            // means we can't.  So, just return null
            if (Batch.Count == 0)
            {
                Logger.Log(Logger.Level.Info, $"Unable to find any valid requests");
                return null;
            }

            // If you are a smart person and package your requests into one
            // container, we handle that here
            if (Batch.Count > 1)
            {
                Logger.Log(Logger.Level.Debug, $"Attempting to package {Batch.Count} requests into one container");

                // Manually create a container since it's not included in the schema
                var msg_container = ManualTypes.CreateMessageContainer(RawPackedData.ToArray());
                // Serialize the container like we did above.  This will be the container id
                var container_id = _State.WriteDataAsMessage(msg_container, out byte[] serialized, false);
                // Repackage all the data into one
                RawPackedData = new List<byte[]> { serialized };
                // Assign the container id on all the requests inside
                Batch.ForEach(x => x.ContainerID = container_id);
            }

            var CompiledData = RawPackedData.Join();

            Logger.Log(Logger.Level.Info, $"Returning a {nameof(PackedMessage)} with a total of {Batch.Count} requests");
            Logger.Log(Logger.Level.Info, $"Total Payload: {CompiledData.Length}");

            return new PackedMessage()
            {
                Batch = Batch.ToArray(),
                Data = CompiledData
            };
        }
    }
}
